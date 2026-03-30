# Changelog

All notable changes to Fast Repeat are documented here.
Format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).
This project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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
