# SpkDisplayController

<div align="center">

<img src="docs/icon.png" width="112" alt="SpkDisplayController" />

**Control HDR, brightness, resolution, refresh rate, orientation and power state for each monitor from the Windows system tray or CLI.**

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
- **Brightness** – Quick presets (`1%`, `25%`, `50%`, `75%`, `100%`) in the tray menu, or any `0–100` value via CLI.
- **Resolution & Refresh rate** – Switch per-monitor resolution and refresh rate.
- **Orientation** – Rotate each monitor between Landscape, Portrait, Landscape (flipped) and Portrait (flipped).
- **Soft off** – Power off a monitor via DDC/CI (`VCP 0xD6 = 4`) while keeping it connected in Windows, preserving your desktop layout and window positions.
- **Command-line interface** – Scriptable CLI that automatically forwards commands to a running tray instance (or runs standalone when no tray instance is open).
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

## Command-line usage

CLI commands work whether or not the tray instance is already running. When a tray instance is active, commands are forwarded over a local Named Pipe so DDC handles do not conflict and the tray menu stays in sync.

> **Important (PowerShell / UAC):** Because `SpkDisplayController.exe` is a `WinExe` binary with `requireAdministrator`, run CLI commands from an **elevated** terminal and pipe to `Out-String` (or use `Start-Process -Wait`) in interactive PowerShell so the shell waits for output.

```powershell
# List monitors and current state (human-readable or JSON)
.\SpkDisplayController.exe -l | Out-String
.\SpkDisplayController.exe --list --json | Out-String

# Combine multiple operations on a monitor in one call
.\SpkDisplayController.exe -m 1 --resolution 1920x1080 --refresh-rate 60 | Out-String
.\SpkDisplayController.exe -m 1 -r 2560x1440 -f 144 -H off -b 50 | Out-String
.\SpkDisplayController.exe -m Philips -orientation portrait | Out-String
.\SpkDisplayController.exe -m all --soft-off | Out-String
.\SpkDisplayController.exe -R | Out-String
```

Every long option accepts both `--` and `-` prefixes (e.g. `--refresh-rate` or `-refresh-rate`), as well as `=value` syntax:

| Option | Short | Value | Description |
| --- | --- | --- | --- |
| `--monitor` | `-m` | `<target>` | `1..N`, `DISPLAYn` / `\\.\DISPLAYn`, name substring, or `all` |
| `--list` | `-l` | — | List monitors, active mode, HDR, brightness and power state (also `list`) |
| `--json` | `-j` | — | Output JSON when listing |
| `--resolution` | `-r` | `<WxH>` | Set resolution (e.g. `1920x1080`) |
| `--refresh-rate` | `-f` | `<Hz>` | Set refresh rate (aliases: `--refresh`, `--hz`; e.g. `60` or `60Hz`) |
| `--orientation` | `-o` | `<value>` | `0`/`landscape`, `90`/`portrait`, `180`/`landscape-flipped`, `270`/`portrait-flipped` |
| `--hdr` | `-H` | `<on\|off\|toggle>` | Set or toggle HDR |
| `--brightness` | `-b` | `<0-100>` | Set DDC brightness percentage (`0..100`) |
| `--soft-off` | `-s` | — | Send DDC soft off (`VCP 0xD6 = 4`) |
| `--refresh-monitors` | `-R` | — | Re-enumerate monitors on the running tray instance |
| `--help` | `-h` | — | Show CLI help |

- **Exit codes:** `0` success, `1` execution failure, `2` invalid arguments or unsupported monitor/mode.

### Diagnostic options

Diagnostic flags write output files to the executable's directory and exit:

| Option | Description |
| --- | --- |
| `--selftest` | Enumerate monitors and write capabilities/state to `SpkDisplayController-selftest.txt` |
| `--dumpmenu` | Write the tray menu tree to `SpkDisplayController-menu.txt` |
| `--simulate-softoff` | Used with `--dumpmenu` to simulate all monitors in soft-off state |
| `--lang=<code>` | Override UI language (`en`, `zh-Hans`, `ja`, `ko`, `ru`) |

## Known limitations

- **Soft-off recovery:** On some monitors, soft off (`VCP 0xD6 = 4`) puts the panel into a deep standby state that cannot be woken by mouse/keyboard input or DDC wake commands—you must turn it back on using the monitor's physical power button.
- **DDC/CI availability:** Laptop built-in displays, virtual displays, and monitors with DDC/CI disabled in their OSD do not support DDC brightness or soft off; those menu items will be marked unavailable.
- **Brightness presets:** The tray brightness submenu uses fixed presets (`1%`, `25%`, `50%`, `75%`, `100%`). If the current brightness does not match any preset, none will be checked (arbitrary `0–100` values are supported via CLI).

## Build

```powershell
git clone https://github.com/xapenny/SpkDisplayController.git
cd SpkDisplayController

dotnet build -c Release

# Framework-dependent single-file publish -> dist/SpkDisplayController.exe
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o dist
```

## License

MIT License. See [LICENSE](LICENSE) for details.