mod config;
mod engine;
mod injector;
mod input;
mod gui;

use clap::{Parser, Subcommand};
use config::{AppSettings, KeyBinding};
use engine::RepeatEngine;
use injector::Injector;
use input::{is_mouse_button, key_name, CapturedKey};
use log::{error, info};
use std::sync::{Arc, Mutex};
use tokio::sync::mpsc;

const VERSION: &str = env!("CARGO_PKG_VERSION");

#[derive(Parser)]
#[command(name = "fastrepeat", version = VERSION, about = "System-wide key and mouse button repeater for Linux")]
struct Cli {
    #[command(subcommand)]
    command: Option<Commands>,
}

#[derive(Subcommand)]
enum Commands {
    /// Launch the graphical interface (default if no command given)
    Gui,
    /// Run the repeater daemon
    Run,
    /// Add a new key binding interactively
    Add,
    /// List all current bindings
    List,
    /// Remove a binding by index
    Remove {
        /// Index of the binding to remove (from 'list' output)
        index: usize,
    },
    /// Set the repeat interval in milliseconds
    Speed {
        /// Repeat interval in ms (25-600)
        ms: u64,
    },
    /// Enable or disable the repeater
    Enable,
    /// Disable the repeater
    Disable,
    /// Show current status
    Status,
}

#[tokio::main]
async fn main() {
    env_logger::Builder::from_env(env_logger::Env::default().default_filter_or("info"))
        .format_timestamp(None)
        .init();

    let cli = Cli::parse();

    match cli.command.unwrap_or(Commands::Gui) {
        Commands::Gui => gui::run_gui(),
        Commands::Run => cmd_run().await,
        Commands::Add => cmd_add(),
        Commands::List => cmd_list(),
        Commands::Remove { index } => cmd_remove(index),
        Commands::Speed { ms } => cmd_speed(ms),
        Commands::Enable => cmd_set_enabled(true),
        Commands::Disable => cmd_set_enabled(false),
        Commands::Status => cmd_status(),
    }
}

async fn cmd_run() {
    info!("Fast Repeat v{} — starting", VERSION);

    let settings = AppSettings::load();
    let n = settings.bindings.len();
    info!("Loaded {} binding(s), interval {}ms, {}",
        n, settings.repeat_interval_ms,
        if settings.is_enabled { "enabled" } else { "disabled" });

    // Create the virtual input device for injection
    let injector = match Injector::new() {
        Ok(inj) => inj,
        Err(e) => {
            error!("Failed to create virtual input device: {}", e);
            error!("Make sure you have permission to access /dev/uinput.");
            error!("  Run: sudo usermod -aG input $USER  (then log out and back in)");
            error!("  Or: sudo chmod 0660 /dev/uinput && sudo chown root:input /dev/uinput");
            std::process::exit(1);
        }
    };

    let settings = Arc::new(Mutex::new(settings));
    let engine = RepeatEngine::new(settings.clone(), injector);

    // Channel for input events
    let (tx, rx) = mpsc::channel::<input::InputAction>(256);

    // Start input monitoring in the background
    let _monitor_handle = tokio::spawn(async move {
        input::monitor_inputs(tx).await;
    });

    // Set up signal handling for clean shutdown
    
    tokio::select! {
        _ = engine.run(rx) => {}
        _ = tokio::signal::ctrl_c() => {
            info!("Shutting down...");
        }
    }

    // Save settings on exit
    if let Ok(s) = settings.lock() {
        let _ = s.save();
    }
    info!("Fast Repeat stopped.");
}

