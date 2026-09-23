using System.IO;
using System.Text.Json;

namespace Vaani;

/// <summary>User settings persisted to %APPDATA%\Vaani\settings.json.</summary>
public sealed class Settings
{
    public string Language { get; set; } = "auto";        // "auto" handles Hinglish
    public bool SoundEnabled { get; set; } = true;
    public bool FloatingVisible { get; set; } = true;
    public double FloatingX { get; set; } = -1;            // -1 = not set yet
    public double FloatingY { get; set; } = -1;
    public string InjectionMode { get; set; } = "paste";  // "paste" | "type"

    private static string Dir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Vaani");
    private static string FilePath => Path.Combine(Dir, "settings.json");

    public static Settings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var json = File.ReadAllText(FilePath);
                return JsonSerializer.Deserialize<Settings>(json) ?? new Settings();
            }
        }
        catch { /* fall through to defaults */ }
        return new Settings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Dir);
            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(FilePath, json);
        }
        catch { /* best effort */ }
    }

    public static readonly (string Code, string Name)[] Languages =
    {
        ("auto", "Auto-detect (Hinglish)"),
        ("en", "English"), ("hi", "Hindi"), ("es", "Spanish"), ("fr", "French"),
        ("de", "German"), ("it", "Italian"), ("pt", "Portuguese"), ("ru", "Russian"),
        ("ja", "Japanese"), ("ko", "Korean"), ("zh", "Chinese"), ("ar", "Arabic"),
        ("bn", "Bengali"), ("ta", "Tamil"), ("te", "Telugu"), ("mr", "Marathi"),
        ("gu", "Gujarati"), ("pa", "Punjabi"), ("ur", "Urdu")
    };
}
