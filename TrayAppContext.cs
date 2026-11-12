using System.Reflection;
using SpkDisplayController.Localization;
using SpkDisplayController.Models;
using SpkDisplayController.Native;
using SpkDisplayController.Services;
using SpkDisplayController.UI;
using Microsoft.Win32;

namespace SpkDisplayController;

/// <summary>
/// 托盘宿主：构建三分区原生菜单，管理显示器状态与生命周期。
/// </summary>
internal sealed class TrayAppContext : ApplicationContext
{
    private static readonly int[] BrightnessPresets = { 1, 25, 50, 75, 100 };

    private readonly NotifyIcon _tray;
    private readonly MenuHostWindow _menuHost;
    private readonly MonitorService _service = new();
    private readonly Icon? _appIcon;

    private bool _disposed;

    public TrayAppContext()
    {
        Loc.Initialize();

        _appIcon = LoadAppIcon();
        _menuHost = new MenuHostWindow();

        _tray = new NotifyIcon
        {
            Icon = _appIcon ?? SystemIcons.Application,
            Text = Loc.Get("TrayTooltip"),
            Visible = true
        };
        _tray.MouseUp += OnTrayMouseUp;

        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;

        _service.Refresh();
    }

    // ---------------------------------------------------------------- 图标

    private static Icon? LoadAppIcon()
    {
        try
        {
            var asm = Assembly.GetExecutingAssembly();
            using var stream = asm.GetManifestResourceStream("SpkDisplayController.Resources.app.ico");
            return stream is null ? null : new Icon(stream);
        }
        catch
        {
            return null;
        }
    }

    // ---------------------------------------------------------------- 托盘交互

    private void OnTrayMouseUp(object? sender, MouseEventArgs e)
    {
        if (e.Button is MouseButtons.Left or MouseButtons.Right)
            ShowMenu();
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        try
        {
            _service.Refresh();
        }
        catch
        {
            // 显示器热插拔期间枚举可能失败，忽略。
        }
    }

    // ---------------------------------------------------------------- 菜单

    /// <summary>
    /// 每次弹出前重建整棵 HMENU。原生菜单没有可复用的对象模型，
    /// 状态必须现取现建。
    /// </summary>
    private void ShowMenu()
    {
        Point pos = Cursor.Position;

        using var menu = new NativeMenu();
        var root = menu.Root;

        // ---- 分区 1 ----
        var autoStartChecked = AutoStart.IsEnabled;
        menu.AddItem(root, Loc.Get("StartWithWindows"), true, autoStartChecked, () =>
        {
            var next = !AutoStart.IsEnabled;
            AutoStart.Set(next);
        });

        menu.AddItem(root, Loc.Get("RefreshMonitors"), true, false, () => _service.Refresh());

        menu.AddSeparator(root);

        // ---- 分区 2 ----
        if (_service.Monitors.Count == 0)
        {
            menu.AddItem(root, Loc.Get("NoMonitors"), false, false, null);
        }
        else
        {
            foreach (var monitor in _service.Monitors)
                BuildMonitorMenu(menu, root, monitor);
        }

        menu.AddSeparator(root);

        // ---- 分区 3 ----
        menu.AddItem(root, Loc.Get("About"), true, false, () =>
        {
            using var form = new AboutForm(_appIcon, _service.Monitors.Count);
            form.ShowDialog();
        });

        menu.AddItem(root, Loc.Get("Exit"), true, false, ExitApplication);

        menu.Show(_menuHost.Handle, pos.X, pos.Y);
    }

    private void BuildMonitorMenu(NativeMenu menu, IntPtr root, MonitorDevice monitor)
    {
        // 弹出前重读该显示器状态，保证勾选与禁用态是最新的
        try
        {
            _service.ReadState(monitor);
        }
        catch
        {
            // DDC 偶发超时，沿用上一次状态
        }

        var sub = menu.AddSubMenu(root, monitor.DisplayLabel, enabled: true);

        // ---- HDR ----
        if (monitor.SupportsHdr)
        {
            menu.AddItem(sub, Loc.Get("Hdr"), true, monitor.HdrEnabled, () =>
            {
                if (!_service.SetHdr(monitor, !monitor.HdrEnabled))
                    ShowBalloon(Loc.Get("HdrFail", monitor.DisplayLabel), ToolTipIcon.Warning);
            });
        }
        else
        {
            menu.AddItem(sub, $"{Loc.Get("Hdr")} {Loc.Get("Unavailable")}", false, false, null);
        }

        // ---- 亮度（三级菜单）----
        if (monitor.SupportsBrightness)
        {
            var brightness = menu.AddSubMenu(sub, Loc.Get("Brightness"), enabled: true);

            foreach (var percent in BrightnessPresets)
            {
                var value = percent;
                var isChecked = Math.Abs(monitor.BrightnessPercent - percent) <= 1;

                menu.AddItem(brightness, $"{percent}%", true, isChecked, () =>
                {
                    if (!_service.SetBrightnessPercent(monitor, value))
                        ShowBalloon(Loc.Get("BrightnessFail", monitor.DisplayLabel), ToolTipIcon.Warning);
                });
            }
        }
        else
        {
            menu.AddItem(sub, $"{Loc.Get("Brightness")} {Loc.Get("Unavailable")}", false, false, null);
        }

        // ---- 软关闭 ----
        if (!monitor.SupportsPower)
        {
            menu.AddItem(sub, $"{Loc.Get("SoftOff")} {Loc.Get("Unavailable")}", false, false, null);
        }
        else if (monitor.IsSoftOff)
        {
            menu.AddItem(sub, $"{Loc.Get("SoftOff")} {Loc.Get("AlreadySoftOff")}", false, false, null);
        }
        else
        {
            menu.AddItem(sub, Loc.Get("SoftOff"), true, false, () =>
            {
                if (!_service.SoftOff(monitor))
                    ShowBalloon(Loc.Get("SoftOffFail", monitor.DisplayLabel), ToolTipIcon.Warning);
            });
        }
    }

