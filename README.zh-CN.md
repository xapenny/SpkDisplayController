# SpkDisplayController

<div align="center">

<img src="docs/icon.png" width="112" alt="SpkDisplayController" />

**从系统托盘管理多台显示器的 HDR、亮度和电源状态。**

[![CI](https://github.com/xapenny/SpkDisplayController/actions/workflows/ci.yml/badge.svg)](https://github.com/xapenny/SpkDisplayController/actions/workflows/ci.yml)
[![Release](https://img.shields.io/github/v/release/xapenny/SpkDisplayController?display_name=tag)](https://github.com/xapenny/SpkDisplayController/releases)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/license-MIT-blue)](LICENSE)

[English](README.md) · 简体中文

</div>

---

SpkDisplayController 是一个运行在 Windows 系统托盘中的显示器控制工具。它通过 DDC/CI 和 Windows DisplayConfig API 管理显示器，不需要打开系统设置即可调整各台显示器的相关选项。

## 功能

- **HDR**：分别开启或关闭每台显示器的 HDR。
- **亮度**：通过 DDC/CI 调整显示器亮度。
- **软关闭**：发送 DDC/CI 电源状态指令，在不让 Windows 移除显示器的情况下关闭面板。
- **多显示器**：每台显示器的设置和操作相互独立。
- **系统托盘**：没有常驻主窗口，通过托盘图标访问所有功能。
- **开机启动**：支持登录时自动启动，无需每次手动确认 UAC。

## 工作方式

项目使用两套 Windows 接口控制显示器：

| 功能 | 使用的接口 |
| --- | --- |
| 显示器枚举 | EnumDisplayDevices / DXVA2 |
| 亮度控制 | DDC/CI，VCP `0x10` |
| 电源控制 | DDC/CI，VCP `0xD6` |
| HDR 状态 | DisplayConfig Advanced Color |
| 托盘菜单 | Win32 HMENU |

软关闭使用 DDC/CI 的 `VCP 0xD6`，将电源模式设置为 `4`。这与 Windows 断开显示器连接不同，正常情况下不会触发桌面重新排列。

## 环境要求

- Windows 10 / 11（x64）
- [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0)
- 显示器支持 DDC/CI，并且已在显示器 OSD 中开启该功能

## 下载与运行

从 [GitHub Releases](https://github.com/xapenny/SpkDisplayController/releases) 下载最新版本。

运行 `SpkDisplayController.exe`，根据 UAC 提示授予管理员权限。启动后程序会驻留在系统托盘，单击鼠标左键或右键即可打开菜单。

## 构建

```powershell
git clone https://github.com/xapenny/SpkDisplayController.git
cd SpkDisplayController

dotnet build -c Release

# 发布为单文件程序（依赖 .NET Runtime）
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o dist
```

生成的程序位于：

```text
dist/SpkDisplayController.exe
```

当前发布配置生成的文件约为 450 KB，运行时需要安装对应版本的 .NET Desktop Runtime。

## 命令行参数

| 参数 | 说明 |
| --- | --- |
| `--selftest` | 枚举显示器并输出状态信息到 `SpkDisplayController-selftest.txt` |
| `--dumpmenu` | 输出菜单结构到 `SpkDisplayController-menu.txt` |
| `--simulate-softoff` | 配合 `--dumpmenu` 使用，仅在内存中模拟所有显示器处于软关闭状态 |
| `--lang=xx` | 指定界面语言 |

支持的语言参数：

`en`、`zh-Hans`、`ja`、`ko`、`ru`

示例：

```powershell
.\SpkDisplayController.exe --selftest

.\SpkDisplayController.exe --dumpmenu --lang=ja

.\SpkDisplayController.exe --dumpmenu --simulate-softoff --lang=zh-Hans
```

## 权限与开机启动

程序的 manifest 使用 `requireAdministrator`，因此启动时需要管理员权限。

部分显卡驱动环境下，以标准用户权限调用 DDC/CI 的 `SetVCPFeature` 或 DisplayConfig 的 `SetAdvancedColorState` 可能无法正常工作。

开机启动使用 Windows 任务计划程序，而不是注册表 `Run` 项。程序会创建一个以最高权限运行的登录触发任务：

```powershell
schtasks /Create /TN "SpkDisplayController" /TR "\"<exe path>\"" /SC ONLOGON /RL HIGHEST /F
```

这样登录后可以直接启动已提升权限的进程，不需要再次弹出 UAC 确认。

## 技术实现

### DDC/CI

通过 `dxva2.dll` 调用以下接口：

- `GetPhysicalMonitorsFromHMONITOR`
- `GetVCPFeatureAndVCPFeatureReply`
- `SetVCPFeature`

使用的 VCP 指令：

| VCP | 功能 | 数值 |
| --- | --- | --- |
| `0x10` | 亮度 | 根据显示器支持的范围设置 |
| `0xD6` | 电源模式 | `1`：开启，`4`：软关闭 |

需要注意，DXVA2 返回的 physical monitor 句柄不能简单地通过 `IntPtr.Zero` 判断是否有效。部分驱动会返回数值为 `0` 的合法句柄，因此项目使用独立的 `HasDdcHandle` 标志记录句柄状态。

### HDR

HDR 状态通过 Windows DisplayConfig Advanced Color 接口读取和设置：

- `DISPLAYCONFIG_DEVICE_INFO_GET_ADVANCED_COLOR_INFO`（Type 9）
- `DISPLAYCONFIG_DEVICE_INFO_SET_ADVANCED_COLOR_STATE`（Type 10）

获取显示路径时，依次尝试：

1. `QDC_ONLY_ACTIVE_PATHS`
2. `QDC_DATABASE_CURRENT`
3. `QDC_ALL_PATHS`

这是为了兼容部分驱动无法正常处理 `QDC_ONLY_ACTIVE_PATHS` 的情况。

### 显示器关联

DDC/CI 和 DisplayConfig 使用不同的显示器标识，需要将两边的结果关联起来。

项目使用 `DISPLAYCONFIG_DEVICE_INFO_GET_SOURCE_NAME` 获取 `viewGdiDeviceName`，并将其与 DDC 枚举得到的 `\\.\DISPLAYn` 进行匹配。

不直接比较 `EnumDisplayDevices` 返回的 `DeviceID` 与 DisplayConfig 的路径标识，因为两者的命名格式并不一致。

### 原生菜单

托盘菜单直接使用 Win32 `HMENU` 和 `TrackPopupMenu` 实现，而不是 WinForms 的 `ContextMenuStrip`。

菜单由不可见的 `WS_POPUP | WS_EX_TOOLWINDOW` 窗口承载，并在每次打开时根据当前显示器状态重新生成。

## 已知限制

- **软关闭后的唤醒问题**：在某些显示器上，发送 `VCP 0xD6 = 4` 后，DDC 通道仍然可以读取，但发送 `VCP 0xD6 = 1` 或亮度指令无法唤醒面板，需要通过显示器自身的菜单按键恢复。不同显示器和连接方式的表现可能不同。
- **DDC/CI 支持情况**：如果显示器没有开启 DDC/CI，或者当前连接链路不支持相关指令，对应操作将无法使用。菜单会显示不可用状态。
- **亮度预设**：亮度菜单使用固定的 1%、25%、50%、75% 和 100% 档位。如果当前亮度不等于其中任何一个值，则不会有选中标记。

## 项目结构

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

## 许可证

本项目使用 [MIT License](LICENSE)。
