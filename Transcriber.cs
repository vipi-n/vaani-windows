using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Whisper.net;

namespace Vaani;

/// <summary>Runs Whisper.net on a WAV buffer and returns cleaned text.</summary>
public sealed class Transcriber : IDisposable
{
    private readonly WhisperFactory? _factory;
    public bool IsAvailable => _factory != null;

    public Transcriber()
    {
        var modelPath = FindModel();
        if (modelPath != null && File.Exists(modelPath))
        {
            _factory = WhisperFactory.FromPath(modelPath);
            Log.Write($"Whisper model loaded: {modelPath}");
        }
        else
        {
            Log.Write("Whisper model NOT found next to the executable.");
        }
    }

    private static string? FindModel()
    {
        var baseDir = AppContext.BaseDirectory;
        foreach (var name in new[] { "ggml-small.bin", "ggml-base.bin", "ggml-medium.bin" })
        {
            var p = Path.Combine(baseDir, name);
            if (File.Exists(p)) return p;
        }
        // Fallback: any ggml-*.bin
        var found = Directory.GetFiles(baseDir, "ggml-*.bin");
        return found.Length > 0 ? found[0] : null;
    }

    public async Task<string?> TranscribeAsync(byte[] wav, string language)
    {
        if (_factory == null) return null;
        try
        {
            using var processor = _factory.CreateBuilder()
                .WithLanguage(string.IsNullOrEmpty(language) ? "auto" : language)
                .Build();

            var sb = new StringBuilder();
            using var stream = new MemoryStream(wav);
            await foreach (var segment in processor.ProcessAsync(stream))
                sb.Append(segment.Text);

            var raw = sb.ToString().Trim();
            var cleaned = StripNonSpeech(raw);
            if (IsHallucination(cleaned)) { cleaned = string.Empty; }

            Log.Write($"Transcriber: raw=\"{Truncate(raw)}\" -> final=\"{Truncate(cleaned)}\"");
            return string.IsNullOrWhiteSpace(cleaned) ? null : cleaned;
        }
        catch (Exception ex)
        {
            Log.Write($"Transcriber error: {ex.Message}");
            return null;
        }
    }

    private static string Truncate(string s) => s.Length > 160 ? s[..160] : s;

    private static string StripNonSpeech(string input)
    {
        var s = input;
        foreach (var p in new[] { @"\[.*?\]", @"\(.*?\)", @"\*.*?\*" })
            s = Regex.Replace(s, p, "");
        return s.Trim();
    }

    private static readonly HashSet<string> Hallucinations = new(StringComparer.OrdinalIgnoreCase)
    {
        "thank you", "thank you.", "thanks for watching", "thanks for watching!",
        "thank you for watching", "please subscribe", "subscribe", "you", "you.",
        "bye", "bye.", "okay", "okay.", "ok", "yeah", "so", ".", "!", "?", "...",
        "the", "music", "applause", "silence", "no", "yes", "hmm", "mm", "uh",
        "i'll see you next time", "see you next time"
    };

    private static bool IsHallucination(string text)
    {
        var t = text.Trim();
        return t.Length == 0 || Hallucinations.Contains(t);
    }

    public void Dispose() => _factory?.Dispose();
}
