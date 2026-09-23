using System.Windows.Interop;

namespace Vaani;

/// <summary>
/// Registers a global toggle hotkey (default Ctrl+Alt+D) using a message-only
/// window to receive WM_HOTKEY. Push-to-talk (hold) would need a low-level
/// keyboard hook; the toggle + floating button + silence auto-stop cover the UX.
/// </summary>
public sealed class HotKeyManager : IDisposable
{
    private const int HotkeyId = 0xB001;
    private HwndSource? _source;
    private IntPtr _hwnd;

    public event Action? Toggled;

    public void Register()
    {
        var parameters = new HwndSourceParameters("VaaniHotKeyWindow")
        {
            Width = 0,
            Height = 0,
            WindowStyle = 0,
            ParentWindow = new IntPtr(-3) // HWND_MESSAGE (message-only window)
        };
        _source = new HwndSource(parameters);
        _source.AddHook(WndProc);
        _hwnd = _source.Handle;

        uint mods = NativeMethods.MOD_CONTROL | NativeMethods.MOD_ALT | NativeMethods.MOD_NOREPEAT;
        const uint vkD = 0x44; // 'D'
        bool ok = NativeMethods.RegisterHotKey(_hwnd, HotkeyId, mods, vkD);
        Log.Write(ok ? "Global hotkey Ctrl+Alt+D registered." : "Failed to register global hotkey.");
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_HOTKEY && wParam.ToInt32() == HotkeyId)
        {
            handled = true;
            Toggled?.Invoke();
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_hwnd != IntPtr.Zero)
            NativeMethods.UnregisterHotKey(_hwnd, HotkeyId);
        _source?.RemoveHook(WndProc);
        _source?.Dispose();
    }
}
