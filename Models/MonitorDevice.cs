using SpkDisplayController.Native;

namespace SpkDisplayController.Models;

/// <summary>
/// Runtime view of a single physical monitor: identity, DDC capabilities, and current state.
/// </summary>
internal sealed class MonitorDevice : IDisposable
{
    /// <summary>\\.\DISPLAYn</summary>
    public string DeviceName { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    /// <summary>PnP manufacturer code, e.g., ACR.</summary>
    public string Manufacturer { get; init; } = string.Empty;

    /// <summary>DISPLAY\ACR0724\5&amp;...</summary>
    public string DeviceId { get; init; } = string.Empty;

    public string DisplayLabel { get; init; } = string.Empty;

    public IntPtr Handle { get; internal set; } = IntPtr.Zero;

    /// <summary>
    /// Whether a DDC handle was successfully obtained. Do not check Handle == IntPtr.Zero,
    /// because a DXVA2 handle is an internal index where 0 is a valid value.
    /// </summary>
    public bool HasDdcHandle { get; internal set; }

    public DisplayConfig.TargetInfo? Target { get; internal set; }

    public bool SupportsBrightness { get; internal set; }
    public bool SupportsPower { get; internal set; }

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
