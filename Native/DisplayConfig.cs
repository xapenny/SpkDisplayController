using System.Runtime.InteropServices;

namespace SpkDisplayController.Native;

/// <summary>
/// DisplayConfig 互操作：枚举输出目标、读写 HDR（advanced color）状态。
/// </summary>
internal static class DisplayConfig
{
    // QueryDisplayConfig flags
    private const uint QDC_ALL_PATHS = 1;
    private const uint QDC_ONLY_ACTIVE_PATHS = 2;
    private const uint QDC_DATABASE_CURRENT = 4;

    // DisplayConfigGetDeviceInfo / SetDeviceInfo types
    private const uint GET_SOURCE_NAME = 1;
    private const uint GET_TARGET_NAME = 2;
    private const uint GET_ADVANCED_COLOR_INFO = 9;
    private const uint SET_ADVANCED_COLOR_STATE_TYPE = 10;

    // ---------------------------------------------------------------- 结构

    [StructLayout(LayoutKind.Sequential)]
    public struct LUID
    {
        public uint LowPart;
        public int HighPart;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PATH_SOURCE_INFO
    {
        public LUID adapterId;
        public uint id;
        public uint modeInfoIdx;
        public uint statusFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PATH_TARGET_INFO
    {
        public LUID adapterId;
        public uint id;
        public uint modeInfoIdx;
        public uint outputTechnology;
        public uint rotation;
        public uint scaling;
        public uint refreshRateNumerator;
        public uint refreshRateDenominator;
        public uint scanLineOrdering;
        public int targetAvailable;
        public uint statusFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PATH_INFO
    {
        public PATH_SOURCE_INFO sourceInfo;
        public PATH_TARGET_INFO targetInfo;
        public uint flags;
    }

    // union 最大成员 DISPLAYCONFIG_TARGET_MODE = 48 字节
    [StructLayout(LayoutKind.Sequential)]
    private struct MODE_INFO
    {
        public uint infoType;
        public uint id;
        public LUID adapterId;
        public ulong a, b, c, d, e, f;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DEVICE_INFO_HEADER
    {
        public uint type;
        public uint size;
        public LUID adapterId;
        public uint id;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct TARGET_DEVICE_NAME
    {
        public DEVICE_INFO_HEADER header;
        public uint flags;
        public uint outputTechnology;
        public ushort edidManufactureId;
        public ushort edidProductCodeId;
        public uint connectorInstance;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string monitorFriendlyDeviceName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string monitorDevicePath;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SOURCE_DEVICE_NAME
    {
        public DEVICE_INFO_HEADER header;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string viewGdiDeviceName;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ADVANCED_COLOR_INFO
    {
        public DEVICE_INFO_HEADER header;
        public uint value;
        public uint colorEncoding;
        public uint bitsPerColorChannel;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SET_ADVANCED_COLOR_STATE
    {
        public DEVICE_INFO_HEADER header;
        public uint value;
    }

    // ---------------------------------------------------------------- P/Invoke

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetDisplayConfigBufferSizes(uint flags, out uint numPath, out uint numMode);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int QueryDisplayConfig(
        uint flags,
        ref uint numPath,
        [Out] PATH_INFO[] paths,
        ref uint numMode,
        [Out] MODE_INFO[] modes,
        out uint currentTopology);

    [DllImport("user32.dll", EntryPoint = "DisplayConfigGetDeviceInfo", SetLastError = true)]
    private static extern int GetSourceName(ref SOURCE_DEVICE_NAME info);

    [DllImport("user32.dll", EntryPoint = "DisplayConfigGetDeviceInfo", SetLastError = true)]
    private static extern int GetTargetName(ref TARGET_DEVICE_NAME info);

    [DllImport("user32.dll", EntryPoint = "DisplayConfigGetDeviceInfo", SetLastError = true)]
    private static extern int GetAdvancedColorInfo(ref ADVANCED_COLOR_INFO info);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int DisplayConfigSetDeviceInfo(ref SET_ADVANCED_COLOR_STATE info);

    // ---------------------------------------------------------------- 模型

    public sealed class TargetInfo
    {
        public LUID AdapterId { get; init; }
        public uint TargetId { get; init; }
        public uint OutputTechnology { get; init; }
        public string FriendlyName { get; init; } = string.Empty;
        public string MonitorDevicePath { get; init; } = string.Empty;

        /// <summary>该 target 所属 source 的 GDI 设备名，例如 \\.\DISPLAY1。</summary>
        public string GdiDeviceName { get; init; } = string.Empty;

        /// <summary>规范化成 DISPLAY\ACR0724\5&amp;... 形式，便于与 GDI DeviceID 比对。</summary>
        public string NormalizedPath => NormalizePath(MonitorDevicePath);
    }

    public static string NormalizePath(string raw)
    {
        if (string.IsNullOrEmpty(raw))
            return string.Empty;

        var s = raw;
        if (s.StartsWith(@"\\?\", StringComparison.Ordinal))
            s = s.Substring(4);

        var hashGuid = s.IndexOf("#{", StringComparison.Ordinal);
        if (hashGuid > 0)
            s = s.Substring(0, hashGuid);

        return s.Replace('#', '\\');
    }

    // ---------------------------------------------------------------- 查询

    /// <summary>
    /// 枚举当前活动的输出目标。
    /// QDC_ONLY_ACTIVE_PATHS 在部分显卡驱动上返回 ERROR_INVALID_PARAMETER，
    /// 因此按 QDC_ONLY_ACTIVE_PATHS → QDC_DATABASE_CURRENT → QDC_ALL_PATHS 依次回退。
    /// </summary>
    public static List<TargetInfo> GetTargets()
    {
        foreach (var flags in new[] { QDC_ONLY_ACTIVE_PATHS, QDC_DATABASE_CURRENT, QDC_ALL_PATHS })
        {
            var result = TryQuery(flags);
            if (result is { Count: > 0 })
                return result;
        }

        return new List<TargetInfo>();
    }

    private static List<TargetInfo>? TryQuery(uint flags)
    {
        uint numPath = 0;
        uint numMode = 0;

        if (GetDisplayConfigBufferSizes(flags, out numPath, out numMode) != 0)
            return null;

        if (numPath == 0)
            return null;

        var paths = new PATH_INFO[numPath];
        var modes = new MODE_INFO[Math.Max(numMode, 1u)];

        var hr = QueryDisplayConfig(flags, ref numPath, paths, ref numMode, modes, out _);
        if (hr != 0)
            return null;

        var list = new List<TargetInfo>((int)numPath);

        for (var i = 0; i < numPath; i++)
        {
            var path = paths[i];
            var source = path.sourceInfo;
            var target = path.targetInfo;

            // source → \\.\DISPLAYn
            var sourceName = new SOURCE_DEVICE_NAME
            {
                header = new DEVICE_INFO_HEADER
                {
                    type = GET_SOURCE_NAME,
                    size = (uint)Marshal.SizeOf<SOURCE_DEVICE_NAME>(),
                    adapterId = source.adapterId,
                    id = source.id
                }
            };
            var sourceOk = GetSourceName(ref sourceName) == 0;

            // target → 显示器路径与友好名
            var name = new TARGET_DEVICE_NAME
            {
                header = new DEVICE_INFO_HEADER
                {
                    type = GET_TARGET_NAME,
                    size = (uint)Marshal.SizeOf<TARGET_DEVICE_NAME>(),
                    adapterId = target.adapterId,
                    id = target.id
                }
            };
            var nameOk = GetTargetName(ref name) == 0;

            list.Add(new TargetInfo
            {
                AdapterId = target.adapterId,
                TargetId = target.id,
                OutputTechnology = target.outputTechnology,
                FriendlyName = nameOk ? name.monitorFriendlyDeviceName ?? string.Empty : string.Empty,
                MonitorDevicePath = nameOk ? name.monitorDevicePath ?? string.Empty : string.Empty,
                GdiDeviceName = sourceOk ? sourceName.viewGdiDeviceName ?? string.Empty : string.Empty
            });
        }

        return list;
    }

    /// <summary>读取 HDR 支持与启用状态。</summary>
    public static bool TryGetHdr(TargetInfo target, out bool supported, out bool enabled)
    {
        supported = false;
        enabled = false;

        var info = new ADVANCED_COLOR_INFO
        {
            header = new DEVICE_INFO_HEADER
            {
                type = GET_ADVANCED_COLOR_INFO,
                size = (uint)Marshal.SizeOf<ADVANCED_COLOR_INFO>(),
                adapterId = target.AdapterId,
                id = target.TargetId
            }
        };

        if (GetAdvancedColorInfo(ref info) != 0)
            return false;

        supported = (info.value & 0x1) != 0;
        enabled = (info.value & 0x2) != 0;
        return true;
    }

    /// <summary>设置 HDR 开关。</summary>
    public static bool SetHdr(TargetInfo target, bool enable)
    {
        var state = new SET_ADVANCED_COLOR_STATE
        {
            header = new DEVICE_INFO_HEADER
            {
                type = SET_ADVANCED_COLOR_STATE_TYPE,
                size = (uint)Marshal.SizeOf<SET_ADVANCED_COLOR_STATE>(),
                adapterId = target.AdapterId,
                id = target.TargetId
            },
            value = enable ? 1u : 0u
        };

        return DisplayConfigSetDeviceInfo(ref state) == 0;
    }
}
