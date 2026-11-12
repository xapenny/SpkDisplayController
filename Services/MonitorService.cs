using System.Runtime.InteropServices;
using SpkDisplayController.Models;
using SpkDisplayController.Native;

namespace SpkDisplayController.Services;

/// <summary>
/// 显示器枚举与操作。持有所有 DDC 句柄，需显式 Dispose。
/// </summary>
internal sealed class MonitorService : IDisposable
{
    private readonly List<MonitorDevice> _monitors = new();

    public IReadOnlyList<MonitorDevice> Monitors => _monitors;

    private static readonly Dictionary<string, string> VendorNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ACR"] = "ACER",
        ["PHL"] = "Philips",
        ["SAM"] = "Samsung",
        ["GSM"] = "LG",
        ["DEL"] = "Dell",
        ["BNQ"] = "BenQ",
        ["AOC"] = "AOC",
        ["HWP"] = "HP",
        ["LEN"] = "Lenovo",
        ["VSC"] = "ViewSonic",
        ["MSI"] = "MSI",
        ["ASU"] = "ASUS",
        ["GBT"] = "Gigabyte",
        ["EIZ"] = "EIZO",
        ["NEC"] = "NEC",
        ["IVM"] = "iiyama",
        ["AUS"] = "ASUS",
        ["CMN"] = "ChiMei",
        ["BOE"] = "BOE",
        ["AUO"] = "AUO",
    };

    // ---------------------------------------------------------------- 枚举

    public void Refresh()
    {
        DisposeAll();

        var idMap = Ddc.MapDisplayIds();
        var targets = DisplayConfig.GetTargets();

        var handles = new Dictionary<string, (IntPtr Handle, string Description)>(StringComparer.OrdinalIgnoreCase);

        Ddc.MonitorEnumProc callback = (IntPtr hMon, IntPtr hdc, ref Ddc.RECT rect, IntPtr data) =>
        {
            var mi = new Ddc.MONITORINFOEX { cbSize = Marshal.SizeOf<Ddc.MONITORINFOEX>() };
            if (!Ddc.GetMonitorInfo(hMon, ref mi))
                return true;

            var deviceName = mi.szDevice ?? string.Empty;
            if (deviceName.Length == 0)
                return true;

            uint count = 0;
            if (!Ddc.GetNumberOfPhysicalMonitorsFromHMONITOR(hMon, ref count) || count == 0)
                return true;

            var physical = new Ddc.PHYSICAL_MONITOR[count];
            if (!Ddc.GetPhysicalMonitorsFromHMONITOR(hMon, count, physical))
                return true;

            // 镜像模式下会有多个物理显示器；取第一个作为代表。
            handles[deviceName] = (physical[0].hPhysicalMonitor,
                                   physical[0].szPhysicalMonitorDescription ?? string.Empty);

            for (var i = 1; i < physical.Length; i++)
            {
                if (physical[i].hPhysicalMonitor != IntPtr.Zero)
                    Ddc.DestroyPhysicalMonitor(physical[i].hPhysicalMonitor);
            }

            return true;
        };

        Ddc.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, callback, IntPtr.Zero);
        GC.KeepAlive(callback);

        foreach (var (deviceName, entry) in handles)
        {
            var deviceId = idMap.TryGetValue(deviceName, out var mapped) ? mapped.MonitorId : string.Empty;

            // 通过 source 的 GDI 设备名精确匹配 \\.\DISPLAYn
            var target = targets.FirstOrDefault(t =>
                !string.IsNullOrEmpty(t.GdiDeviceName) &&
                string.Equals(t.GdiDeviceName, deviceName, StringComparison.OrdinalIgnoreCase));

            var vendor = ExtractVendor(deviceId);
            var description = entry.Description;
            if (string.IsNullOrWhiteSpace(description))
                description = target?.FriendlyName ?? deviceName;

            var label = BuildLabel(vendor, description);

            var monitor = new MonitorDevice
            {
                DeviceName = deviceName,
                Description = description,
                Manufacturer = vendor,
                DeviceId = deviceId,
                DisplayLabel = label,
                Handle = entry.Handle,
                HasDdcHandle = true,
                Target = target
            };

            ReadState(monitor);
            _monitors.Add(monitor);
        }

        // 按系统枚举顺序排序，保证菜单顺序稳定
        _monitors.Sort((a, b) => string.CompareOrdinal(a.DeviceName, b.DeviceName));
    }

    private static string ExtractVendor(string deviceId)
    {
        if (string.IsNullOrEmpty(deviceId))
            return string.Empty;

        var parts = deviceId.Split('\\');
        if (parts.Length < 2)
            return string.Empty;

        var pnp = parts[1];
        return pnp.Length >= 3 ? pnp[..3].ToUpperInvariant() : pnp.ToUpperInvariant();
    }

    private static string BuildLabel(string vendor, string description)
    {
        description = CleanDescription(description);

        var vendorName = vendor.Length > 0 && VendorNames.TryGetValue(vendor, out var friendly)
            ? friendly
            : vendor;

        if (string.IsNullOrWhiteSpace(vendorName))
            return description;

        if (string.IsNullOrWhiteSpace(description))
            return vendorName;

        // 描述里已经带厂商名时不重复
        if (description.StartsWith(vendorName, StringComparison.OrdinalIgnoreCase))
            return description;

        return $"{vendorName} {description}";
    }

    /// <summary>去掉 DDC 描述里的括号补充，例如 "32M2N8900X (31.5 inch Wide OLED…)"。</summary>
    private static string CleanDescription(string description)
    {
        if (string.IsNullOrWhiteSpace(description))
            return description;

        var idx = description.IndexOf('(');
        if (idx > 0)
            description = description[..idx].TrimEnd();

        return description;
    }

    // ---------------------------------------------------------------- 状态

    public void ReadState(MonitorDevice monitor)
    {
        monitor.SupportsBrightness = false;
        monitor.SupportsPower = false;
        monitor.SupportsHdr = false;

        if (Ddc.TryGetVcp(monitor.Handle, Ddc.VcpBrightness, out var brightness, out var brightnessMax)
            && brightnessMax > 0)
        {
            monitor.SupportsBrightness = true;
            monitor.Brightness = (int)brightness;
            monitor.BrightnessMax = (int)brightnessMax;
        }

        if (Ddc.TryGetVcp(monitor.Handle, Ddc.VcpPowerMode, out var power, out _))
        {
            monitor.SupportsPower = true;
            monitor.PowerState = power;
        }

        if (monitor.Target is not null
            && DisplayConfig.TryGetHdr(monitor.Target, out var supported, out var enabled))
        {
            monitor.SupportsHdr = supported;
            monitor.HdrEnabled = enabled;
        }
    }

    public void ReadAll()
    {
        foreach (var m in _monitors)
            ReadState(m);
    }

    // ---------------------------------------------------------------- 操作

    public bool SetBrightnessPercent(MonitorDevice monitor, int percent)
    {
        if (!monitor.SupportsBrightness || monitor.BrightnessMax <= 0)
            return false;

        var target = (uint)Math.Round(percent / 100.0 * monitor.BrightnessMax);
        if (!Ddc.TrySetVcp(monitor.Handle, Ddc.VcpBrightness, target))
            return false;

        monitor.Brightness = (int)target;
        return true;
    }

    public bool SoftOff(MonitorDevice monitor)
    {
        if (!monitor.SupportsPower)
            return false;

        if (!Ddc.TrySetVcp(monitor.Handle, Ddc.VcpPowerMode, Ddc.PowerSoftOff))
            return false;

        monitor.PowerState = Ddc.PowerSoftOff;
        return true;
    }

    public bool SetHdr(MonitorDevice monitor, bool enable)
    {
        if (monitor.Target is null || !monitor.SupportsHdr)
            return false;

        if (!DisplayConfig.SetHdr(monitor.Target, enable))
            return false;

        monitor.HdrEnabled = enable;
        return true;
    }

    public bool ToggleHdr(MonitorDevice monitor) => SetHdr(monitor, !monitor.HdrEnabled);

    /// <summary>仅供诊断：在内存中把所有显示器标记为软关闭，不发送任何 DDC 命令。</summary>
    internal void SimulateSoftOff()
    {
        foreach (var monitor in _monitors)
            monitor.PowerState = Ddc.PowerSoftOff;
    }

    // ---------------------------------------------------------------- 释放

    private void DisposeAll()
    {
        foreach (var m in _monitors)
            m.Dispose();
        _monitors.Clear();
    }

    public void Dispose() => DisposeAll();
}
