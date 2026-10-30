# Architecture

An overview of how cobihw is put together: the projects, where each reading comes from, and the non-obvious decisions behind the UI. Build and contribution steps live in [CONTRIBUTING.md](../CONTRIBUTING.md).

## Projects

| Project | Type | Role |
| --- | --- | --- |
| `src/CobiHw.Core` | Class library | Hardware readers. No UI; P/Invoke, PDH, WMI and ETW. |
| `src/CobiHw.App` | WPF app (`cobihw.exe`) | Widget, tray icon, windows, settings and translations. MVVM via CommunityToolkit.Mvvm. |
| `src/CobiHw.ConsoleTest` | Console app | Dumps one set of Core readings, handy for comparing against Task Manager. |
| `tests/CobiHw.Tests` | xUnit v3 | Covers both projects; internals are exposed through `InternalsVisibleTo`. |

Dependencies point one way: App → Core.

## Sampling

`App` runs two background loops:

1. **Hardware, every second.** `HardwareMonitor.Sample()` runs on the thread pool and returns a `HardwareSnapshot`; `MainViewModel.Apply()` formats it on the UI thread. The same tick feeds the network devices window and the FPS reading.
2. **Ping, every 5 seconds**, only when enabled. Results for a host that changed in the meantime are discarded.

`HardwareMonitor` primes itself with one sample on construction, so the first snapshot already has rates.

## Data sources

| Reading | Source | Code |
| --- | --- | --- |
| CPU usage | PDH `\Processor Information(_Total)\% Processor Utility`, the counter Task Manager uses | `CpuReader` |
| CPU clock | Base clock (`CallNtPowerInformation`) × PDH `% Processor Performance` | `CpuReader` |
| RAM | `GlobalMemoryStatusEx` | `MemoryReader` |
| GPU list and order | DXGI, ranked by `IDXGIFactory6::EnumAdapterByGpuPreference` (high performance); Direct3D 12 flags integrated GPUs | `GpuAdapter` |
| GPU usage | PDH `\GPU Engine(*)\Utilization Percentage`, summed per engine across processes; the busiest engine wins | `GpuReader` |
| GPU memory | PDH `\GPU Adapter Memory(*)\Dedicated Usage` / `Shared Usage` | `GpuReader` |
| GPU temperature | `D3DKMTQueryAdapterInfo`, when the driver reports it | `GpuSensor` |
| Disk | PDH `\PhysicalDisk(*)\% Idle Time`, `Disk Read Bytes/sec`, `Disk Write Bytes/sec`; "the Windows disk" is the one holding the system drive | `DiskReader` |
| Network traffic | `GetIfTable2` octet counters | `NetworkReader` |
| Network device details | `GetIfTable2` (list and type), `NetworkInterface` (addresses, gateway, DNS), WMI `MSFT_NetAdapter` (adapters Windows hides) | `NetworkDevices`, `NetAdapterQuery` |
| Wi-Fi | Native Wi-Fi API (`wlanapi.dll`) | `WifiReader` |
| Battery | `GetSystemPowerStatus` | `BatteryReader` |
| FPS | ETW present events from `Microsoft-Windows-DXGI` and `Microsoft-Windows-D3D9`, as PresentMon does | `FpsMonitor`, `FrameCounter` |
| Ping | `System.Net.NetworkInformation.Ping` | `LatencyProbe` |
| Speed test | `speed.cloudflare.com` download/upload endpoints | `CloudflareSpeedTest` |
| Public IP | Cloudflare `/cdn-cgi/trace` | `PublicAddress` |

All PDH counters share one `PdhQuery`, collected once per tick, so every value in a snapshot covers the same interval.

## FPS and elevation

Real-time ETW sessions on the graphics providers require admin rights; the widget itself runs `asInvoker`. A scheduled task (`ElevatedTask`) bridges the two:

1. Enabling FPS relaunches the app through UAC with `--register-task`. That instance creates the task and exits.
2. The task runs `cobihw.exe --settings "<file>"` with highest privileges for the logged-on user. It's named after the user's SID, so multiple accounts on one PC don't collide.
3. From then on, a normal start with FPS enabled triggers the task and exits; the elevated instance takes over. No further prompts.

The task is bound to the executable path and settings file and is re-registered if the app moves. Disabling FPS or **Remove cobihw from Windows** deletes it.

## Settings

`SettingsStore` keeps `cobihw.settings.json` next to the executable; the app stays portable.

- **Profiles:** one profile per computer and Windows user. A new machine inherits the personal choices (look, items, language) but not the hardware-bound ones (position, disk, GPU, network devices).
- **Atomic writes:** a `.lock` file serializes writers across instances; data goes to a `.tmp` file that then replaces the original.
- **Contention and corruption:** if another process holds the file, the app starts read-only with defaults and relaunches once it can read it. A corrupt file is set aside as `.invalid`.
- **Migration:** files from older versions are upgraded on load.

Settings are also flushed every minute and on exit, skipping unchanged state.

## Widget window

`MainWindow` is borderless, transparent and sized to content.

- **Layouts:** three `ItemsControl`s over one item list; `MetricTemplateConverter` picks the template per item.
- **Zoom:** dragging the edges changes a `LayoutTransform` scale rather than the window size (`ResizeRule`).
- **Position:** persisted in physical pixels, which survive mixed-DPI setups (per-monitor DPI aware v2). `WindowFit` handles work-area clamping, edge snapping, monitor moves and monitor removal.
- **Window messages:** `WM_WINDOWPOSCHANGING`, `WM_MOVING` and `WM_DISPLAYCHANGE` hooks keep it on screen, snap it, and pin it to the bottom of the z-order in desktop mode.
- **Click-through:** `WS_EX_TRANSPARENT`, lifted while the interaction key is held.
- **Stable width:** speed fields reserve the width of their widest possible text, so the widget doesn't reflow every second. Values use three significant digits (`DisplayFormat`).