fn cmd_add() {
    println!("Fast Repeat — Add Key Binding\n");

    // --- Permission check: try to open evdev devices ---
    let devices: Vec<_> = evdev::enumerate().collect();
    let mut kbd_devices: Vec<_> = devices.into_iter()
        .filter(|(_, d)| d.supported_events().contains(evdev::EventType::KEY))
        .collect();

    if kbd_devices.is_empty() {
        eprintln!("✗ No input devices found.");
        eprintln!("  You need to be in the 'input' group. Run:");
        eprintln!("    sudo usermod -aG input $USER");
        eprintln!("  Then log out and back in.");
        std::process::exit(1);
    }

    // Verify we can actually read events (permission test)
    let mut readable = false;
    for (_, d) in kbd_devices.iter_mut() {
        if d.fetch_events().is_ok() {
            readable = true;
            break;
        }
    }
    if !readable {
        eprintln!("✗ Cannot read any input devices (permission denied).");
        eprintln!("  You need to be in the 'input' group. Run:");
        eprintln!("    sudo usermod -aG input $USER");
        eprintln!("  Then log out and back in.");
        std::process::exit(1);
    }

    println!("Found {} input device(s).\n", kbd_devices.len());

    // --- Step 1: Capture trigger ---
    println!("Step 1/2: Press the key or button to use as the TRIGGER...");
    println!("  (press Ctrl+C to cancel)\n");

    let trigger = capture_key(&mut kbd_devices);
    println!("  Detected: {} (code {}) on {}", trigger.name, trigger.code, trigger.device_name);

    if !confirm("  Use this as trigger?") {
        println!("Cancelled.");
        return;
    }

    // --- Step 2: Capture output ---
    println!("\nStep 2/2: Press the key to REPEAT as output (or same key for self-repeat)...");
    println!("  (press Ctrl+C to cancel)\n");

    let output = capture_key(&mut kbd_devices);
    let same_key = output.code == trigger.code;

    if same_key {
        println!("  Detected: {} (self-repeat)", trigger.name);
    } else {
        println!("  Detected: {} (code {}) on {}", output.name, output.code, output.device_name);
    }

    if !confirm("  Use this as output?") {
        println!("Cancelled.");
        return;
    }

    // --- Step 3: Mode selection ---
    println!("\nRepeat mode:");
    println!("  [1] Repeat while held (default)");
    println!("  [2] Single press on hold");
    let mode = read_choice(2, 1);
    let mode_str = if mode == 1 { "repeat" } else { "single_press" };

    // --- Step 4: Check for duplicates and save ---
    let mut settings = AppSettings::load();

    if settings.bindings.iter().any(|b| b.trigger_code == trigger.code && b.trigger_is_mouse == trigger.is_mouse) {
        eprintln!("\n✗ A binding for {} already exists. Remove it first with 'fastrepeat remove'.", trigger.name);
        std::process::exit(1);
    }

    let binding = KeyBinding {
        trigger_code: trigger.code,
        trigger_is_mouse: trigger.is_mouse,
        output_code: if same_key { None } else { Some(output.code) },
        output_is_mouse: if same_key { None } else { Some(output.is_mouse) },
        mode: mode_str.to_string(),
        display_name: if same_key {
            format!("{} → self", trigger.name)
        } else {
            format!("{} → {}", trigger.name, output.name)
        },
    };

    settings.bindings.push(binding);
    settings.save().expect("Failed to save settings");

    let mode_label = if mode == 1 { "repeat while held" } else { "single press" };
    println!("\n✓ Added binding: {} → {} ({})",
        trigger.name,
        if same_key { "self".to_string() } else { output.name.clone() },
        mode_label);
    println!("  Speed: {}ms (use 'fastrepeat speed <ms>' to change)", settings.repeat_interval_ms);
    println!("  Restart the daemon to apply: fastrepeat run");
}

