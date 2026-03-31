use ksni;
use std::sync::{Arc, atomic::{AtomicBool, Ordering}};

/// Actions the tray can send to the GTK main thread
#[derive(Debug, Clone)]
pub enum TrayAction {
    ShowWindow,
    ToggleEnabled,
    ToggleAutostart,
    Quit,
}

/// Shared state between the tray (background thread) and GTK (main thread)
pub struct TrayState {
    pub is_enabled: AtomicBool,
    pub autostart_enabled: AtomicBool,
}

impl TrayState {
    pub fn new(is_enabled: bool) -> Self {
        let autostart = check_autostart();
        Self {
            is_enabled: AtomicBool::new(is_enabled),
            autostart_enabled: AtomicBool::new(autostart),
        }
    }
}

/// Check if the systemd user service is enabled
fn check_autostart() -> bool {
    std::process::Command::new("systemctl")
        .args(["--user", "is-enabled", "fastrepeat.service"])
        .output()
        .map(|o| o.status.success())
        .unwrap_or(false)
}

/// Toggle the systemd user service
fn toggle_autostart(enable: bool) -> bool {
    let action = if enable { "enable" } else { "disable" };
    std::process::Command::new("systemctl")
        .args(["--user", action, "fastrepeat.service"])
        .output()
        .map(|o| o.status.success())
        .unwrap_or(false)
}

struct FastRepeatTray {
    state: Arc<TrayState>,
    tx: std::sync::mpsc::Sender<TrayAction>,
}

impl ksni::Tray for FastRepeatTray {
    fn id(&self) -> String {
        "fastrepeat".to_string()
    }

    fn title(&self) -> String {
        "Fast Repeat".to_string()
    }

    fn icon_name(&self) -> String {
        // Use a generic keyboard icon from the system theme
        // Falls back to the app name if not found
        "input-keyboard".to_string()
    }

    fn tool_tip(&self) -> ksni::ToolTip {
        let status = if self.state.is_enabled.load(Ordering::Relaxed) {
            "Enabled"
        } else {
            "Disabled"
        };
        ksni::ToolTip {
            title: format!("Fast Repeat — {}", status),
            description: String::new(),
            icon_name: String::new(),
            icon_pixmap: Vec::new(),
        }
    }

    fn menu(&self) -> Vec<ksni::MenuItem<Self>> {
        let is_enabled = self.state.is_enabled.load(Ordering::Relaxed);
        let autostart = self.state.autostart_enabled.load(Ordering::Relaxed);

        vec![
            ksni::MenuItem::Standard(ksni::menu::StandardItem {
                label: "Open".to_string(),
                activate: Box::new(|tray: &mut Self| {
                    let _ = tray.tx.send(TrayAction::ShowWindow);
                }),
                ..Default::default()
            }),
            ksni::MenuItem::Separator,
            ksni::MenuItem::Standard(ksni::menu::StandardItem {
                label: "Enabled".to_string(),
                icon_name: if is_enabled {
                    "emblem-ok-symbolic".to_string()
                } else {
                    String::new()
                },
                activate: Box::new(|tray: &mut Self| {
                    let _ = tray.tx.send(TrayAction::ToggleEnabled);
                }),
                ..Default::default()
            }),
            ksni::MenuItem::Standard(ksni::menu::StandardItem {
                label: "Run at Startup".to_string(),
                icon_name: if autostart {
                    "emblem-ok-symbolic".to_string()
                } else {
                    String::new()
                },
                activate: Box::new(|tray: &mut Self| {
                    let _ = tray.tx.send(TrayAction::ToggleAutostart);
                }),
                ..Default::default()
            }),
            ksni::MenuItem::Separator,
            ksni::MenuItem::Standard(ksni::menu::StandardItem {
                label: "Exit".to_string(),
                activate: Box::new(|tray: &mut Self| {
                    let _ = tray.tx.send(TrayAction::Quit);
                }),
                ..Default::default()
            }),
        ]
    }

    fn activate(&mut self, _x: i32, _y: i32) {
        // Left-click on the tray icon opens the window
        let _ = self.tx.send(TrayAction::ShowWindow);
    }
}

/// Start the system tray in a background thread.
/// Returns a receiver for tray actions and the shared state handle.
pub fn start_tray(is_enabled: bool) -> (
    std::sync::mpsc::Receiver<TrayAction>,
    Arc<TrayState>,
    ksni::Handle<FastRepeatTray>,
) {
    let (tx, rx) = std::sync::mpsc::channel();
    let state = Arc::new(TrayState::new(is_enabled));

    let tray = FastRepeatTray {
        state: state.clone(),
        tx,
    };

    let service = ksni::TrayService::new(tray);
    let handle = service.handle();
    service.spawn();

    (rx, state, handle)
}

/// Toggle autostart and update shared state. Returns the new state.
pub fn do_toggle_autostart(state: &TrayState) -> bool {
    let current = state.autostart_enabled.load(Ordering::Relaxed);
    let new_state = !current;
    if toggle_autostart(new_state) {
        state.autostart_enabled.store(new_state, Ordering::Relaxed);
        new_state
    } else {
        current
    }
}
