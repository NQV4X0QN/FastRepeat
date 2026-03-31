use serde::{Deserialize, Serialize};
use std::fs;
use std::path::PathBuf;

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct KeyBinding {
    /// evdev event code for the trigger key/button
    pub trigger_code: u16,
    /// true if trigger is a mouse button (EV_KEY with BTN_* code)
    pub trigger_is_mouse: bool,
    /// evdev event code for the output key/button (None = same as trigger)
    pub output_code: Option<u16>,
    /// true if output is a mouse button
    pub output_is_mouse: Option<bool>,
    /// "repeat" or "single_press"
    #[serde(default = "default_mode")]
    pub mode: String,
    /// Human-readable name for display
    #[serde(default)]
    pub display_name: String,
}

fn default_mode() -> String {
    "repeat".to_string()
}

impl KeyBinding {
    pub fn actual_output_code(&self) -> u16 {
        self.output_code.unwrap_or(self.trigger_code)
    }

    pub fn actual_output_is_mouse(&self) -> bool {
        self.output_is_mouse.unwrap_or(self.trigger_is_mouse)
    }

    pub fn id(&self) -> String {
        if self.trigger_is_mouse {
            format!("MOUSE_{}", self.trigger_code)
        } else {
            format!("KEY_{}", self.trigger_code)
        }
    }
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct AppSettings {
    #[serde(default = "default_interval")]
    pub repeat_interval_ms: u64,
    #[serde(default)]
    pub is_speed_locked: bool,
    #[serde(default = "default_true")]
    pub is_enabled: bool,
    #[serde(default)]
    pub bindings: Vec<KeyBinding>,
}

fn default_interval() -> u64 { 100 }
fn default_true() -> bool { true }

impl Default for AppSettings {
    fn default() -> Self {
        Self {
            repeat_interval_ms: 100,
            is_speed_locked: false,
            is_enabled: true,
            bindings: Vec::new(),
        }
    }
}

impl AppSettings {
    pub fn config_path() -> PathBuf {
        let config_dir = dirs::config_dir()
            .unwrap_or_else(|| PathBuf::from("~/.config"))
            .join("fastrepeat");
        config_dir.join("settings.json")
    }

    pub fn load() -> Self {
        let path = Self::config_path();
        if path.exists() {
            match fs::read_to_string(&path) {
                Ok(text) => serde_json::from_str(&text).unwrap_or_default(),
                Err(_) => Self::default(),
            }
        } else {
            Self::default()
        }
    }

    pub fn save(&self) -> Result<(), Box<dyn std::error::Error>> {
        let path = Self::config_path();
        if let Some(parent) = path.parent() {
            fs::create_dir_all(parent)?;
        }
        let json = serde_json::to_string_pretty(self)?;
        fs::write(path, json)?;
        Ok(())
    }
}
