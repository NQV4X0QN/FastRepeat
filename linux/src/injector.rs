use evdev::uinput::VirtualDeviceBuilder;
use evdev::{AttributeSet, EventType, InputEvent, Key, RelativeAxisType};
use log::info;
use std::io;

/// Wrapper around a uinput virtual device for injecting synthetic key/mouse events
pub struct Injector {
    device: evdev::uinput::VirtualDevice,
}

impl Injector {
    pub fn new() -> io::Result<Self> {
        let mut keys = AttributeSet::<Key>::new();
        // Register all keyboard keys (KEY_ESC through KEY_MAX)
        for code in 1..=767 {
            keys.insert(Key::new(code));
        }

        let device = VirtualDeviceBuilder::new()?
            .name("FastRepeat Virtual Device")
            .with_keys(&keys)?
            .with_relative_axes(&{
                let mut axes = AttributeSet::<RelativeAxisType>::new();
                axes.insert(RelativeAxisType::REL_X);
                axes.insert(RelativeAxisType::REL_Y);
                axes.insert(RelativeAxisType::REL_WHEEL);
                axes.insert(RelativeAxisType::REL_HWHEEL);
                axes
            })?
            .build()?;

        info!("Virtual input device created: FastRepeat Virtual Device");
        Ok(Self { device })
    }

    /// Send a key press + release (tap)
    pub fn tap_key(&mut self, code: u16) -> io::Result<()> {
        let key = Key::new(code);
        let down = InputEvent::new(EventType::KEY, code, 1);
        let up = InputEvent::new(EventType::KEY, code, 0);
        let sync = InputEvent::new(EventType::SYNCHRONIZATION, 0, 0);

        self.device.emit(&[down, sync])?;
        self.device.emit(&[up, sync])?;
        Ok(())
    }

    /// Send a mouse button press + release (click)
    pub fn click_button(&mut self, code: u16) -> io::Result<()> {
        // Mouse buttons use the same EV_KEY type, just different codes
        self.tap_key(code)
    }
}
