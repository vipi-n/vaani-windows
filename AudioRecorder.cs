using System.IO;
using NAudio.Wave;

namespace Vaani;

/// <summary>
/// Captures microphone audio at 16 kHz mono (what Whisper expects) using NAudio,
/// tracks input level + peak, and can auto-stop after a stretch of silence.
/// </summary>
public sealed class AudioRecorder
{
    private WaveInEvent? _waveIn;
    private MemoryStream? _pcm;                 // raw 16-bit PCM samples
    private readonly object _gate = new();

    public bool IsRecording { get; private set; }
    public float LastPeak { get; private set; }

    /// <summary>0..1 live input level (on a background thread).</summary>
    public event Action<float>? LevelChanged;
    /// <summary>Raised when silence auto-stop triggers.</summary>
    public event Action? AutoStopped;

    public bool AutoStopOnSilence { get; set; }

    // Tuning (mirrors the macOS build).
    private const float VoiceThreshold = 0.012f;
    private const double SilenceTimeoutSec = 2.5;
    private const double MaxRecordingSec = 60.0;

    private bool _heardVoice;
    private DateTime _lastVoice;
    private DateTime _start;
    private bool _autoStopFired;

    public void Start()
    {
        if (IsRecording) return;

        _pcm = new MemoryStream();
        LastPeak = 0;
        _heardVoice = false;
        _autoStopFired = false;
        _start = DateTime.UtcNow;
        _lastVoice = _start;

        _waveIn = new WaveInEvent
        {
            WaveFormat = new WaveFormat(16000, 16, 1),
            BufferMilliseconds = 50
        };
        _waveIn.DataAvailable += OnData;
        _waveIn.StartRecording();
        IsRecording = true;
        Log.Write($"Recorder started (autoStopOnSilence={AutoStopOnSilence}).");
    }

    private void OnData(object? sender, WaveInEventArgs e)
    {
        lock (_gate) { _pcm?.Write(e.Buffer, 0, e.BytesRecorded); }

        // Compute RMS + peak from 16-bit samples.
        double sumSq = 0;
        float peak = 0;
        int samples = e.BytesRecorded / 2;
        for (int i = 0; i < e.BytesRecorded; i += 2)
        {
            short s = (short)(e.Buffer[i] | (e.Buffer[i + 1] << 8));
            float v = s / 32768f;
            sumSq += v * v;
            float a = Math.Abs(v);
            if (a > peak) peak = a;
        }
        float rms = samples > 0 ? (float)Math.Sqrt(sumSq / samples) : 0;
        if (peak > LastPeak) LastPeak = peak;

        var now = DateTime.UtcNow;
        if (rms > VoiceThreshold) { _heardVoice = true; _lastVoice = now; }

        if (!_autoStopFired)
        {
            bool silence = AutoStopOnSilence && _heardVoice &&
                           (now - _lastVoice).TotalSeconds > SilenceTimeoutSec;
            bool tooLong = (now - _start).TotalSeconds > MaxRecordingSec;
            if (silence || tooLong)
            {
                _autoStopFired = true;
                AutoStopped?.Invoke();
            }
        }

        LevelChanged?.Invoke(Math.Min(1f, rms * 18f));
    }

    /// <summary>Stops and returns a 16 kHz mono WAV as a byte array (or null if too short).</summary>
    public byte[]? StopAndGetWav()
    {
        if (!IsRecording) return null;
        IsRecording = false;

        try { _waveIn!.StopRecording(); } catch { }
        _waveIn!.DataAvailable -= OnData;
        _waveIn.Dispose();
        _waveIn = null;

        byte[] pcm;
        lock (_gate) { pcm = _pcm?.ToArray() ?? Array.Empty<byte>(); }
        _pcm?.Dispose();
        _pcm = null;

        double seconds = pcm.Length / 2.0 / 16000.0;
        Log.Write($"Recorder stopped: ~{seconds:0.00}s, peak={LastPeak:0.0000}, heardVoice={_heardVoice}");
        if (pcm.Length < 16000) return null; // < ~0.5s

        return WrapWav(pcm, 16000, 1, 16);
    }

    private static byte[] WrapWav(byte[] pcm, int sampleRate, short channels, short bits)
    {
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);
        int byteRate = sampleRate * channels * bits / 8;
        short blockAlign = (short)(channels * bits / 8);

        bw.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
        bw.Write(36 + pcm.Length);
        bw.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
        bw.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
        bw.Write(16);
        bw.Write((short)1);              // PCM
        bw.Write(channels);
        bw.Write(sampleRate);
        bw.Write(byteRate);
        bw.Write(blockAlign);
        bw.Write(bits);
        bw.Write(System.Text.Encoding.ASCII.GetBytes("data"));
        bw.Write(pcm.Length);
        bw.Write(pcm);
        bw.Flush();
        return ms.ToArray();
    }
}
