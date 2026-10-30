# Changelog

All notable changes to cobihw are documented here.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [1.2.0] - 2026-10-30

### Added

- Speed test window with a live gauge: ping, download and upload run in turn, with jitter. Closing the window doesn't cancel the test; a notification reports the result.
- Network label in the compact layouts.
- Halo behind the widget's text and bars, keeping them readable over transparent backgrounds.

### Changed

- Speed test methodology aligned with Ookla's: 4 connections, 10 s per phase, first 2 s discarded. Ping excludes Cloudflare's server time, as Cloudflare's own client does.
- Tray tooltip is now a fixed summary: CPU, GPU, RAM, disk, network and battery.
- Redesigned app windows and tray menu, identical on Windows 10 and 11 in light and dark: custom title bar with rounded corners and snap layouts, and a footer bar for actions.
- Messages follow the app's theme and language; their buttons used to follow the Windows language.

### Fixed

- Exiting from the tray while the settings window was asking about unapplied changes crashed the app.

## [1.1.0] - 2026-10-24

### Added

- Portable and framework-dependent packages for 32-bit Windows 10 (x86).
- Error reports show the architecture of the app next to the architecture of Windows.

### Changed

- New app icon, the same as the website's.
- The update check only offers a new version when it has a package for the architecture of the running app.

## [1.0.0] - 2026-10-22

First public release.

### Added

- Widget with CPU (usage and clock), GPU (usage, memory and optional temperature), RAM, disk (active time and optional read/write speed), network (speed, session data and optional ping), battery and FPS.
- Choice of GPU, ranked like Windows' high-performance preference, with integrated GPUs detected.
- Choice of disk (the Windows disk or a specific one) and of network device (sum of the physical adapters or one device).
- Details on mouse hover, including hidden items, and a summary in the tray icon tooltip.
- Three layouts: full, compact horizontal and compact vertical; double-click switches between them.
- Always on top or on the desktop; snap to screen edges; lock position; click-through with a hold-to-interact key; move to another monitor.
- Resize by dragging the edges (zoom from 50% to 200%).
- Item order and visibility; usage colors with custom thresholds.
- Appearance: background color and transparency, text color and weight, rounded corners, bar rounding and border.
- Network devices window with addresses, gateway, DNS, MAC, link speed, Wi-Fi details and public IP lookup.
- Internet speed test using Cloudflare servers.
- About window with an on-demand update check.
- English, Portuguese (Brazil), Spanish and Russian; light, dark or automatic theme.
- Portable settings file next to the executable, with one profile per computer and Windows user.
- Start with Windows, and an option to remove everything the app registered in Windows.
- Portable and framework-dependent packages for Windows x64.

[Unreleased]: https://github.com/leonardocobianqui/cobihw/compare/v1.2.0...HEAD
[1.2.0]: https://github.com/leonardocobianqui/cobihw/compare/v1.1.0...v1.2.0
[1.1.0]: https://github.com/leonardocobianqui/cobihw/compare/v1.0.0...v1.1.0
[1.0.0]: https://github.com/leonardocobianqui/cobihw/releases/tag/v1.0.0