    // ---------------------------------------------------------------- 提示

    private void ShowBalloon(string message, ToolTipIcon icon)
    {
        _tray.BalloonTipTitle = Loc.Get("AppName");
        _tray.BalloonTipText = message;
        _tray.BalloonTipIcon = icon;
        _tray.ShowBalloonTip(2500);
    }

    // ---------------------------------------------------------------- 诊断

    /// <summary>仅供诊断：把所有显示器标记为软关闭，用于验证菜单禁用态。</summary>
    internal void SimulateSoftOff() => _service.SimulateSoftOff();

    /// <summary>
    /// 把菜单树渲染成文本。原生菜单没有可遍历的对象模型，
    /// 因此这里复用同一套构建逻辑，把结果记录到 StringBuilder。
    /// </summary>
    internal string DumpMenuTree()
    {
        var sb = new System.Text.StringBuilder();
        var depth = 0;

        void Line(string text) => sb.AppendLine(new string(' ', depth * 2) + text);

        depth = 0;
        Line(Loc.Get("StartWithWindows") + (AutoStart.IsEnabled ? " [checked]" : ""));
        Line(Loc.Get("RefreshMonitors"));
        Line("--------");

        if (_service.Monitors.Count == 0)
        {
            Line(Loc.Get("NoMonitors") + " [disabled]");
        }
        else
        {
            foreach (var monitor in _service.Monitors)
            {
                Line(monitor.DisplayLabel + " >");
                depth = 1;

                if (monitor.SupportsHdr)
                    Line(Loc.Get("Hdr") + (monitor.HdrEnabled ? " [checked]" : ""));
                else
                    Line($"{Loc.Get("Hdr")} {Loc.Get("Unavailable")} [disabled]");

                if (monitor.SupportsBrightness)
                {
                    Line(Loc.Get("Brightness") + " >");
                    depth = 2;
                    foreach (var percent in BrightnessPresets)
                    {
                        var chk = Math.Abs(monitor.BrightnessPercent - percent) <= 1;
                        Line($"{percent}%" + (chk ? " [checked]" : ""));
                    }
                    depth = 1;
                }
                else
                {
                    Line($"{Loc.Get("Brightness")} {Loc.Get("Unavailable")} [disabled]");
                }

                if (!monitor.SupportsPower)
                    Line($"{Loc.Get("SoftOff")} {Loc.Get("Unavailable")} [disabled]");
                else if (monitor.IsSoftOff)
                    Line($"{Loc.Get("SoftOff")} {Loc.Get("AlreadySoftOff")} [disabled]");
                else
                    Line(Loc.Get("SoftOff"));

                depth = 0;
            }
        }

        Line("--------");
        Line(Loc.Get("About"));
        Line(Loc.Get("Exit"));

        return sb.ToString();
    }

    // ---------------------------------------------------------------- 退出

    private void ExitApplication()
    {
        _tray.Visible = false;
        ExitThread();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_disposed)
        {
            _disposed = true;

            SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;

            if (_tray is not null)
            {
                _tray.Visible = false;
                _tray.Dispose();
            }

            _menuHost?.Dispose();
            _service.Dispose();
        }

        base.Dispose(disposing);
    }

    /// <summary>
    /// TrackPopupMenu 需要一个窗口作为 owner。这里用一个不可见的
    /// 消息窗口顶替——它不参与布局，也不出现在任务栏。
    /// </summary>
    private sealed class MenuHostWindow : NativeWindow, IDisposable
    {
        private bool _disposed;

        public MenuHostWindow()
        {
            CreateHandle(new CreateParams
            {
                Caption = "SpkDisplayController.MenuHost",
                X = 0,
                Y = 0,
                Width = 0,
                Height = 0,
                Style = unchecked((int)0x80000000),   // WS_POPUP
                ExStyle = 0x00000080  // WS_EX_TOOLWINDOW
            });
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            DestroyHandle();
        }
    }
}
