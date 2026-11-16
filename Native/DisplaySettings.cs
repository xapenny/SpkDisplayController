using System.Runtime.InteropServices;

namespace SpkDisplayController.Native;

/// <summary>
/// Win32 GDI display mode enumeration and switching (EnumDisplaySettingsEx / ChangeDisplaySettingsEx).
/// </summary>
internal static class DisplaySettings
{
    private const int EnumCurrentSettings = -1;

    private const uint DmDisplayOrientation = 0x00000080;
    private const uint DmBitsPerPel = 0x00040000;
    private const uint DmPelsWidth = 0x00080000;
    private const uint DmPelsHeight = 0x00100000;
    private const uint DmDisplayFrequency = 0x00400000;

    private const uint DmDisplayFlagsInterlaced = 0x00000002;

    private const uint CdsUpdateRegistry = 0x00000001;
    private const int DispChangeSuccessful = 0;
    private const int DispChangeRestart = 1;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DEVMODE
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string dmDeviceName;
        public ushort dmSpecVersion;
        public ushort dmDriverVersion;
        public ushort dmSize;
        public ushort dmDriverExtra;
        public uint dmFields;
        public int dmPositionX;
        public int dmPositionY;
        public uint dmDisplayOrientation;
        public uint dmDisplayFixedOutput;
        public short dmColor;
        public short dmDuplex;
        public short dmYResolution;
        public short dmTTOption;
        public short dmCollate;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string dmFormName;
        public ushort dmLogPixels;
        public uint dmBitsPerPel;
        public uint dmPelsWidth;
        public uint dmPelsHeight;
        public uint dmDisplayFlags;
        public uint dmDisplayFrequency;
        public uint dmICMMethod;
        public uint dmICMIntent;
        public uint dmMediaType;
        public uint dmDitherType;
        public uint dmReserved1;
        public uint dmReserved2;
        public uint dmPanningWidth;
        public uint dmPanningHeight;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool EnumDisplaySettingsEx(
        string lpszDeviceName, int iModeNum, ref DEVMODE lpDevMode, uint dwFlags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int ChangeDisplaySettingsEx(
        string lpszDeviceName, ref DEVMODE lpDevMode, IntPtr hwnd, uint dwFlags, IntPtr lParam);

    public readonly record struct DisplayState(
        int Width,
        int Height,
        int RefreshRate,
        int Orientation,
        IReadOnlyList<(int Width, int Height)> Resolutions,
        IReadOnlyList<int> RefreshRates);

    public static bool TryGetState(string deviceName, out DisplayState state)
    {
        state = default;

        var current = CreateDevMode();
        if (!EnumDisplaySettingsEx(deviceName, EnumCurrentSettings, ref current, 0))
            return false;

        var curW = (int)current.dmPelsWidth;
        var curH = (int)current.dmPelsHeight;
        var curHz = (int)current.dmDisplayFrequency;
        if (curW <= 0 || curH <= 0)
            return false;

        var resolutions = new HashSet<(int Width, int Height)>();
        var refreshRates = new HashSet<int>();

        for (var i = 0; ; i++)
        {
            var mode = CreateDevMode();
            if (!EnumDisplaySettingsEx(deviceName, i, ref mode, 0))
                break;

            if (mode.dmBitsPerPel != current.dmBitsPerPel)
                continue;

            if ((mode.dmDisplayFlags & DmDisplayFlagsInterlaced) != 0)
                continue;

            var w = (int)mode.dmPelsWidth;
            var h = (int)mode.dmPelsHeight;
            var hz = (int)mode.dmDisplayFrequency;
            if (w <= 0 || h <= 0 || hz <= 1)
                continue;

            resolutions.Add((w, h));

            if (w == curW && h == curH)
                refreshRates.Add(hz);
        }

        if (resolutions.Count == 0)
            resolutions.Add((curW, curH));

        if (curHz > 1 && refreshRates.Count == 0)
            refreshRates.Add(curHz);

        var sortedRes = resolutions
            .OrderByDescending(r => (long)r.Width * r.Height)
            .ThenByDescending(r => r.Width)
            .ThenByDescending(r => r.Height)
            .ToArray();

        var sortedHz = refreshRates
            .OrderByDescending(hz => hz)
            .ToArray();

        state = new DisplayState(curW, curH, curHz, (int)current.dmDisplayOrientation, sortedRes, sortedHz);
        return true;
    }

    public static bool TrySetResolution(string deviceName, int width, int height)
    {
        var current = CreateDevMode();
        if (!EnumDisplaySettingsEx(deviceName, EnumCurrentSettings, ref current, 0))
            return false;

        if ((int)current.dmPelsWidth == width && (int)current.dmPelsHeight == height)
            return true;

        var targetHz = PickRefreshRateForResolution(deviceName, current.dmBitsPerPel, width, height, (int)current.dmDisplayFrequency);
        if (targetHz <= 1)
            return false;

        return ApplyMode(deviceName, current, width, height, targetHz);
    }

    public static bool TrySetRefreshRate(string deviceName, int refreshRate)
    {
        var current = CreateDevMode();
        if (!EnumDisplaySettingsEx(deviceName, EnumCurrentSettings, ref current, 0))
            return false;

        if ((int)current.dmDisplayFrequency == refreshRate)
            return true;

        return ApplyMode(deviceName, current, (int)current.dmPelsWidth, (int)current.dmPelsHeight, refreshRate);
    }

    public static bool TrySetOrientation(string deviceName, int orientation)
    {
        if (orientation is < 0 or > 3)
            return false;

        var current = CreateDevMode();
        if (!EnumDisplaySettingsEx(deviceName, EnumCurrentSettings, ref current, 0))
            return false;

        var curOrientation = (int)current.dmDisplayOrientation;
        if (curOrientation == orientation)
            return true;

        var width = (int)current.dmPelsWidth;
        var height = (int)current.dmPelsHeight;

        // Swap width and height when switching between landscape (0/2) and portrait (1/3).
        if ((curOrientation % 2) != (orientation % 2))
            (width, height) = (height, width);

        current.dmDisplayOrientation = (uint)orientation;
        return ApplyMode(deviceName, current, width, height, (int)current.dmDisplayFrequency);
    }

    private static int PickRefreshRateForResolution(
        string deviceName, uint bitsPerPel, int width, int height, int preferredHz)
    {
        var bestHz = 0;

        for (var i = 0; ; i++)
        {
            var mode = CreateDevMode();
            if (!EnumDisplaySettingsEx(deviceName, i, ref mode, 0))
                break;

            if (mode.dmBitsPerPel != bitsPerPel || (mode.dmDisplayFlags & DmDisplayFlagsInterlaced) != 0)
                continue;

            if ((int)mode.dmPelsWidth != width || (int)mode.dmPelsHeight != height)
                continue;

            var hz = (int)mode.dmDisplayFrequency;
            if (hz <= 1)
                continue;

            if (hz == preferredHz)
                return preferredHz;

            if (hz > bestHz)
                bestHz = hz;
        }

        return bestHz;
    }

    private static bool ApplyMode(string deviceName, DEVMODE current, int width, int height, int refreshRate)
    {
        current.dmPelsWidth = (uint)width;
        current.dmPelsHeight = (uint)height;
        current.dmDisplayFrequency = (uint)refreshRate;
        // Omit DM_POSITION and DM_DISPLAYFIXEDOUTPUT so Windows preserves multi-monitor adjacency and scaling preferences.
        current.dmFields = DmPelsWidth | DmPelsHeight | DmDisplayFrequency | DmBitsPerPel | DmDisplayOrientation;

        var result = ChangeDisplaySettingsEx(deviceName, ref current, IntPtr.Zero, CdsUpdateRegistry, IntPtr.Zero);
        return result == DispChangeSuccessful || result == DispChangeRestart;
    }

    private static DEVMODE CreateDevMode() =>
        new() { dmSize = (ushort)Marshal.SizeOf<DEVMODE>() };
}
