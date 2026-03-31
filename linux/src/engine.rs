use crate::config::{AppSettings, KeyBinding};
use crate::injector::Injector;
use crate::input::InputAction;
use log::{debug, warn};
use std::collections::HashMap;
use std::sync::{Arc, Mutex};
use tokio::sync::mpsc;
use tokio::time::{sleep, Duration};

/// The repeat engine: listens for input events and injects repeats for bound keys
pub struct RepeatEngine {
    settings: Arc<Mutex<AppSettings>>,
    injector: Arc<Mutex<Injector>>,
    /// Active repeat tasks, keyed by binding ID — cancel via the sender
    active: Arc<Mutex<HashMap<String, tokio::sync::oneshot::Sender<()>>>>,
}

impl RepeatEngine {
    pub fn new(settings: Arc<Mutex<AppSettings>>, injector: Injector) -> Self {
        Self {
            settings,
            injector: Arc::new(Mutex::new(injector)),
            active: Arc::new(Mutex::new(HashMap::new())),
        }
    }

    /// Main loop: process input events from the channel
    pub async fn run(&self, mut rx: mpsc::Receiver<InputAction>) {
        while let Some(action) = rx.recv().await {
            match action {
                InputAction::KeyDown { code, is_mouse } => {
                    self.on_key_down(code, is_mouse);
                }
                InputAction::KeyUp { code, is_mouse } => {
                    self.on_key_up(code, is_mouse);
                }
            }
        }
    }

    fn on_key_down(&self, code: u16, is_mouse: bool) {
        let settings = self.settings.lock().unwrap();
        if !settings.is_enabled {
            return;
        }

        // Find a binding that matches this trigger
        let binding = settings.bindings.iter().find(|b| {
            b.trigger_code == code && b.trigger_is_mouse == is_mouse
        });
        let binding = match binding {
            Some(b) => b.clone(),
            None => return,
        };

        let interval_ms = settings.repeat_interval_ms;
        drop(settings); // release lock

        let binding_id = binding.id();

        // Single press mode: fire once, no repeat loop
        if binding.mode == "single_press" {
            let mut inj = self.injector.lock().unwrap();
            let output_code = binding.actual_output_code();
            if binding.actual_output_is_mouse() {
                let _ = inj.click_button(output_code);
            } else {
                let _ = inj.tap_key(output_code);
            }
            return;
        }

        // Repeat mode: start an async repeat loop
        let mut active = self.active.lock().unwrap();
        if active.contains_key(&binding_id) {
            return; // already repeating
        }

        let (cancel_tx, cancel_rx) = tokio::sync::oneshot::channel::<()>();
        active.insert(binding_id.clone(), cancel_tx);
        drop(active);

        let injector = self.injector.clone();
        let active_map = self.active.clone();

        tokio::spawn(async move {
            let output_code = binding.actual_output_code();
            let output_is_mouse = binding.actual_output_is_mouse();

            tokio::select! {
                _ = cancel_rx => {
                    debug!("Repeat cancelled for {}", binding_id);
                }
                _ = async {
                    // Initial delay before first repeat
                    sleep(Duration::from_millis(interval_ms)).await;
                    loop {
                        {
                            let mut inj = injector.lock().unwrap();
                            if output_is_mouse {
                                let _ = inj.click_button(output_code);
                            } else {
                                let _ = inj.tap_key(output_code);
                            }
                        }
                        sleep(Duration::from_millis(interval_ms)).await;
                    }
                } => {}
            }

            // Clean up
            let mut active = active_map.lock().unwrap();
            active.remove(&binding_id);
        });
    }

    fn on_key_up(&self, code: u16, is_mouse: bool) {
        let id = if is_mouse {
            format!("MOUSE_{}", code)
        } else {
            format!("KEY_{}", code)
        };

        let mut active = self.active.lock().unwrap();
        if let Some(cancel_tx) = active.remove(&id) {
            let _ = cancel_tx.send(());
        }
    }

    pub fn stop_all(&self) {
        let mut active = self.active.lock().unwrap();
        let ids: Vec<String> = active.keys().cloned().collect();
        for id in ids {
            if let Some(cancel_tx) = active.remove(&id) {
                let _ = cancel_tx.send(());
            }
        }
    }
}
