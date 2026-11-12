using SpkDisplayController.Native;

namespace SpkDisplayController.Models;

/// <summary>
/// 单个物理显示器的运行时视图：身份、DDC 能力与当前状态。
/// </summary>
internal sealed class MonitorDevice : IDisposable
{
    /// <summary>\\.\DISPLAYn</summary>
    public string DeviceName { get; init; } = string.Empty;

    /// <summary>DDC 上报的型号描述，例如 VG252Q。</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>PnP 厂商代码，例如 ACR。</summary>
    public string Manufacturer { get; init; } = string.Empty;

    /// <summary>DISPLAY\ACR0724\5&amp;...</summary>
    public string DeviceId { get; init; } = string.Empty;

    /// <summary>菜单里展示的名字，例如 ACER VG252Q。</summary>
    public string DisplayLabel { get; init; } = string.Empty;

    public IntPtr Handle { get; internal set; } = IntPtr.Zero;

    /// <summary>
    /// 是否成功取得 DDC 句柄。不能用 Handle == IntPtr.Zero 判断，
    /// 因为 DXVA2 的句柄是内部索引，0 是合法值。
    /// </summary>
    public bool HasDdcHandle { get; internal set; }

    public DisplayConfig.TargetInfo? Target { get; internal set; }

    // ---- DDC 能力 ----
    public bool SupportsBrightness { get; internal set; }
    public bool SupportsPower { get; internal set; }

    // ---- 当前状态 ----
    public int Brightness { get; internal set; }
    public int BrightnessMax { get; internal set; } = 100;
    public uint PowerState { get; internal set; }
    public bool IsSoftOff => PowerState == Ddc.PowerSoftOff;

    public bool SupportsHdr { get; internal set; }
    public bool HdrEnabled { get; internal set; }

    public int BrightnessPercent =>
        BrightnessMax <= 0 ? 0 : (int)Math.Round(Brightness * 100.0 / BrightnessMax);

    public void Dispose()
    {
        if (HasDdcHandle)
        {
            Ddc.DestroyPhysicalMonitor(Handle);
            HasDdcHandle = false;
        }

        Handle = IntPtr.Zero;
    }
}
