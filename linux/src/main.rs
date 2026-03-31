mod config;
mod engine;
mod injector;
mod input;

use clap::{Parser, Subcommand};
use config::{AppSettings, KeyBinding};
use engine::RepeatEngine;
use injector::Injector;
use input::{is_mouse_button, key_name};
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
    /// Run the repeater daemon (default if no command given)
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

    match cli.command.unwrap_or(Commands::Run) {
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
    let monitor_handle = tokio::spawn(async move {
        input::monitor_inputs(tx).await;
    });

    // Set up signal handling for clean shutdown
    let engine_ref = &engine;
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
    println!("This will capture the next key or mouse button you press.\n");

    // We need to read a raw input event to capture the key
    let devices: Vec<_> = evdev::enumerate().collect();
    let kbd_devices: Vec<_> = devices.into_iter()
        .filter(|(_, d)| d.supported_events().contains(evdev::EventType::KEY))
        .collect();

    if kbd_devices.is_empty() {
        eprintln!("No input devices found. Make sure you're in the 'input' group.");
        std::process::exit(1);
    }

    println!("Press the TRIGGER key (the key you will hold)...");

    let trigger = capture_key(&kbd_devices);
    let trigger_name = key_name(trigger.0);
    let trigger_is_mouse = is_mouse_button(trigger.0);
    println!("  Trigger: {} (code {})\n", trigger_name, trigger.0);

    println!("Press the OUTPUT key (the key to repeat), or press the same key for self-repeat...");

    let output = capture_key(&kbd_devices);
    let output_name = key_name(output.0);
    let output_is_mouse = is_mouse_button(output.0);

    let same_key = output.0 == trigger.0;

    let binding = KeyBinding {
        trigger_code: trigger.0,
        trigger_is_mouse,
        output_code: if same_key { None } else { Some(output.0) },
        output_is_mouse: if same_key { None } else { Some(output_is_mouse) },
        mode: "repeat".to_string(),
        display_name: if same_key {
            format!("{} → self", trigger_name)
        } else {
            format!("{} → {}", trigger_name, output_name)
        },
    };

    let mut settings = AppSettings::load();

    // Check for duplicate trigger
    if settings.bindings.iter().any(|b| b.trigger_code == trigger.0 && b.trigger_is_mouse == trigger_is_mouse) {
        eprintln!("A binding for {} already exists. Remove it first.", trigger_name);
        std::process::exit(1);
    }

    settings.bindings.push(binding);
    settings.save().expect("Failed to save settings");
    println!("\n✓ Binding added. Restart the daemon to apply.");
}

fn capture_key(devices: &[(std::path::PathBuf, evdev::Device)]) -> (u16, bool) {
    // Simple blocking capture — read from all devices until we get a key press
    use std::os::fd::AsRawFd;
    loop {
        for (_, device) in devices {
            // Non-blocking read
            if let Ok(events) = device.fetch_events() {
                for event in events {
                    if let evdev::InputEventKind::Key(key) = event.kind() {
                        if event.value() == 1 {
                            return (key.code(), is_mouse_button(key.code()));
                        }
                    }
                }
            }
        }
        std::thread::sleep(std::time::Duration::from_millis(10));
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