## Look

The app windows and the tray menu share one design system on Windows 10 and 11. The widget is excluded; its colors come from the Appearance tab.

- `Themes/Dark.xaml` and `Themes/Light.xaml` define the same color keys; `Themes/Controls.xaml` holds the control styles and reads colors through `DynamicResource`.
- `WindowTheme.Apply` merges both into each window's resources instead of `Application.Resources`, which keeps the widget out of it. A theme change (or Windows flipping light/dark in automatic mode) swaps only the color dictionary (`WindowTheme.ApplyAll`).
- `WindowFrame` provides the title bar, 12 px corners and shadow. Windows are borderless and transparent (`AllowsTransparency`), so `WM_NCHITTEST` is answered by hand: the empty title bar is `HTCAPTION` (drag, snap, double-click to maximize), a thin band around the visible edge resizes, and the maximize button reports `HTMAXBUTTON` so Windows 11 shows snap layouts. Hover and clicks on that button then arrive as non-client messages, which `WindowFrame` maps back to the button state. `WM_GETMINMAXINFO` constrains the maximized window to the monitor's work area.
- The shadow lives in a 16 px transparent margin (`WindowFrame.ShadowSize`), so XAML sizes include it. It's drawn on a separate layer behind the content; applying the effect to the content would turn off ClearType. `RenderOptions.ClearTypeHint` keeps ClearType on inside the layered window.
- `MessageWindow` replaces `MessageBox` with the same buttons and results, but in the app's theme and language (`MessageBox` labels its buttons in the OS language). It keeps the expected behavior: default and cancel buttons, system sounds, Ctrl+C to copy. Ownerless messages are topmost so the widget can't cover them. `MessageBox` remains only as the error window's last-resort fallback.

## Tray icon

The icon is a WinForms `NotifyIcon`; the menu is a WPF `ContextMenu` using the styles above. The popup grabs the foreground when it opens, otherwise it wouldn't dismiss on an outside click. The tooltip is a fixed summary (CPU, GPU, RAM, disk, network, battery), independent of what the widget shows. Windows caps it at 127 characters; every line fits in every language, and `TrayTooltip` drops whole lines from the end as a safeguard.

## Speed test

`CloudflareSpeedTest` runs the phases sequentially:

- **Latency:** median time to first byte over 20 empty downloads on a warm connection, minus server time. Server time comes from `Server-Timing`, parsed the way Cloudflare's own client does: `cfRequestDuration` if present, otherwise the sum of the `cfSpeed…` entries.
- **Download / upload:** Ookla-style, 4 parallel connections for 10 seconds, with back-to-back requests of up to 50 MB (the server rejects 100 MB and above). Bytes are counted as they move. At the end of a phase, downloads are aborted; uploads use chunked transfer (no `Content-Length`), so they simply stop sending and complete cleanly. The first 2 seconds are discarded as TCP ramp-up, and the result is the average over the rest. The live value is the last second of traffic.

Each phase's final progress report carries its result, so the window can fill in a card as soon as a phase ends.

Starting a test from anywhere (widget, tray, settings) opens `SpeedTestWindow`. Closing the window doesn't cancel the test: the tray item reopens it, "Cancel test" stops it, and a notification reports the result if the window is closed. `SpeedGauge` draws on `SpeedGaugeScale`, whose 0–1000 Mbps marks are evenly spaced, as in Ookla's Speedtest.

## x64 and x86

Both packages build from the same source; `publish.ps1` only switches the runtime identifier (`-r win-x86`).

- Interop structs use `nint`/`nuint` wherever the SDK has pointers, handles or `SIZE_T`. `InteropLayoutTests` and `User32Tests` assert their sizes against the SDK headers for both architectures, and CI runs the suite in a 32-bit process as well.
- `User32.GetWindowLongPtr` / `SetWindowLongPtr` fall back to `GetWindowLongW` / `SetWindowLongW` on x86, where the `…Ptr` exports don't exist.
- The update check only offers releases that ship a package for the running architecture (`UpdateChecker.IsUpdate`).

## Localization

- English lives in `Resources/Strings.resx`, with a translator comment per entry; Portuguese, Spanish and Russian in `Strings.pt.resx`, `Strings.es.resx` and `Strings.ru.resx`.
- XAML uses `{loc:Text Key}` (`TextExtension`), bound to `Localizer`, so a language change updates every open window immediately.
- Code goes through the generated `Strings` class and formats numbers with `Localizer.Format`, so decimal separators follow the app language rather than the OS.
- Tests enforce matching keys and placeholders across languages and verify that every key referenced from XAML exists.

## Folder map

```
src/CobiHw.Core/
  Readers/      CPU, RAM, GPU, disk, network and battery
  Network/      Network devices, Wi-Fi, ping, speed test, public IP
  Fps/          ETW session and frame counting
  Interop/      Windows API declarations
src/CobiHw.App/
  Settings/     Settings model, file store, settings and appearance windows
  Devices/      Network devices window
  About/        About window and update check
  Themes/       Colors and control styles
  Tray/         Tray icon, menu and tooltip
  Localization/ Language switching and the XAML text extension
  Resources/    Translations
  Converters/   XAML converters
  Interop/      App-side Windows API declarations
tests/CobiHw.Tests/
  Core/, App/   Tests by area
  Fakes/        Simulated speed test server
  Support/      Shared fixtures and helpers
```
