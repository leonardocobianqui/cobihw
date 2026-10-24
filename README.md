# cobihw

A small, customizable hardware monitor widget for Windows. It shows CPU, GPU, RAM, disk, network, battery and FPS in a borderless window that stays on top of your apps or sits on the desktop.

Free and open source under the [MIT License](LICENSE). Website: [cobihw.com](https://cobihw.com).

## What it shows

| Item | Values | Source |
| --- | --- | --- |
| CPU | Usage and current clock speed | Same counters as Task Manager |
| GPU | Usage, memory and, optionally, temperature | Every GPU in the PC; pick one or let Windows' high-performance choice decide |
| RAM | Usage and used / total | |
| Disk | Active time and, optionally, read / write speed | The Windows disk or one you choose |
| Network | Download / upload speed, data used in the session, ping (optional) | The sum of the physical adapters or one device |
| Battery | Charge, charging state and time left | Only on PCs with a battery |
| FPS | Frames per second of the app in the foreground | Optional; needs administrator permission once |

Hover over the widget to see the details of every item, including the hidden ones. The tray icon tooltip shows a summary.

## Features

- Three layouts: full, compact horizontal and compact vertical (double-click the widget to switch).
- Always on top or on the desktop, under other windows.
- Snap to screen edges, lock the position, and let clicks pass through the widget (hold a key to interact again).
- Resize by dragging the edges; the content zooms instead of cropping.
- Choose and reorder the items; colors change with usage (normal, warning, critical) at thresholds you set.
- Appearance: background color and transparency, text color and weight, rounded corners, bar rounding, and a border.
- Network devices window: IP addresses, gateway, DNS, MAC, link speed and Wi-Fi details for each adapter.
- Internet speed test (Cloudflare), with the last result kept in the details.
- Languages: English, Português (Brasil), Español and Русский. Light, dark or automatic theme for the windows and the tray menu.
- Portable: no installer; the settings file lives next to the executable.

## Download

Get the latest version from [Releases](https://github.com/leonardocobianqui/cobihw/releases). There are two packages for each kind of Windows:

| Package | Windows | Size | Needs |
| --- | --- | --- | --- |
| `cobihw-<version>-portable-win-x64.zip` | 64-bit | Larger | Nothing; .NET is included |
| `cobihw-<version>-framework-dependent-win-x64.zip` | 64-bit | Smaller | [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) (x64) |
| `cobihw-<version>-portable-win-x86.zip` | 32-bit | Larger | Nothing; .NET is included |
| `cobihw-<version>-framework-dependent-win-x86.zip` | 32-bit | Smaller | [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) (x86) |

Not sure which one you have? **Settings → System → About → System type** shows it. Each release also has a `SHA256SUMS.txt` file to check the download.

**Requirements:** Windows 11, or Windows 10 22H2 (64-bit or 32-bit).

## Getting started

1. Extract the zip to a folder you can write to, for example `Documents\cobihw`. The settings are saved in that folder.
2. Run `cobihw.exe`. The app isn't signed yet, so Windows SmartScreen may warn you: choose **More info** and then **Run anyway**.
3. Use the tray icon:
   - **Left click** shows or hides the widget.
   - **Right click** opens the menu: layout, position options, network tools, settings, about and exit.
4. Drag the widget to move it, drag its edges to resize it, and double-click it to change the layout.

### Notes

- **FPS** reads Windows graphics events, which needs administrator permission. The first time you turn it on, Windows asks once; cobihw then creates a scheduled task so it can start with that permission without asking again. **Settings → General → Remove cobihw from Windows** deletes the task and the startup entry.
- **Wi-Fi details** (network name, signal, channel) need the Location permission on recent Windows versions. The network devices window has a button that opens that setting.
- **GPU temperature** appears only when the graphics driver reports it.

## Privacy

cobihw has no telemetry and doesn't collect data. It only goes online when you ask: speed test, public IP, update check, and ping (off by default). See [PRIVACY.md](PRIVACY.md).

## Building from source

Needs Windows and the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0). Visual Studio is optional.

```powershell
git clone https://github.com/leonardocobianqui/cobihw.git
cd cobihw
dotnet build cobihw.slnx
dotnet run --project src/CobiHw.App
```

Run the tests:

```powershell
dotnet test --project tests/CobiHw.Tests/CobiHw.Tests.csproj
```

Create the release packages with PowerShell 7 (zips for x64 and x86, and their checksums, in `publish/<version>`):

```powershell
pwsh ./publish.ps1
```

See [CONTRIBUTING.md](CONTRIBUTING.md) for the project conventions and [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) for how the code is organized.

## License

[MIT](LICENSE) © Leonardo Cobianqui. Third-party components are listed in [THIRD-PARTY-NOTICES.txt](THIRD-PARTY-NOTICES.txt).
