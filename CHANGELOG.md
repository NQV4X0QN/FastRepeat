# Changelog

All notable changes to Fast Repeat are documented here.
Format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).
This project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---


## [1.7.3] — 2026-03-31

### Fixed (Linux)
- **`fastrepeat add` CLI** — completely rewritten interactive flow:
  - Upfront permission check: tests evdev read access before prompting, exits immediately
    with actionable "add yourself to the input group" message if denied (was: silent hang)
  - Clear step-by-step flow: Step 1/2 (trigger) → Step 2/2 (output) with Ctrl+C hint
  - Shows device name on capture (e.g. "Logitech G502 X") — helps identify hardware buttons
  - Y/n confirmation after each capture so user can re-do if they hit the wrong key
  - Repeat mode selection: repeat while held (default) or single press on hold
  - Summary on success showing binding, speed, and restart hint


## [1.5.1] — 2025-03-30

### Added
- **Self-install to LocalAppData** — on first launch, the EXE copies itself to
  `%LOCALAPPDATA%\FastRepeat\FastRepeat.exe` and relaunches from there. The
  original downloaded file can be safely deleted afterward.
- **Start Menu shortcut** — created automatically during self-install via a
  VBScript shim so the app appears in Windows search and the Start Menu programs list.
- **Add/Remove Programs registration** — writes an uninstall entry to
  `HKCU\...\Uninstall\FastRepeat` with display name, version, publisher, icon,
  and estimated size. The app now appears in Windows Settings > Apps & Features.
- **Full uninstall support** — accessible via the tray menu ("Uninstall") or
  Windows Apps & Features. Prompts to confirm, optionally removes saved settings,
  cleans up registry entries (startup + uninstall), deletes the Start Menu shortcut,
  and removes the EXE via a batch trampoline.
- **Run at Startup toggle** — new tray context menu item that writes/removes a
  `HKCU\...\Run` registry entry. Setting persists in `settings.json` and the
  registry is re-synced on each launch.

### Changed
- **UpdateManager.ApplyUpdate** now always targets the installed path at
  `%LOCALAPPDATA%\FastRepeat` so updates go to the correct location regardless
  of where the EXE is running from.

---



## [1.5.0] — 2026-03-30

### Added
- **Windows 11 Fluent Design overhaul** — complete visual refresh of the main window
  using card-based layout with rounded corners (`RoundedPanel`), matching the WinUI 3
  design language. Two distinct cards for "Assigned Keys" and "Repeat Speed" sections
  provide clear visual hierarchy.
- **Custom FluentButton control** — owner-drawn buttons with 4 px rounded corners,
  accent/subtle variants, proper hover/press states, and a bottom-edge depth cue
  matching the Windows 11 button style. Replaces all flat-style WinForms buttons.
- **Prominent update section** — dedicated footer bar with version label and
  "Check for Updates" button (replaces the small header version button). Shows
  green "⬇ Download v{x.y.z}" when an update is available, live download progress,
  and version transition text ("v1.4.0 → v1.5.0 available").
- **DWM border color** — sets a subtle light border on the window frame via
  `DWMWA_BORDER_COLOR` on Windows 11 builds.

### Changed
- **Window size increased** — default 540 × 510 → **640 × 660**, minimum
  460 × 450 → **560 × 580**. Fixes buttons being clipped at the bottom of the
  window on standard DPI displays.
- **Header redesigned** — full-width accent-blue bar replaced with a clean
  inline layout: green/grey status dot + "Fast Repeat" title (Segoe UI Variable
  Display 13 pt bold) + accent Disable/Enable toggle button.
- **Color palette refined** — accent blue updated to `#005FB8` (Windows 11
  default), semantic green `#0F7B0F` for enabled state, semantic red `#C42B1C`
  for locked-speed indicator.
- **CaptureDialog modernized** — larger (420 × 200), background matches layer
  color, content panel styled as a card, "Waiting for input…" hint added.
- **Tray icon refined** — uses rounded rectangle background via GraphicsPath
  with antialiasing, updated accent color, and consistent green status dot.

### Fixed
- **Version constant in sync** — `UpdateManager.CurrentVersion` now matches the
  release tag (was stuck at 1.3.0 in the v1.4.0 release).

---

## [1.4.0] — 2026-03-29

### Added
- **Auto-update** — "v1.3.0" button in the header checks GitHub Releases for a newer
  version. First click queries the API and shows either "✓ Latest" (up-to-date) or
  "↓ v1.4.0" (update available). Second click downloads the new EXE with live
  progress ("↓ 47%"), then applies it via a batch-file trampoline in `%TEMP%` that
  replaces the running EXE and restarts the app.
- **Windows 11 UI redesign** — flat header strip (Accent blue) with status label and
  action buttons; section panels with divider lines replacing GroupBox borders;
  hover and press color effects on all buttons; Segoe UI Variable font with
  Segoe UI fallback; DWM rounded corners on Windows 11 (build ≥ 22000).

### Fixed
- **Column headers always visible** — `AutoSizeColumns()` now takes the maximum of
  content width and header text width so "Hold (trigger)", "Repeats (output)", and
  "Mode" columns are never narrower than their heading.

