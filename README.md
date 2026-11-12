# SpkDisplayController

<div align="center">

<img src="docs/icon.png" width="112" alt="SpkDisplayController" />

**Control HDR, brightness and power state for each monitor from the Windows system tray.**

[![CI](https://github.com/xapenny/SpkDisplayController/actions/workflows/ci.yml/badge.svg)](https://github.com/xapenny/SpkDisplayController/actions/workflows/ci.yml)
[![Release](https://img.shields.io/github/v/release/xapenny/SpkDisplayController?display_name=tag)](https://github.com/xapenny/SpkDisplayController/releases)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/license-MIT-blue)](LICENSE)

English · [简体中文](README.zh-CN.md)

</div>

---

SpkDisplayController is a Windows tray application for managing individual monitors. It uses DDC/CI and the Windows DisplayConfig API to adjust display settings without having to navigate through Windows Settings.

## Features

- **HDR** – Enable or disable HDR independently for each monitor.
- **Brightness** – Adjust monitor brightness through DDC/CI.
- **Soft off** – Send a display power command without disconnecting the monitor from Windows.
- **Multi-monitor support** – Each monitor can be controlled independently.
- **System tray** – Runs in the notification area without a main window.
- **Startup** – Launch automatically when you sign in to Windows.
- **Localization** – English, Simplified Chinese, Japanese, Korean and Russian.

## How it works

The application uses two Windows interfaces to control monitors:

| Operation | Interface |
| --- | --- |
| Monitor enumeration | EnumDisplayDevices / DXVA2 |
| Brightness | DDC/CI, VCP `0x10` |
| Power state | DDC/CI, VCP `0xD6` |
| HDR | DisplayConfig Advanced Color |
| Tray menu | Win32 HMENU |

Soft off uses DDC/CI `VCP 0xD6`, setting the power mode to `4`. Unlike disconnecting a display in Windows, this normally keeps the display connected from the operating system's perspective, preserving the desktop layout and window positions.

## Requirements

- Windows 10 / 11 (x64)
- [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0)
- DDC/CI support enabled in the monitor's OSD

## Download and run

Download the latest version from [GitHub Releases](https://github.com/xapenny/SpkDisplayController/releases).

Run `SpkDisplayController.exe` and approve the UAC prompt. The application will appear in the system tray. Left-click or right-click the tray icon to open the menu.

## Build

```powershell
git clone https://github.com/xapenny/SpkDisplayController.git
cd SpkDisplayController

dotnet build -c Release

# Single-file, framework-dependent publish
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o dist
```

The executable is generated at:

```text
dist/SpkDisplayController.exe
```

The current build is approximately 450 KB, including the application icon. The .NET Desktop Runtime must be installed separately.

## Command-line options

| Option | Description |
| --- | --- |
| `--selftest` | Enumerate monitors, write their state to `SpkDisplayController-selftest.txt`, then exit |
| `--dumpmenu` | Write the menu structure to `SpkDisplayController-menu.txt`, then exit |
| `--simulate-softoff` | Used with `--dumpmenu` to simulate all monitors being in soft-off state without sending DDC commands |
| `--lang=xx` | Override the UI language: `en`, `zh-Hans`, `ja`, `ko`, `ru` |

Examples:

```powershell
.\SpkDisplayController.exe --selftest

.\SpkDisplayController.exe --dumpmenu --lang=ja

.\SpkDisplayController.exe --dumpmenu --simulate-softoff --lang=zh-Hans
```

## Permissions and startup

The application manifest specifies `requireAdministrator`, so administrator privileges are required.

On some GPU drivers, DDC/CI `SetVCPFeature` and DisplayConfig `SetAdvancedColorState` calls may fail silently when running under a standard user token.

### Startup configuration

The application uses Windows Task Scheduler instead of the registry `Run` key.

A process launched through `HKCU\...\Run` inherits the standard user token, which would trigger a UAC prompt on every login. Instead, the application registers a scheduled task with the highest available privileges:

```powershell
schtasks /Create /TN "SpkDisplayController" /TR "\"<exe path>\"" /SC ONLOGON /RL HIGHEST /F
```

Task Scheduler launches the application with its elevated token when the user signs in, avoiding another UAC prompt.

## Technical details

### DDC/CI

DDC/CI communication is handled through `dxva2.dll`:

- `GetPhysicalMonitorsFromHMONITOR`
- `GetVCPFeatureAndVCPFeatureReply`
- `SetVCPFeature`

The application uses the following VCP codes:

| VCP | Function | Values |
| --- | --- | --- |
| `0x10` | Brightness | Monitor-supported range |
| `0xD6` | Power mode | `1`: On, `4`: Soft Off |

DXVA2 physical monitor handles require special handling: a handle with the value `0` can be valid. Checking validity with `IntPtr.Zero` would incorrectly discard such handles and skip DDC operations for the corresponding monitor.

The application uses a separate `HasDdcHandle` flag to track whether a handle was successfully obtained.

### HDR

HDR state is managed through the DisplayConfig Advanced Color interface:

- `DISPLAYCONFIG_DEVICE_INFO_GET_ADVANCED_COLOR_INFO` (Type 9)
- `DISPLAYCONFIG_DEVICE_INFO_SET_ADVANCED_COLOR_STATE` (Type 10)

When querying display paths, the application tries the following flags in order:

1. `QDC_ONLY_ACTIVE_PATHS`
2. `QDC_DATABASE_CURRENT`
3. `QDC_ALL_PATHS`

Some GPU drivers return `ERROR_INVALID_PARAMETER (87)` when queried with `QDC_ONLY_ACTIVE_PATHS`, so the other modes are used as fallbacks.

### Monitor mapping

DDC/CI enumeration returns device names such as `\\.\DISPLAYn`, while DisplayConfig uses its own target identifiers.

The application maps them using `DISPLAYCONFIG_DEVICE_INFO_GET_SOURCE_NAME` (Type 1). Its `viewGdiDeviceName` field provides the corresponding `\\.\DISPLAYn` name.

`EnumDisplayDevices` returns a `DeviceID` in a different format, such as `MONITOR\PHLC44F\{GUID}\0142`, whereas DisplayConfig paths look like `DISPLAY#PHLC44F#...`. These identifiers cannot be matched directly.

### Native menu

The tray menu is implemented using Win32 `HMENU` and `TrackPopupMenu` rather than WinForms `ContextMenuStrip`.

An invisible `WS_POPUP | WS_EX_TOOLWINDOW` window acts as the menu owner. Before displaying the menu, the application calls `SetForegroundWindow` to ensure it closes correctly when clicking outside it.

After the menu closes, `PostMessage(owner, WM_NULL)` is used to keep the tray icon responsive.

The menu is rebuilt whenever it opens to reflect the current monitor state.

## Known limitations

- **Soft-off recovery:** On certain monitor, sending `VCP 0xD6 = 4` leaves the DDC channel readable and `WmiMonitorID.Active` remains `True`. However, subsequent `VCP 0xD6 = 1` and `VCP 0x10` commands do not wake the panel. The monitor must be turned back on using its physical power button. Behavior may vary between monitors and connection types.
- **DDC/CI support:** DDC/CI must be enabled in the monitor's OSD. Otherwise, `SetVCPFeature` may fail with `0xC00D3A02` (I2C transfer failure), and the corresponding menu item will be marked unavailable.
- **Brightness presets:** The brightness menu provides fixed levels of 1%, 25%, 50%, 75% and 100%. If the current brightness does not match any of these values, no preset is checked.

## Project structure

```text
SpkDisplayController/
├── SpkDisplayController.csproj
├── app.manifest
├── Program.cs
├── TrayAppContext.cs
├── Models/
│   └── MonitorDevice.cs
├── Services/
│   └── MonitorService.cs
├── Native/
│   ├── Ddc.cs
│   ├── DisplayConfig.cs
│   ├── NativeMenu.cs
│   └── AutoStart.cs
├── Localization/
│   └── Loc.cs
├── UI/
│   └── AboutForm.cs
├── Resources/
│   └── app.ico
└── docs/
    └── icon.png
```

## License

MIT License. See [LICENSE](LICENSE) for details.
