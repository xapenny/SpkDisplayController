using System.Runtime.InteropServices;

namespace SpkDisplayController.Native;

/// <summary>
/// DDC/CI interop (dxva2.dll) and GDI display device enumeration (user32.dll).
/// </summary>
internal static class Ddc
{
    public const byte VcpBrightness = 0x10;
    public const byte VcpPowerMode = 0xD6;

    // VCP 0xD6 power mode values (MCCS 1.2)
    public const uint PowerOn = 1;
    public const uint PowerStandby = 2;
    public const uint PowerSuspend = 3;
    public const uint PowerSoftOff = 4;

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left, Top, Right, Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct MONITORINFOEX
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string szDevice;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct PHYSICAL_MONITOR
    {
        public IntPtr hPhysicalMonitor;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szPhysicalMonitorDescription;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct DISPLAY_DEVICE
    {
        public int cb;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string DeviceName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceString;
        public uint StateFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceID;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceKey;
    }

    public delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdc, ref RECT rect, IntPtr data);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr clip, MonitorEnumProc proc, IntPtr data);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFOEX info);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern bool EnumDisplayDevices(string? lpDevice, uint iDevNum, ref DISPLAY_DEVICE device, uint dwFlags);

    [DllImport("dxva2.dll", SetLastError = true)]
    public static extern bool GetNumberOfPhysicalMonitorsFromHMONITOR(IntPtr hMonitor, ref uint count);

    [DllImport("dxva2.dll", SetLastError = true)]
    public static extern bool GetPhysicalMonitorsFromHMONITOR(IntPtr hMonitor, uint count, [Out] PHYSICAL_MONITOR[] monitors);

    [DllImport("dxva2.dll", SetLastError = true)]
    public static extern bool GetVCPFeatureAndVCPFeatureReply(IntPtr hPhysicalMonitor, byte code, IntPtr type, ref uint current, ref uint maximum);

    [DllImport("dxva2.dll", SetLastError = true)]
    public static extern bool SetVCPFeature(IntPtr hPhysicalMonitor, byte code, uint value);

    [DllImport("dxva2.dll", SetLastError = true)]
    public static extern bool DestroyPhysicalMonitor(IntPtr hPhysicalMonitor);

    /// <remarks>
    /// DXVA2 physical monitor handles are driver-internal index values where 0 is valid,
    /// so IntPtr.Zero must not be used to check handle validity.
    /// </remarks>
    public static bool TryGetVcp(IntPtr handle, byte code, out uint current, out uint maximum)
    {
        current = 0;
        maximum = 0;
        return GetVCPFeatureAndVCPFeatureReply(handle, code, IntPtr.Zero, ref current, ref maximum);
    }

    public static bool TrySetVcp(IntPtr handle, byte code, uint value)
    {
        return SetVCPFeature(handle, code, value);
    }

    /// <summary>
    /// Enumerates the mapping between \\.\DISPLAYn and physical monitor IDs.
    /// Returns a dictionary: device name -> (monitor DeviceID, adapter description).
    /// </summary>
    public static Dictionary<string, (string MonitorId, string AdapterName)> MapDisplayIds()
    {
        var map = new Dictionary<string, (string, string)>(StringComparer.OrdinalIgnoreCase);

        for (uint i = 0; i < 32; i++)
        {
            var adapter = new DISPLAY_DEVICE { cb = Marshal.SizeOf<DISPLAY_DEVICE>() };
            if (!EnumDisplayDevices(null, i, ref adapter, 0))
                break;

            if (string.IsNullOrEmpty(adapter.DeviceName))
                continue;

            for (uint j = 0; j < 8; j++)
            {
                var mon = new DISPLAY_DEVICE { cb = Marshal.SizeOf<DISPLAY_DEVICE>() };
                if (!EnumDisplayDevices(adapter.DeviceName, j, ref mon, 0))
                    break;

                if (string.IsNullOrEmpty(mon.DeviceID))
                    continue;

                map[adapter.DeviceName] = (mon.DeviceID, adapter.DeviceString);
                break;
            }
        }

        return map;
    }
}
