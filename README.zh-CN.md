# SpkDisplayController

<div align="center">

<img src="docs/icon.png" width="112" alt="SpkDisplayController" />

**从系统托盘控制每台显示器的 HDR、亮度、分辨率、刷新率、显示方向和电源状态。**

[![CI](https://github.com/xapenny/SpkDisplayController/actions/workflows/ci.yml/badge.svg)](https://github.com/xapenny/SpkDisplayController/actions/workflows/ci.yml)
[![Release](https://img.shields.io/github/v/release/xapenny/SpkDisplayController?display_name=tag)](https://github.com/xapenny/SpkDisplayController/releases)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/license-MIT-blue)](LICENSE)

[English](README.md) · 简体中文

</div>

---

SpkDisplayController 是一个轻量的 Windows 系统托盘工具，无需打开系统设置即可单独管理每台显示器。

## 功能

- **HDR**：为每台显示器独立开关 HDR。
- **亮度**：通过 DDC/CI 快速切换亮度预设（`1%`、`25%`、`50%`、`75%`、`100%`）。
- **分辨率与刷新率**：按显示器切换支持的分辨率与刷新率。
- **显示方向**：在横向、纵向、横向（翻转）、纵向（翻转）之间切换。
- **软关闭**：通过 DDC/CI（`VCP 0xD6 = 4`）关闭显示器面板，同时保持 Windows 连接状态，不打乱桌面布局和窗口位置。
- **开机启动**：支持登录时自动启动，且不重复弹出 UAC 提示。
- **多语言**：支持英语、简体中文、日语、韩语和俄语。

## 环境要求

- Windows 10 / 11（x64）
- [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0)
- 显示器已在 OSD 菜单中开启 DDC/CI（亮度和软关闭功能需要）

## 下载与运行

从 [GitHub Releases](https://github.com/xapenny/SpkDisplayController/releases) 下载最新版本。

运行 `SpkDisplayController.exe` 并确认 UAC 提权提示，左键或右键点击系统托盘图标即可打开菜单。

### 权限与开机启动

部分显卡驱动在标准用户权限下会拒绝或忽略 DDC/CI 与 HDR 修改请求，因此程序需要以管理员权限运行。

开启 **随 Windows 启动** 后，程序会在 Windows 任务计划程序中注册名为 `SpkDisplayController` 的高权限登录任务，以便开机登录时静默启动而不弹出 UAC。

## 已知限制

- **软关闭后的唤醒**：部分显示器在收到软关闭指令（`VCP 0xD6 = 4`）后会进入深度待机，无法通过鼠标/键盘或 DDC 指令唤醒，需要通过显示器物理电源键重新打开。
- **DDC/CI 支持情况**：笔记本内置屏幕、虚拟显示器以及在 OSD 中关闭了 DDC/CI 的显示器不支持 DDC 亮度和软关闭，对应菜单项会显示为不可用。
- **亮度预设**：亮度菜单提供固定的 `1%`、`25%`、`50%`、`75%` 和 `100%` 档位；若当前亮度不匹配其中任一档位，则不会显示勾选标记。

## 构建

```powershell
git clone https://github.com/xapenny/SpkDisplayController.git
cd SpkDisplayController

dotnet build -c Release

# 发布为依赖 .NET Runtime 的单文件程序 -> dist/SpkDisplayController.exe
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o dist
```

## 命令行参数

诊断参数会将结果输出到程序所在目录后退出：

| 参数 | 说明 |
| --- | --- |
| `--selftest` | 枚举显示器并将能力与状态写入 `SpkDisplayController-selftest.txt` |
| `--dumpmenu` | 将托盘菜单结构写入 `SpkDisplayController-menu.txt` |
| `--simulate-softoff` | 配合 `--dumpmenu` 使用，在内存中模拟所有显示器处于软关闭状态 |
| `--lang=<code>` | 指定界面语言（`en`、`zh-Hans`、`ja`、`ko`、`ru`） |

## 许可证

本项目使用 [MIT License](LICENSE)。
