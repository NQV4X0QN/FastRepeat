# Fast Repeat

**Fast Repeat** is a lightweight Windows utility that adds a fully configurable *repeat-on-hold* behaviour to any keyboard key or mouse button — system-wide, including games and full-screen applications.

![Platform](https://img.shields.io/badge/platform-Windows%2010%2F11-blue)
![License](https://img.shields.io/badge/license-MIT-green)
![.NET](https://img.shields.io/badge/.NET-8.0-purple)

---

## Features

| Feature | Detail |
|---|---|
| **Any key or button** | Assign any keyboard key or mouse button (Left, Right, Middle, X1, X2) |
| **System-wide** | Works in all applications, browsers, and games using Windows low-level hooks |
| **Configurable speed** | 25 ms – 600 ms repeat interval (adjustable in real time) |
| **Lockable speed** | One-click lock prevents accidental changes |
| **Enable / Disable toggle** | Pause all repeats instantly from the tray menu |
| **Runs in the system tray** | Minimal footprint — no taskbar button when minimized |
| **Persistent settings** | Configuration saved automatically between sessions |
| **Single portable EXE** | No installation, no .NET runtime required |

---

## Download

Go to the [**Releases**](https://github.com/NQV4X0QN/FastRepeat/releases) page and download the latest `FastRepeat.exe`.
Just run it — no installer, no dependencies.

---

## Usage

1. **Launch** `FastRepeat.exe`. A tray icon appears (blue `F` with green dot = enabled).
2. **Double-click** the tray icon (or right-click → *Open Fast Repeat*) to open the settings window.
3. Click **+ Add Key / Mouse Button** and press the key or click the mouse button you want to repeat.
4. Adjust the **Repeat Speed** slider (lower ms = faster repeats).
5. Click **Lock Speed** to prevent accidental changes.
6. Close the window — Fast Repeat keeps running in the tray.
7. To exit, right-click the tray icon → *Exit*.

### Speed reference

| Interval | Repeats per second |
|---|---|
| 25 ms | ~40 cps (fastest) |
| 50 ms | ~20 cps |
| 100 ms | ~10 cps |
| 200 ms | ~5 cps |
| 600 ms | ~1.7 cps (slowest) |

### Settings file

Settings are stored at:
```
%APPDATA%\FastRepeat\settings.json
```
You can back this up or share it across machines.

---

## How it works

Fast Repeat installs Windows **low-level input hooks** (`WH_KEYBOARD_LL` / `WH_MOUSE_LL`) that observe all input events before they reach any application.
When a monitored key is held, a background timer fires `SendInput()` at the configured interval, injecting synthetic press-and-release events.
All synthetic events are tagged with a unique marker so they are never re-processed by the hook — no infinite loops.

---

## Building from source

Requirements: [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8)

```powershell
# Clone
git clone https://github.com/NQV4X0QN/FastRepeat.git
cd FastRepeat

# Run in debug mode
dotnet run --project src/FastRepeat

# Build self-contained single-file EXE
dotnet publish src/FastRepeat/FastRepeat.csproj `
  --configuration Release `
  --runtime win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  --output ./dist
```

The final `FastRepeat.exe` will be in `./dist/`.

### CI / GitHub Actions

Every push of a version tag (`v*.*.*`) triggers the workflow at `.github/workflows/build-release.yml`, which:
1. Builds the self-contained EXE on `windows-latest`.
2. Publishes it as a GitHub Release asset.

You can also trigger a build manually from the **Actions** tab.

---

## Notes for games

- **Most games** (DirectX, Vulkan, Win32, Unity, Unreal) work fine out of the box.
- For games with **anti-cheat** software (EAC, BattlEye, Vanguard), using input injection tools may violate the game's terms of service. Use at your own risk and discretion.
- If a specific game blocks `SendInput`, try running Fast Repeat **as Administrator**.

---

## License

[MIT](LICENSE) © 2026 NQV4X0QN
