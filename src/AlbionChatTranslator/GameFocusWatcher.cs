using System.Diagnostics;
using System.Runtime.InteropServices;

namespace AlbionChatTranslator;

/// <summary>
/// Tells the app whether Albion (or this app itself) is the window you're using, so the overlay
/// can hide and "always on top" can step back when you Alt+Tab elsewhere. Windows notifies us of
/// each foreground change; nothing is polled and no input is read.
/// </summary>
public sealed class GameFocusWatcher : IDisposable
{
    private const uint EventSystemForeground = 0x0003;
    private const uint WinEventOutOfContext = 0x0000;

    private readonly WinEventDelegate _callback; // kept alive for as long as the hook exists
    private readonly IntPtr _hook;
    private readonly int _ownPid = Environment.ProcessId;
    private readonly Dictionary<uint, bool> _isGame = [];

    public GameFocusWatcher()
    {
        _callback = OnForegroundChanged;
        _hook = SetWinEventHook(EventSystemForeground, EventSystemForeground, IntPtr.Zero, _callback, 0, 0, WinEventOutOfContext);
        IsActive = Classify(GetForegroundWindow());
    }

    /// <summary>True while Albion or this app is the foreground window.</summary>
    public bool IsActive { get; private set; }

    /// <summary>Raised on the UI thread when <see cref="IsActive"/> changes.</summary>
    public event Action<bool>? ActiveChanged;

    private void OnForegroundChanged(IntPtr hook, uint evt, IntPtr hwnd, int idObject, int idChild, uint thread, uint time)
    {
        bool active = Classify(hwnd);
        if (active == IsActive) return;
        IsActive = active;
        ActiveChanged?.Invoke(active);
    }

    private bool Classify(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return IsActive;
        GetWindowThreadProcessId(hwnd, out uint pid);
        if (pid == _ownPid) return true;
        if (_isGame.TryGetValue(pid, out bool known)) return known;

        bool game;
        try
        {
            using var p = Process.GetProcessById((int)pid);
            game = p.ProcessName.StartsWith("Albion-Online", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException)
        {
            game = false;
        }
        if (_isGame.Count > 256) _isGame.Clear();
        _isGame[pid] = game;
        return game;
    }

    public void Dispose()
    {
        if (_hook != IntPtr.Zero) UnhookWinEvent(_hook);
    }

    private delegate void WinEventDelegate(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

    [DllImport("user32.dll")]
    private static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc, WinEventDelegate lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);

    [DllImport("user32.dll")]
    private static extern bool UnhookWinEvent(IntPtr hWinEventHook);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
}
