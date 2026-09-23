using System.Windows;
using System.Windows.Threading;

namespace Vaani;

/// <summary>Inserts transcribed text into whatever window has focus.</summary>
public static class TextInjector
{
    public static void Insert(string text, string mode)
    {
        if (string.IsNullOrEmpty(text)) return;
        if (mode == "type") TypeUnicode(text);
        else Paste(text);
    }

    // ---- Clipboard + Ctrl+V (reliable, incl. Devanagari) ----
    private static void Paste(string text)
    {
        string? previous = null;
        try { if (Clipboard.ContainsText()) previous = Clipboard.GetText(); } catch { }

        try { Clipboard.SetText(text); } catch { TypeUnicode(text); return; }

        SendCtrlV();

        // Restore the user's previous clipboard shortly after.
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            try
            {
                if (previous != null) Clipboard.SetText(previous);
                else Clipboard.Clear();
            }
            catch { }
        };
        timer.Start();
    }

    private static void SendCtrlV()
    {
        var inputs = new NativeMethods.INPUT[4];
        inputs[0] = KeyDown(NativeMethods.VK_CONTROL);
        inputs[1] = KeyDown(NativeMethods.VK_V);
        inputs[2] = KeyUp(NativeMethods.VK_V);
        inputs[3] = KeyUp(NativeMethods.VK_CONTROL);
        NativeMethods.SendInput((uint)inputs.Length, inputs, System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.INPUT>());
    }

    // ---- Direct Unicode typing (no clipboard) ----
    private static void TypeUnicode(string text)
    {
        foreach (char c in text)
        {
            var inputs = new[] { UnicodeDown(c), UnicodeUp(c) };
            NativeMethods.SendInput((uint)inputs.Length, inputs, System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.INPUT>());
        }
    }

    private static NativeMethods.INPUT KeyDown(ushort vk) => new()
    {
        type = NativeMethods.INPUT_KEYBOARD,
        U = new NativeMethods.InputUnion { ki = new NativeMethods.KEYBDINPUT { wVk = vk } }
    };

    private static NativeMethods.INPUT KeyUp(ushort vk) => new()
    {
        type = NativeMethods.INPUT_KEYBOARD,
        U = new NativeMethods.InputUnion { ki = new NativeMethods.KEYBDINPUT { wVk = vk, dwFlags = NativeMethods.KEYEVENTF_KEYUP } }
    };

    private static NativeMethods.INPUT UnicodeDown(char c) => new()
    {
        type = NativeMethods.INPUT_KEYBOARD,
        U = new NativeMethods.InputUnion { ki = new NativeMethods.KEYBDINPUT { wScan = c, dwFlags = NativeMethods.KEYEVENTF_UNICODE } }
    };

    private static NativeMethods.INPUT UnicodeUp(char c) => new()
    {
        type = NativeMethods.INPUT_KEYBOARD,
        U = new NativeMethods.InputUnion { ki = new NativeMethods.KEYBDINPUT { wScan = c, dwFlags = NativeMethods.KEYEVENTF_UNICODE | NativeMethods.KEYEVENTF_KEYUP } }
    };
}
