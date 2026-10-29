# Architecture

How cobihw is organized, where each reading comes from and how the pieces fit together. For build and contribution steps, see [CONTRIBUTING.md](../CONTRIBUTING.md).

## Projects

| Project | Type | Role |
| --- | --- | --- |
| `src/CobiHw.Core` | Class library | Reads the hardware. No UI; talks to Windows through P/Invoke, PDH, WMI and ETW. |
| `src/CobiHw.App` | WPF app (`cobihw.exe`) | The widget, tray icon, windows, settings and translations. MVVM with CommunityToolkit.Mvvm. |
| `src/CobiHw.ConsoleTest` | Console app | Prints the Core readings once, to compare them with Task Manager. |
| `tests/CobiHw.Tests` | xUnit v3 tests | Tests both projects; reaches internal types through `InternalsVisibleTo`. |

The App depends on the Core; the Core knows nothing about the App.

## Reading loop

`App` starts two background loops when the app opens:

1. **Sampling, every second.** `HardwareMonitor.Sample()` runs on a thread-pool thread and returns a `HardwareSnapshot`. Back on the UI thread, `MainViewModel.Apply()` turns it into texts and colors, the network devices window gets the new traffic, and the FPS reading is added.
2. **Ping, every 5 seconds**, only while ping is on. A result is dropped if the host changed meanwhile.

`HardwareMonitor` takes one sample when it's created, so the first sample shown already has rates (rates are averaged since the previous sample).

## Data sources

| Reading | Where it comes from | Code |
| --- | --- | --- |
| CPU usage | PDH `\Processor Information(_Total)\% Processor Utility` (the counter Task Manager uses) | `CpuReader` |
| CPU clock | Base clock from `CallNtPowerInformation` × PDH `% Processor Performance` | `CpuReader` |
| RAM | `GlobalMemoryStatusEx` | `MemoryReader` |
| GPU list and order | DXGI adapters, ranked with `IDXGIFactory6::EnumAdapterByGpuPreference` (high performance); Direct3D 12 tells integrated GPUs apart | `GpuAdapter` |
| GPU usage | PDH `\GPU Engine(*)\Utilization Percentage`: processes are summed per engine and the busiest engine wins | `GpuReader` |
| GPU memory | PDH `\GPU Adapter Memory(*)\Dedicated Usage` and `Shared Usage` | `GpuReader` |
| GPU temperature | `D3DKMTQueryAdapterInfo` (performance data reported by the display driver) | `GpuSensor` |
| Disk | PDH `\PhysicalDisk(*)\% Idle Time`, `Disk Read Bytes/sec`, `Disk Write Bytes/sec`; the Windows disk is the one that holds the system drive | `DiskReader` |
| Network traffic | `GetIfTable2` (IP Helper) octet counters per interface | `NetworkReader` |
| Network device details | `GetIfTable2` for the list and type, .NET `NetworkInterface` for addresses, gateway and DNS, WMI `MSFT_NetAdapter` for which adapters Windows hides | `NetworkDevices`, `NetAdapterQuery` |
| Wi-Fi details | Native Wi-Fi API (`wlanapi.dll`) | `WifiReader` |
| Battery | `GetSystemPowerStatus` | `BatteryReader` |
| FPS | ETW "Present" events from `Microsoft-Windows-DXGI` and `Microsoft-Windows-D3D9`, like PresentMon | `FpsMonitor`, `FrameCounter` |
| Ping | `System.Net.NetworkInformation.Ping` | `LatencyProbe` |
| Speed test | Cloudflare's `speed.cloudflare.com` download and upload endpoints | `CloudflareSpeedTest` |
| Public IP | Cloudflare's `/cdn-cgi/trace` | `PublicAddress` |

All PDH counters live in one `PdhQuery` collected once per sample, so every reading in a snapshot covers the same second.

## FPS and administrator permission

Real-time ETW sessions for the graphics providers need administrator rights, but the widget runs without them (`asInvoker` in `app.manifest`). The bridge is a scheduled task (`ElevatedTask`):

1. When the user turns FPS on, the app starts a copy of itself through a UAC prompt with `--register-task`. That copy only creates the task and exits.
2. The task runs `cobihw.exe --settings "<file>"` with the highest privileges, for the logged-on user. It's named after the user's SID, so accounts on the same PC don't overwrite each other's task.
3. From then on, a normal start with FPS on runs the task (no prompt) and exits; the elevated copy takes over.

The task is tied to the `.exe` path and the settings file. If the app is moved, it's registered again. Turning FPS off, or **Remove cobihw from Windows**, deletes it.

## Settings

Settings are saved in `cobihw.settings.json`, next to the executable (`SettingsStore`), so the app stays portable.

- **Profiles:** one file holds one profile per computer and Windows user. A new PC copies only the personal choices (look, items, language), not what depends on the hardware (position, disk, GPU, network devices).
- **Safe writes:** a `.lock` file serializes writes between instances. Each write goes to a `.tmp` file that then replaces the real one, so a reader never sees half a file.
- **Busy or broken files:** if another process holds the file, the app runs with defaults in read-only mode and relaunches once the file can be read. A corrupt file is kept as `.invalid`.
- **Old files:** settings files from older versions are read and migrated.

The app also saves every minute and on exit; saving is skipped when nothing changed.

## Widget window

`MainWindow` is a borderless, transparent WPF window sized to its content.

