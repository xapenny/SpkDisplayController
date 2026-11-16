# SpkDisplayController

<div align="center">

<img src="docs/icon.png" width="112" alt="SpkDisplayController" />

**Control HDR, brightness, resolution, refresh rate, orientation and power state for each monitor from the Windows system tray.**

[![CI](https://github.com/xapenny/SpkDisplayController/actions/workflows/ci.yml/badge.svg)](https://github.com/xapenny/SpkDisplayController/actions/workflows/ci.yml)
[![Release](https://img.shields.io/github/v/release/xapenny/SpkDisplayController?display_name=tag)](https://github.com/xapenny/SpkDisplayController/releases)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/license-MIT-blue)](LICENSE)

English · [简体中文](README.zh-CN.md)

</div>

---

SpkDisplayController is a lightweight Windows system tray utility for managing individual monitors without opening Windows Settings.

## Features

- **HDR** – Toggle HDR independently for each monitor.
- **Brightness** – Quick presets (`1%`, `25%`, `50%`, `75%`, `100%`) via DDC/CI.
- **Resolution & Refresh rate** – Switch per-monitor resolution and refresh rate.
- **Orientation** – Rotate each monitor between Landscape, Portrait, Landscape (flipped) and Portrait (flipped).
- **Soft off** – Power off a monitor via DDC/CI (`VCP 0xD6 = 4`) while keeping it connected in Windows, preserving your desktop layout and window positions.
- **Startup** – Optional launch at Windows sign-in without repeated UAC prompts.
- **Localization** – English, Simplified Chinese, Japanese, Korean and Russian.

## Requirements

- Windows 10 / 11 (x64)
- [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0)
- DDC/CI enabled in the monitor's OSD (required for brightness and soft off)

## Download and run

Download the latest release from [GitHub Releases](https://github.com/xapenny/SpkDisplayController/releases).

Run `SpkDisplayController.exe` and approve the UAC prompt. Left-click or right-click the system tray icon to open the menu.

### Permissions and startup

Administrator privileges are required because some GPU drivers reject or silently ignore DDC/CI and HDR changes from standard user processes.

When **Start with Windows** is enabled, the app registers a Task Scheduler task (`SpkDisplayController`) so Windows launches it elevated at sign-in without showing a UAC prompt.

## Known limitations

- **Soft-off recovery:** On some monitors, soft off (`VCP 0xD6 = 4`) puts the panel into a deep standby state that cannot be woken by mouse/keyboard input or DDC wake commands—you must turn it back on using the monitor's physical power button.
- **DDC/CI availability:** Laptop built-in displays, virtual displays, and monitors with DDC/CI disabled in their OSD do not support DDC brightness or soft off; those menu items will be marked unavailable.
- **Brightness presets:** The brightness submenu uses fixed presets (`1%`, `25%`, `50%`, `75%`, `100%`). If the current brightness does not match any preset, none will be checked.

## Build

```powershell
git clone https://github.com/xapenny/SpkDisplayController.git
cd SpkDisplayController

dotnet build -c Release

# Framework-dependent single-file publish -> dist/SpkDisplayController.exe
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o dist
```

## Command-line options

Diagnostic options write output files to the executable's directory and exit:

| Option | Description |
| --- | --- |
| `--selftest` | Enumerate monitors and write capabilities/state to `SpkDisplayController-selftest.txt` |
| `--dumpmenu` | Write the tray menu tree to `SpkDisplayController-menu.txt` |
| `--simulate-softoff` | Used with `--dumpmenu` to simulate all monitors in soft-off state |
| `--lang=<code>` | Override UI language (`en`, `zh-Hans`, `ja`, `ko`, `ru`) |

## License

MIT License. See [LICENSE](LICENSE) for details.
