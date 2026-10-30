# cobihw

A small, customizable hardware monitor widget for Windows. CPU, GPU, RAM, disk, network, battery and FPS in a borderless window that floats above your apps or sits on the desktop.

Free and open source under the [MIT License](LICENSE). Website: [cobihw.com](https://cobihw.com).

## What it shows

| Item | Values | Source |
| --- | --- | --- |
| CPU | Usage and current clock | The same counters as Task Manager |
| GPU | Usage, memory and optional temperature | Any GPU in the system; pick one or follow Windows' high-performance choice |
| RAM | Usage and used / total | |
| Disk | Active time and optional read / write speed | The Windows disk or one you choose |
| Network | Download / upload speed, session data, optional ping | All physical adapters combined, or a single device |
| Battery | Charge, charging state and time left | PCs with a battery |
| FPS | Frame rate of the foreground app | Optional; asks for administrator permission once |

Hover over the widget for details on every item, hidden ones included. The tray icon tooltip shows a summary.

## Features

- Three layouts: full, compact horizontal and compact vertical. Double-click the widget to switch.
- Always on top, or on the desktop below other windows.
- Edge snapping, position lock, and click-through (hold a key to interact again).
- Drag the edges to resize; the content scales instead of getting cropped.
- Pick and reorder items, with usage colors (normal, warning, critical) at thresholds you set.
- Appearance: background color and transparency, text color and weight, corner and bar rounding, border.
- Network devices window: IP addresses, gateway, DNS, MAC, link speed and Wi-Fi details per adapter.
- Internet speed test (Cloudflare) with a gauge, measuring ping, download and upload in turn. The last result stays in the details.
- English, Português (Brasil), Español and Русский. Light, dark or automatic theme for the windows and the tray menu.
- Portable: no installer, and the settings file lives next to the executable.

## Download

Grab the latest version from [Releases](https://github.com/leonardocobianqui/cobihw/releases). Each architecture has two packages:

| Package | Windows | Size | Requires |
| --- | --- | --- | --- |
| `cobihw-<version>-portable-win-x64.zip` | 64-bit | Larger | Nothing; .NET is bundled |
| `cobihw-<version>-framework-dependent-win-x64.zip` | 64-bit | Smaller | [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) (x64) |
| `cobihw-<version>-portable-win-x86.zip` | 32-bit | Larger | Nothing; .NET is bundled |
| `cobihw-<version>-framework-dependent-win-x86.zip` | 32-bit | Smaller | [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) (x86) |

Not sure which Windows you have? Check **Settings → System → About → System type**. Every release includes a `SHA256SUMS.txt` to verify the download.

**Requirements:** Windows 11, or Windows 10 22H2 (64-bit or 32-bit).

## Getting started

1. Extract the zip to a folder you can write to, such as `Documents\cobihw`. Settings are saved there.
2. Run `cobihw.exe`. The app isn't code-signed yet, so SmartScreen may warn you: choose **More info**, then **Run anyway**.
3. Use the tray icon:
   - **Left click** shows or hides the widget.
   - **Right click** opens the menu: layout, positioning, network tools, settings, about and exit.
4. Drag the widget to move it, drag its edges to resize it, double-click it to change the layout.

### Notes

- **FPS** relies on Windows graphics events, which require administrator rights. The first time you enable it, Windows asks once; cobihw then registers a scheduled task so later starts don't prompt again. **Settings → General → Remove cobihw from Windows** removes the task and the startup entry.
- **Wi-Fi details** (network name, signal, channel) need the Location permission on recent Windows versions. The network devices window links straight to that setting.
- **GPU temperature** shows up only when the graphics driver reports it.

## Privacy

No telemetry, no data collection. cobihw only goes online when you ask it to: speed test, public IP, update check, and ping (off by default). Details in [PRIVACY.md](PRIVACY.md).

## Building from source

Requires Windows and the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0). Visual Studio is optional.

```powershell
git clone https://github.com/leonardocobianqui/cobihw.git
cd cobihw
dotnet build cobihw.slnx
dotnet run --project src/CobiHw.App
```

Tests:

```powershell
dotnet test --project tests/CobiHw.Tests/CobiHw.Tests.csproj
```

Release packages (PowerShell 7; x64 and x86 zips plus checksums in `publish/<version>`):

```powershell
pwsh ./publish.ps1
```

Conventions are in [CONTRIBUTING.md](CONTRIBUTING.md); the code layout is described in [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md).

## License

[MIT](LICENSE) © Leonardo Cobianqui. Third-party components are listed in [THIRD-PARTY-NOTICES.txt](THIRD-PARTY-NOTICES.txt).