fn capture_key(devices: &mut [(std::path::PathBuf, evdev::Device)]) -> CapturedKey {
    loop {
        for (path, device) in devices.iter_mut() {
            // Cache device name before mutable borrow from fetch_events()
            let dev_name = device.name()
                .unwrap_or("unknown device")
                .to_string();
            match device.fetch_events() {
                Ok(events) => {
                    for event in events {
                        if let evdev::InputEventKind::Key(key) = event.kind() {
                            if event.value() == 1 {
                                let code = key.code();
                                return CapturedKey {
                                    code,
                                    is_mouse: is_mouse_button(code),
                                    name: key_name(code),
                                    device_name: dev_name,
                                };
                            }
                        }
                    }
                }
                Err(e) => {
                    log::debug!("Could not read from {}: {}", path.display(), e);
                }
            }
        }
        std::thread::sleep(std::time::Duration::from_millis(10));
    }
}

/// Prompt the user for Y/n confirmation. Returns true on Y/Enter, false on n.
fn confirm(prompt: &str) -> bool {
    use std::io::{self, Write};
    print!("{} [Y/n]: ", prompt);
    io::stdout().flush().unwrap();
    let mut input = String::new();
    io::stdin().read_line(&mut input).unwrap_or(0);
    let trimmed = input.trim().to_lowercase();
    trimmed.is_empty() || trimmed == "y" || trimmed == "yes"
}

/// Prompt the user to choose 1..=max, with a default.
fn read_choice(max: u32, default: u32) -> u32 {
    use std::io::{self, Write};
    print!("Choice [{}]: ", default);
    io::stdout().flush().unwrap();
    let mut input = String::new();
    io::stdin().read_line(&mut input).unwrap_or(0);
    let trimmed = input.trim();
    if trimmed.is_empty() {
        return default;
    }
    match trimmed.parse::<u32>() {
        Ok(n) if n >= 1 && n <= max => n,
        _ => {
            println!("  Invalid choice, using default ({}).", default);
            default
        }
    }
}

fn cmd_list() {
    let settings = AppSettings::load();
    if settings.bindings.is_empty() {
        println!("No bindings configured.");
        println!("  Use: fastrepeat add");
        return;
    }
    println!("Key Bindings:\n");
    println!("  {:<4} {:<30} {:<20} {}", "#", "Trigger → Output", "Mode", "Code");
    println!("  {}", "-".repeat(70));
    for (i, b) in settings.bindings.iter().enumerate() {
        let trigger = key_name(b.trigger_code);
        let output = if b.output_code.is_some() {
            key_name(b.actual_output_code())
        } else {
            "(self)".to_string()
        };
        println!("  {:<4} {:<30} {:<20} {}",
            i, format!("{} → {}", trigger, output), b.mode, b.trigger_code);
    }
}

fn cmd_remove(index: usize) {
    let mut settings = AppSettings::load();
    if index >= settings.bindings.len() {
        eprintln!("Invalid index {}. Use 'fastrepeat list' to see bindings.", index);
        std::process::exit(1);
    }
    let removed = settings.bindings.remove(index);
    settings.save().expect("Failed to save settings");
    println!("✓ Removed binding: {}", removed.display_name);
}

fn cmd_speed(ms: u64) {
    if ms < 25 || ms > 600 {
        eprintln!("Speed must be between 25 and 600 ms.");
        std::process::exit(1);
    }
    let mut settings = AppSettings::load();
    settings.repeat_interval_ms = ms;
    settings.save().expect("Failed to save settings");
    println!("✓ Repeat speed set to {}ms", ms);
}

fn cmd_set_enabled(enabled: bool) {
    let mut settings = AppSettings::load();
    settings.is_enabled = enabled;
    settings.save().expect("Failed to save settings");
    println!("✓ Fast Repeat {}", if enabled { "enabled" } else { "disabled" });
}

fn cmd_status() {
    let settings = AppSettings::load();
    println!("Fast Repeat v{}", VERSION);
    println!("  Status:   {}", if settings.is_enabled { "Enabled" } else { "Disabled" });
    println!("  Speed:    {}ms", settings.repeat_interval_ms);
    println!("  Bindings: {}", settings.bindings.len());
    println!("  Config:   {}", AppSettings::config_path().display());
}
