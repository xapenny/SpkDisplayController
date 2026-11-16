# Changelog / 更新日志

All notable changes to this project will be documented in this file.
本项目的所有重要变更都将记录在此文件中。

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [1.1.0] - 2026-11-16

### Added
- Per-monitor **Resolution**, **Refresh rate**, and **Orientation** submenus in the tray menu.
- Warning balloon notification when toggling **Start with Windows** fails.

### Changed
- Replaced `schtasks.exe` CLI calls with the Windows Task Scheduler 2.0 COM API (`Schedule.Service`), fixing auto-start failures on battery/restricted environments and removing the 72-hour execution limit.
- Added a 5-second logon delay (`PT5S`) and guarded initial monitor enumeration to improve reliability at Windows sign-in.

### Fixed
- **Start with Windows** checkmark now checks whether the scheduled task is enabled and points to the current executable path.

---

### 新增
- 托盘菜单中为每台显示器新增 **分辨率**、**刷新率** 和 **显示方向** 子菜单。
- 切换 **随 Windows 启动** 失败时显示托盘警告气泡提示。

### 变更
- 将开机自启实现从 `schtasks.exe` 命令行替换为 Windows Task Scheduler 2.0 COM API（`Schedule.Service`），解决电池供电/受限环境下启动失败与 72 小时自动终止限制问题。
- 为登录触发器增加 5 秒延迟（`PT5S`），并对启动初期的显示器枚举增加容错保护，提升开机启动稳定性。

### 修复
- **随 Windows 启动** 勾选状态现在会同时校验计划任务是否启用以及指向的可执行文件路径是否与当前程序一致。

## [1.0.0] - 2026-11-12

### Added
- Initial release with per-monitor HDR, DDC/CI brightness presets, and DDC/CI soft off from the Windows system tray.
- Support for 5 UI languages (`en`, `zh-Hans`, `ja`, `ko`, `ru`).

---

### 新增
- 首个发布版本，支持从 Windows 系统托盘按显示器独立控制 HDR、DDC/CI 亮度预设与 DDC/CI 软关闭。
- 内置 5 种界面语言支持（`en`、`zh-Hans`、`ja`、`ko`、`ru`）。

[Unreleased]: https://github.com/xapenny/SpkDisplayController/compare/v1.1.0...HEAD
[1.1.0]: https://github.com/xapenny/SpkDisplayController/compare/v1.0.0...v1.1.0
[1.0.0]: https://github.com/xapenny/SpkDisplayController/releases/tag/v1.0.0
