# Changelog

All notable changes to Fast Repeat are documented here.
Format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).
This project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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

[1.0.0]: https://github.com/NQV4X0QN/FastRepeat/releases/tag/v1.0.0
