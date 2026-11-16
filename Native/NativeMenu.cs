using System.Runtime.InteropServices;

namespace SpkDisplayController.Native;

/// <summary>
/// Win32 native popup menu (HMENU + TrackPopupMenu).
/// WinForms ContextMenuStrip is GDI custom-drawn and lacks system menu rounded corners,
/// shadows, and popup animations; this class builds an HMENU directly so DWM renders it
/// using the system theme.
/// </summary>
internal sealed class NativeMenu : IDisposable
{
    private const uint MF_STRING = 0x00000000;
    private const uint MF_POPUP = 0x00000010;
    private const uint MF_SEPARATOR = 0x00000800;
    private const uint MF_CHECKED = 0x00000008;
    private const uint MF_GRAYED = 0x00000001;

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

    public IntPtr AddSubMenu(IntPtr parent, string text, bool enabled)
    {
        var sub = CreatePopupMenu();
        AppendMenu(parent, MF_POPUP | (enabled ? 0u : MF_GRAYED), sub, text);
        return sub;
    }

    /// <summary>Shows the menu and returns whether the selected item's action was executed.</summary>
    /// <remarks>
    /// SetForegroundWindow must be called first, otherwise clicking outside may not dismiss the menu;
    /// PostMessage(WM_NULL) after dismissal is the classic tray icon fix, otherwise the icon
    /// briefly becomes unresponsive after the menu closes.
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
