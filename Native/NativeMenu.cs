using System.Runtime.InteropServices;

namespace SpkDisplayController.Native;

/// <summary>
/// Win32 原生弹出菜单（HMENU + TrackPopupMenu）。
/// WinForms 的 ContextMenuStrip 是 GDI 自绘控件，拿不到系统菜单的圆角、
/// 阴影与弹出动画；本类直接构造 HMENU，由 DWM 按系统主题渲染。
/// </summary>
internal sealed class NativeMenu : IDisposable
{
    // 菜单项标志
    private const uint MF_STRING = 0x00000000;
    private const uint MF_POPUP = 0x00000010;
    private const uint MF_SEPARATOR = 0x00000800;
    private const uint MF_CHECKED = 0x00000008;
    private const uint MF_GRAYED = 0x00000001;

    // TrackPopupMenu 标志
    private const uint TPM_LEFTALIGN = 0x0000;
    private const uint TPM_TOPALIGN = 0x0000;
    private const uint TPM_RIGHTBUTTON = 0x0002;
    private const uint TPM_RETURNCMD = 0x0100;

    private const uint WM_NULL = 0x0000;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr CreatePopupMenu();

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool AppendMenu(IntPtr hMenu, uint uFlags, IntPtr uIDNewItem, string? lpNewItem);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyMenu(IntPtr hMenu);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int TrackPopupMenu(
        IntPtr hMenu, uint uFlags, int x, int y, int nReserved, IntPtr hWnd, IntPtr prcRect);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    private readonly IntPtr _root;
    private readonly Dictionary<uint, Action> _actions = new();
    private uint _nextId;
    private bool _disposed;

    public NativeMenu()
    {
        _root = CreatePopupMenu();
    }

    public IntPtr Root => _root;

    /// <summary>在指定菜单下追加一个叶子项。</summary>
    public void AddItem(IntPtr menu, string text, bool enabled, bool isChecked, Action? onClick)
    {
        var flags = MF_STRING;

        if (!enabled)
            flags |= MF_GRAYED;

        if (isChecked)
            flags |= MF_CHECKED;

        uint id = 0;
        if (enabled && onClick is not null)
        {
            id = ++_nextId;
            _actions[id] = onClick;
        }

        AppendMenu(menu, flags, (IntPtr)id, text);
    }

    public void AddSeparator(IntPtr menu) =>
        AppendMenu(menu, MF_SEPARATOR, IntPtr.Zero, null);

    /// <summary>创建一个子菜单并挂到父菜单下，返回子菜单句柄。</summary>
    public IntPtr AddSubMenu(IntPtr parent, string text, bool enabled)
    {
        var sub = CreatePopupMenu();
        AppendMenu(parent, MF_POPUP | (enabled ? 0u : MF_GRAYED), sub, text);
        return sub;
    }

    /// <summary>弹出菜单，返回被选中项对应的动作是否执行。</summary>
    /// <remarks>
    /// 必须先 SetForegroundWindow，否则菜单可能不会因为点击外部而收起；
    /// 收起后再 PostMessage(WM_NULL) 是托盘图标的经典修复，
    /// 否则菜单关闭后图标会短暂失去响应。
    /// </remarks>
    public bool Show(IntPtr owner, int screenX, int screenY)
    {
        if (_disposed || _root == IntPtr.Zero)
            return false;

        SetForegroundWindow(owner);

        var cmd = TrackPopupMenu(
            _root,
            TPM_LEFTALIGN | TPM_TOPALIGN | TPM_RIGHTBUTTON | TPM_RETURNCMD,
            screenX, screenY, 0, owner, IntPtr.Zero);

        PostMessage(owner, WM_NULL, IntPtr.Zero, IntPtr.Zero);

        if (cmd <= 0)
            return false;

        if (_actions.TryGetValue((uint)cmd, out var action))
        {
            action();
            return true;
        }

        return false;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        if (_root != IntPtr.Zero)
            DestroyMenu(_root);
    }
}
