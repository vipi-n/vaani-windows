using System.IO;

namespace Vaani;

/// <summary>Minimal file logger at %APPDATA%\Vaani\vaani.log for diagnosing issues.</summary>
public static class Log
{
    private static readonly object Gate = new();
    private static readonly string Dir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Vaani");
    public static string FilePath => Path.Combine(Dir, "vaani.log");

    public static void Write(string message)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(Dir);
                File.AppendAllText(FilePath, $"[{DateTime.Now:HH:mm:ss.fff}] {message}{Environment.NewLine}");
            }
        }
        catch { /* ignore logging failures */ }
    }
}
