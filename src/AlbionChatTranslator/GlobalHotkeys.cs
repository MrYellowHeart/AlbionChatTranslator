using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace AlbionChatTranslator;

/// <summary>
/// System-wide hotkeys (they work while Albion has focus) via RegisterHotKey. The OS only
/// tells us that our key combination was pressed; no keyboard hook, no key logging.
/// </summary>
public sealed class GlobalHotkeys : IDisposable
{
    private const int WmHotkey = 0x0312;
    private const uint ModAlt = 0x1, ModControl = 0x2, ModShift = 0x4, ModNoRepeat = 0x4000;

    private readonly IntPtr _hwnd;
    private readonly HwndSource _source;
    private readonly Dictionary<int, Action> _actions = [];
    private int _nextId = 0xA100;

    public GlobalHotkeys(Window window)
    {
        _hwnd = new WindowInteropHelper(window).EnsureHandle();
        _source = HwndSource.FromHwnd(_hwnd);
        _source.AddHook(Hook);
    }

    /// <summary>Registers e.g. ("Ctrl+Shift", 'R'). Returns false if another app already owns it.</summary>
    public bool Register(bool ctrl, bool shift, bool alt, char key, Action action)
    {
        uint mods = ModNoRepeat | (ctrl ? ModControl : 0) | (shift ? ModShift : 0) | (alt ? ModAlt : 0);
        int id = _nextId++;
        if (!RegisterHotKey(_hwnd, id, mods, char.ToUpperInvariant(key))) return false;
        _actions[id] = action;
        return true;
    }

    private IntPtr Hook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmHotkey && _actions.TryGetValue(wParam.ToInt32(), out var action))
        {
            action();
            handled = true;
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        foreach (var id in _actions.Keys) UnregisterHotKey(_hwnd, id);
        _actions.Clear();
        _source.RemoveHook(Hook);
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
