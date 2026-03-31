use evdev::{Device, EventType, InputEventKind, Key};
use std::collections::HashMap;
use std::path::Path;
use tokio::sync::mpsc;
use log::{info, warn, debug};

/// Events sent from the input monitor to the engine
#[derive(Debug, Clone)]
pub enum InputAction {
    KeyDown { code: u16, is_mouse: bool },
    KeyUp { code: u16, is_mouse: bool },
}

/// Returns true if this evdev key code is a mouse button
pub fn is_mouse_button(code: u16) -> bool {
    // BTN_MOUSE (0x110) through BTN_TASK (0x117), plus BTN_SIDE, BTN_EXTRA, etc.
    (0x110..=0x11f).contains(&code) || (0x150..=0x151).contains(&code)
}

/// Scan /dev/input/ for keyboard and mouse devices and monitor them
pub async fn monitor_inputs(tx: mpsc::Sender<InputAction>) {
    let mut handles = Vec::new();

    // Enumerate all /dev/input/event* devices
    let devices = match evdev::enumerate() {
        devices => devices.collect::<Vec<_>>(),
    };

    for (path, device) in devices {
        let name = device.name().unwrap_or("Unknown").to_string();
        let supported = device.supported_events();

        // Only monitor devices that produce key events
        if !supported.contains(EventType::KEY) {
            continue;
        }

        info!("Monitoring: {} ({})", name, path.display());
        let tx = tx.clone();

        let handle = tokio::task::spawn_blocking(move || {
            monitor_device(device, &path, tx);
        });
        handles.push(handle);
    }

    if handles.is_empty() {
        warn!("No input devices found! Make sure you're in the 'input' group.");
        warn!("  Run: sudo usermod -aG input $USER  (then log out and back in)");
        return;
    }

    // Wait for all device monitors (they run forever until the device is removed)
    for h in handles {
        let _ = h.await;
    }
}

fn monitor_device(mut device: Device, path: &Path, tx: mpsc::Sender<InputAction>) {
    // Grab the device would prevent other apps from seeing events — we don't want that.
    // We just passively read events.
    loop {
        match device.fetch_events() {
            Ok(events) => {
                for event in events {
                    if let InputEventKind::Key(key) = event.kind() {
                        let code = key.code();
                        let is_mouse = is_mouse_button(code);
                        let action = match event.value() {
                            1 => Some(InputAction::KeyDown { code, is_mouse }),  // press
                            0 => Some(InputAction::KeyUp { code, is_mouse }),    // release
                            2 => None, // autorepeat — ignore, we handle our own
                            _ => None,
                        };
                        if let Some(action) = action {
                            debug!("Input: {:?} (code {})", action, code);
                            if tx.blocking_send(action).is_err() {
                                return; // channel closed
                            }
                        }
                    }
                }
            }
            Err(e) => {
                warn!("Device {} error: {} — stopping monitor", path.display(), e);
                return;
            }
        }
    }
}

/// Get a human-readable name for an evdev key code
pub fn key_name(code: u16) -> String {
    format!("{:?}", Key::new(code))
}