- **Layouts:** the three layouts are `ItemsControl`s over the same item list; `MetricTemplateConverter` picks the template of each item.
- **Zoom:** resizing by the edges changes a zoom factor (`LayoutTransform`), not the window size. `ResizeRule` does the math.
- **Position:** stored in physical pixels, which stay exact across monitors with different DPI (the app is per-monitor DPI aware, v2). `WindowFit` keeps the window inside the work area, snaps it to edges, moves it between monitors and brings it back when a monitor is removed.
- **Window messages:** hooks on `WM_WINDOWPOSCHANGING`, `WM_MOVING` and `WM_DISPLAYCHANGE` keep the window on screen, snap it and keep it under other windows in desktop mode.
- **Click-through:** sets `WS_EX_TRANSPARENT`; holding the interaction key turns it off while held.
- **Stable width:** the speed values reserve the width of their widest possible text, so the widget doesn't resize every second. Numbers use three significant digits (`DisplayFormat`).

## Look

The app windows and the tray menu follow the cobihw design system, the same on Windows 10 and 11; the widget doesn't, since its colors come from the Appearance tab.

- `Themes/Dark.xaml` and `Themes/Light.xaml` hold the colors under the same keys; `Themes/Controls.xaml` holds the control styles, which read the colors with `DynamicResource`.
- `WindowTheme.Apply` merges the colors and the styles into each window's resources, so the widget window is left alone. Changing the theme, or Windows switching light/dark in automatic mode, swaps only the color dictionary (`WindowTheme.ApplyAll`).
- Windows keep the system title bar for now, made dark with the dark theme (`DWMWA_USE_IMMERSIVE_DARK_MODE`).

## Tray icon

The tray icon is a Windows Forms `NotifyIcon`; its menu is a WPF `ContextMenu` with the styles above. The menu's popup takes the foreground when it opens, otherwise it wouldn't close on a click elsewhere. The tooltip is a fixed summary (CPU, GPU, RAM, disk, network and battery), whatever the widget shows. Windows limits it to 127 characters: every line fits in every language, and `TrayTooltip` still drops whole lines from the end if a text ever grows.

## Speed test

`CloudflareSpeedTest` runs against Cloudflare's speed test endpoints, one phase after the other:

- **Latency:** median time to first byte of 20 empty downloads on an open connection, minus the server time. The server time is read from `Server-Timing` as Cloudflare's own test reads it: `cfRequestDuration` when present, otherwise the sum of the `cfSpeed…` parts.
- **Download and upload:** like Ookla's Speedtest, each phase runs 4 connections at once for 10 seconds, with back-to-back requests of up to 50 MB (the server refuses 100 MB or more). Bytes count as they move. When the phase ends, downloads are cut; uploads are sent in chunks (no `Content-Length`), so they just stop sending and finish as complete requests. The first 2 seconds, while TCP speeds up, are left out; the result is the average speed of the rest. The live speed is the traffic of the last second.

The last progress update of each phase is that phase's result, so the window fills each card as soon as its phase ends.

Starting a test anywhere (widget, tray or settings) opens `SpeedTestWindow`. Closing it doesn't stop the test: the tray item shows it again, "Cancel test" stops it, and a notification brings the result when the window is closed. `SpeedGauge` draws the needle on the `SpeedGaugeScale`, whose marks (0 to 1000 Mbps) are evenly spaced like Ookla's Speedtest.

## 64-bit and 32-bit

The x64 and x86 packages come from the same code; `publish.ps1` only changes the runtime (`-r win-x86`).

- Windows structures use `nint`/`nuint` where the SDK has pointers, handles or `SIZE_T`, so they shrink in a 32-bit process. `InteropLayoutTests` and `User32Tests` check each size against the SDK headers for both architectures, and the build workflow also runs the tests in a 32-bit process.
- `User32.GetWindowLongPtr` and `SetWindowLongPtr` call `GetWindowLongW` and `SetWindowLongW` in x86, where the `...Ptr` entry points don't exist.
- The update check only offers a release with a package for the architecture of the running app (`UpdateChecker.IsUpdate`).

## Localization

- English texts are in `Resources/Strings.resx`; Portuguese, Spanish and Russian in `Strings.pt.resx`, `Strings.es.resx` and `Strings.ru.resx`. Each English entry has a comment for translators.
- XAML uses `{loc:Text Key}` (`TextExtension`), which binds to `Localizer`. Changing the language in the settings updates every open window at once.
- Code reads texts through the generated `Strings` class and formats numbers with `Localizer.Format`, so decimals follow the app language, not the Windows one.
- Tests check that every language has the same keys and placeholders as English, and that every key used in XAML exists.

## Folder map

```
src/CobiHw.Core/
  Readers/      CPU, RAM, GPU, disk, network and battery readers
  Network/      Network devices, Wi-Fi, ping, speed test, public IP
  Fps/          ETW session and frame counting
  Interop/      Windows API declarations
src/CobiHw.App/
  Settings/     Settings data, file store, settings and appearance windows
  Devices/      Network devices window
  About/        About window and update check
  Themes/       Colors and control styles of the windows and the tray menu
  Tray/         Tray icon, menu and tooltip
  Localization/ Language switching and the XAML text extension
  Resources/    Translations
  Converters/   XAML converters
  Interop/      Windows API declarations used by the App
tests/CobiHw.Tests/
  Core/, App/   Tests by area
  Fakes/        Simulated speed test server
  Support/      Shared test data and helpers
```