---

## [1.3.0] — 2026-03-30

### Added
- **Single Press mode** — each binding can now be set to either *Repeat* (hold
  trigger to spam output) or *Single Press* (fires output once per trigger press,
  no repeat loop). Useful for toggle actions like Autorun that would stutter-step
  if spammed.
- **Mode column** in the bindings list shows "Repeat" or "Single Press" per row.
- **Set Single Press / Set Repeat** button appears when a binding is selected,
  toggling its mode immediately without re-adding the binding.

---

## [1.2.0] — 2026-03-30

### Added
- **Trigger → Output key mapping** — each binding now has a separate trigger key
  (the key you hold) and an output key (the key that gets injected repeatedly).
  When adding a binding, a two-step dialog first captures the trigger, then asks
  whether to repeat the same key or pick a different output key.
- The bindings list now shows two columns: **Hold (trigger)** and **Repeats (output)**.
- This directly solves hardware macro buttons (e.g. Logitech DPI Shift → F13) that
  generate a virtual key Windows apps ignore: assign F13 as the trigger and Numpad 1
  (or any real game key) as the output.
- Existing single-key bindings continue to work unchanged (output defaults to trigger).

---

## [1.1.0] — 2026-03-30

### Added
- **Scroll wheel tilt support** — Scroll Tilt Left and Scroll Tilt Right are now
  assignable buttons, captured via `WM_MOUSEHWHEEL` in the low-level mouse hook.
  Holding the scroll wheel tilted is detected via a 250 ms debounce timer: the first
  tilt pulse fires "press", each subsequent pulse resets the deadline, and the timer
  expiry fires "release" to stop repeating.
- `MouseBtn.TiltLeft` / `MouseBtn.TiltRight` enum values and friendly display names
  ("Mouse — Scroll Tilt Left / Right").
- Synthetic tilt events use `MOUSEEVENTF_HWHEEL` with ±120 delta (one scroll notch
  per repeat pulse), tagged with `FAST_REPEAT_MARKER` to prevent debounce re-entry.

### Changed
- Button 4 / 5 display names updated to "Back" / "Forward" for clarity.

---

## [1.0.0] — 2026-03-29

### Added
- System-wide low-level keyboard and mouse hooks via `WH_KEYBOARD_LL` / `WH_MOUSE_LL`.
- Support for repeating any keyboard key or mouse button (Left, Right, Middle, X1, X2).
- Configurable repeat speed from **25 ms** to **600 ms** (40 cps → ~1.7 cps).
- **Lock Speed** button to prevent accidental speed changes.
- **Enable / Disable** toggle available both in the main window and the system tray menu.
- Settings persist automatically to `%APPDATA%\FastRepeat\settings.json`.
- System tray icon with live status (active key count, enabled state).
- Single-instance enforcement — launching a second copy shows a tray notification.
- Self-contained single-file `.exe` build via GitHub Actions (no .NET runtime required).
- MIT License.

[1.7.2]: https://github.com/NQV4X0QN/FastRepeat/releases/tag/v1.7.2
[1.7.1]: https://github.com/NQV4X0QN/FastRepeat/releases/tag/v1.7.1
[1.7.0]: https://github.com/NQV4X0QN/FastRepeat/releases/tag/v1.7.0
[1.6.2]: https://github.com/NQV4X0QN/FastRepeat/releases/tag/v1.6.2
[1.6.1]: https://github.com/NQV4X0QN/FastRepeat/releases/tag/v1.6.1
[1.6.0]: https://github.com/NQV4X0QN/FastRepeat/releases/tag/v1.6.0
[1.5.7]: https://github.com/NQV4X0QN/FastRepeat/releases/tag/v1.5.7
[1.5.6]: https://github.com/NQV4X0QN/FastRepeat/releases/tag/v1.5.6
[1.5.5]: https://github.com/NQV4X0QN/FastRepeat/releases/tag/v1.5.5
[1.5.4]: https://github.com/NQV4X0QN/FastRepeat/releases/tag/v1.5.4
[1.5.3]: https://github.com/NQV4X0QN/FastRepeat/releases/tag/v1.5.3
[1.5.2]: https://github.com/NQV4X0QN/FastRepeat/releases/tag/v1.5.2
[1.5.1]: https://github.com/NQV4X0QN/FastRepeat/releases/tag/v1.5.1
[1.5.0]: https://github.com/NQV4X0QN/FastRepeat/releases/tag/v1.5.0
[1.4.0]: https://github.com/NQV4X0QN/FastRepeat/releases/tag/v1.4.0
[1.3.0]: https://github.com/NQV4X0QN/FastRepeat/releases/tag/v1.3.0
[1.2.0]: https://github.com/NQV4X0QN/FastRepeat/releases/tag/v1.2.0
[1.1.0]: https://github.com/NQV4X0QN/FastRepeat/releases/tag/v1.1.0
[1.0.0]: https://github.com/NQV4X0QN/FastRepeat/releases/tag/v1.0.0
