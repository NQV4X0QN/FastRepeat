# Fast Repeat — Linux

System-wide key and mouse button repeater for Linux. Works on both X11 and Wayland by operating at the evdev/uinput kernel level.

## Requirements

- Linux kernel with evdev and uinput support (all modern distros)
- User must be in the `input` group
- Rust toolchain for building from source
- GTK4 development packages (for GUI mode)

## Quick Start

```bash
# Build
cd linux
cargo build --release

# Install
./install.sh

# Add your user to the input group (required, log out/in after)
sudo usermod -aG input $USER

# Launch the GUI (default)
fastrepeat

# Or, add a key binding via CLI
fastrepeat add

# Run the daemon
fastrepeat run
```

## GUI Mode

Fast Repeat includes a native GTK4/libadwaita graphical interface:

```bash
fastrepeat gui    # launch the GUI
fastrepeat        # also launches GUI (default)
```

The GUI provides:
- **Key bindings list** with add/remove/toggle mode buttons
- **Speed slider** with lock to prevent accidental changes
- **Enable/disable toggle** in the header bar for quick control
- Native GNOME/libadwaita styling with dark/light theme support

Note: Key capture (adding bindings) currently requires the CLI — the GUI will prompt
you to run `fastrepeat add` in a terminal. All other operations (remove, toggle mode,
adjust speed, enable/disable) work directly in the GUI.

## Commands

| Command | Description |
|---------|-------------|
| `fastrepeat` | Launch the GUI (default) |
| `fastrepeat gui` | Launch the GUI explicitly |
| `fastrepeat run` | Start the repeater daemon |
| `fastrepeat add` | Interactively add a key binding |
| `fastrepeat list` | Show all configured bindings |
| `fastrepeat remove <index>` | Remove a binding by index |
| `fastrepeat speed <ms>` | Set repeat interval (25-600ms) |
| `fastrepeat enable` | Enable the repeater |
| `fastrepeat disable` | Disable the repeater |
| `fastrepeat status` | Show current configuration |

## Auto-Start

```bash
# Enable the systemd user service
systemctl --user enable fastrepeat
systemctl --user start fastrepeat

# Check logs
journalctl --user -u fastrepeat -f
```

## How It Works

1. **evdev** reads raw input events from `/dev/input/event*` devices at the kernel level — works identically on X11 and Wayland
2. **uinput** creates a virtual input device (`FastRepeat Virtual Device`) for injecting synthetic key presses
3. When a bound key is held, the repeat engine injects press/release events at the configured interval
4. Releasing the key stops the repeat loop immediately

## Configuration

Settings are stored in `~/.config/fastrepeat/settings.json`.
