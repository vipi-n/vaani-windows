using System.Media;

namespace Vaani;

/// <summary>Subtle start/stop cues, respecting the user's sound setting.</summary>
public static class Sounds
{
    public static void Start() { if (App.Settings.SoundEnabled) SafePlay(SystemSounds.Asterisk); }
    public static void Stop() { if (App.Settings.SoundEnabled) SafePlay(SystemSounds.Exclamation); }
    public static void Cancel() { /* silent */ }

    private static void SafePlay(SystemSound s)
    {
        try { s.Play(); } catch { }
    }
}
