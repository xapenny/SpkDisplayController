using System.Reflection;
using SpkDisplayController.Localization;
using SpkDisplayController.Models;
using SpkDisplayController.Native;
using SpkDisplayController.Services;
using SpkDisplayController.UI;
using Microsoft.Win32;

namespace SpkDisplayController;

/// <summary>
/// Tray host: builds the three-section native menu and manages monitor state and lifecycle.
/// </summary>
internal sealed class TrayAppContext : ApplicationContext
{
    private static readonly int[] BrightnessPresets = { 1, 25, 50, 75, 100 };
    private static readonly (int Value, string LocKey)[] OrientationPresets =
    {
        (0, "Orientation0"),
        (1, "Orientation90"),
        (2, "Orientation180"),
        (3, "Orientation270"),
    };

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

        try
        {
            _service.Refresh();
        }
        catch
        {
            // Display enumeration may fail transiently during early logon.
        }
    }

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
            // Enumeration can fail during monitor hot-plugging; ignore.
        }
    }

    /// <summary>
    /// Rebuilds the entire HMENU before each popup. Native menus have no reusable object model,
    /// so state must be fetched and constructed on demand.
    /// </summary>
    private void ShowMenu()
    {
        Point pos = Cursor.Position;

        using var menu = new NativeMenu();
        var root = menu.Root;

        var autoStartChecked = AutoStart.IsEnabled;
        menu.AddItem(root, Loc.Get("StartWithWindows"), true, autoStartChecked, () =>
        {
            var next = !AutoStart.IsEnabled;
            if (!AutoStart.Set(next))
            {
                var detail = string.IsNullOrWhiteSpace(AutoStart.LastError)
                    ? Loc.Get("AutoStartFail")
                    : $"{Loc.Get("AutoStartFail")}\n{AutoStart.LastError}";
                ShowBalloon(detail, ToolTipIcon.Warning);
            }
        });

        menu.AddItem(root, Loc.Get("RefreshMonitors"), true, false, () => _service.Refresh());

        menu.AddSeparator(root);

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
        try
        {
            _service.ReadState(monitor);
        }
        catch
        {
            // Keep previous state if DDC occasionally times out
        }

        var sub = menu.AddSubMenu(root, monitor.DisplayLabel, enabled: true);

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

        if (monitor.SupportsDisplayModes && monitor.SupportedResolutions.Count > 0)
        {
            var resolution = menu.AddSubMenu(sub, Loc.Get("Resolution"), enabled: true);

            foreach (var (w, h) in monitor.SupportedResolutions)
            {
                var width = w;
                var height = h;
                var isChecked = width == monitor.CurrentWidth && height == monitor.CurrentHeight;

                menu.AddItem(resolution, $"{width} × {height}", true, isChecked, () =>
                {
                    if (!_service.SetResolution(monitor, width, height))
                        ShowBalloon(Loc.Get("ResolutionFail", monitor.DisplayLabel), ToolTipIcon.Warning);
                });
            }
        }
        else
        {
            menu.AddItem(sub, $"{Loc.Get("Resolution")} {Loc.Get("Unavailable")}", false, false, null);
        }

        if (monitor.SupportsDisplayModes && monitor.SupportedRefreshRates.Count > 0)
        {
            var refresh = menu.AddSubMenu(sub, Loc.Get("RefreshRate"), enabled: true);

            foreach (var hz in monitor.SupportedRefreshRates)
            {
                var rate = hz;
                var isChecked = rate == monitor.CurrentRefreshRate;

                menu.AddItem(refresh, $"{rate} Hz", true, isChecked, () =>
                {
                    if (!_service.SetRefreshRate(monitor, rate))
                        ShowBalloon(Loc.Get("RefreshRateFail", monitor.DisplayLabel), ToolTipIcon.Warning);
                });
            }
        }
        else
        {
            menu.AddItem(sub, $"{Loc.Get("RefreshRate")} {Loc.Get("Unavailable")}", false, false, null);
        }

        if (monitor.SupportsDisplayModes)
        {
            var orientation = menu.AddSubMenu(sub, Loc.Get("Orientation"), enabled: true);

            foreach (var (value, locKey) in OrientationPresets)
            {
                var targetOrient = value;
                var isChecked = monitor.CurrentOrientation == targetOrient;

                menu.AddItem(orientation, Loc.Get(locKey), true, isChecked, () =>
                {
                    if (!_service.SetOrientation(monitor, targetOrient))
                        ShowBalloon(Loc.Get("OrientationFail", monitor.DisplayLabel), ToolTipIcon.Warning);
                });
            }
        }
        else
        {
            menu.AddItem(sub, $"{Loc.Get("Orientation")} {Loc.Get("Unavailable")}", false, false, null);
        }

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

    private void ShowBalloon(string message, ToolTipIcon icon)
    {
        _tray.BalloonTipTitle = Loc.Get("AppName");
        _tray.BalloonTipText = message;
        _tray.BalloonTipIcon = icon;
        _tray.ShowBalloonTip(2500);
    }

    /// <summary>Diagnostics only: marks all monitors as soft-off to verify disabled menu states.</summary>
    internal void SimulateSoftOff() => _service.SimulateSoftOff();

    /// <summary>
    /// Renders the menu tree as text. Because native menus have no traversable object model,
    /// this reuses the same construction logic and records the output into a StringBuilder.
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

                if (monitor.SupportsDisplayModes && monitor.SupportedResolutions.Count > 0)
                {
                    Line(Loc.Get("Resolution") + " >");
                    depth = 2;
                    foreach (var (w, h) in monitor.SupportedResolutions)
                    {
                        var chk = w == monitor.CurrentWidth && h == monitor.CurrentHeight;
                        Line($"{w} × {h}" + (chk ? " [checked]" : ""));
                    }
                    depth = 1;
                }
                else
                {
                    Line($"{Loc.Get("Resolution")} {Loc.Get("Unavailable")} [disabled]");
                }

                if (monitor.SupportsDisplayModes && monitor.SupportedRefreshRates.Count > 0)
                {
                    Line(Loc.Get("RefreshRate") + " >");
                    depth = 2;
                    foreach (var hz in monitor.SupportedRefreshRates)
                    {
                        var chk = hz == monitor.CurrentRefreshRate;
                        Line($"{hz} Hz" + (chk ? " [checked]" : ""));
                    }
                    depth = 1;
                }
                else
                {
                    Line($"{Loc.Get("RefreshRate")} {Loc.Get("Unavailable")} [disabled]");
                }

                if (monitor.SupportsDisplayModes)
                {
                    Line(Loc.Get("Orientation") + " >");
                    depth = 2;
                    foreach (var (value, locKey) in OrientationPresets)
                    {
                        var chk = monitor.CurrentOrientation == value;
                        Line(Loc.Get(locKey) + (chk ? " [checked]" : ""));
                    }
                    depth = 1;
                }
                else
                {
                    Line($"{Loc.Get("Orientation")} {Loc.Get("Unavailable")} [disabled]");
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
    /// TrackPopupMenu requires an owner window. Use an invisible message window
    /// as a stand-in—it does not participate in layout and never appears on the taskbar.
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
