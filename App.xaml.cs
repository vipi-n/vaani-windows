using System.Windows;
using WinForms = System.Windows.Forms;

namespace Vaani;

public partial class App : Application
{
    public static Settings Settings { get; private set; } = new();

    private readonly AudioRecorder _recorder = new();
    private Transcriber _transcriber = null!;
    private readonly HotKeyManager _hotkey = new();
    private FloatingButton _floating = null!;
    private WinForms.NotifyIcon _tray = null!;

    private enum State { Idle, Listening, Transcribing }
    private State _state = State.Idle;

    private void OnStartup(object sender, StartupEventArgs e)
    {
        Settings = Settings.Load();
        Log.Write("=== Vaani (Windows) launched ===");

        _transcriber = new Transcriber();
        if (!_transcriber.IsAvailable)
        {
            MessageBox.Show(
                "Vaani couldn't find its speech model (ggml-small.bin) next to the app. " +
                "Please reinstall.", "Vaani", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        _floating = new FloatingButton();
        _floating.ToggleRequested += ToggleDictation;
        BuildFloatingMenu();
        if (Settings.FloatingVisible) _floating.Show();

        _recorder.LevelChanged += level => _floating.UpdateLevel(level);
        _recorder.AutoStopped += () => Dispatcher.Invoke(() => { if (_state == State.Listening) _ = FinishListeningAsync(); });

        _hotkey.Toggled += ToggleDictation;
        _hotkey.Register();

        SetupTray();
        SetState(State.Idle);
    }

    // ---- Dictation flow ----

    private void ToggleDictation()
    {
        switch (_state)
        {
            case State.Idle: BeginListening(); break;
            case State.Listening: _ = FinishListeningAsync(); break;
            case State.Transcribing: break;
        }
    }

    private void BeginListening()
    {
        if (_state != State.Idle) return;
        try
        {
            _recorder.AutoStopOnSilence = true; // toggle / hands-free
            _recorder.Start();
            SetState(State.Listening);
            Sounds.Start();
        }
        catch (Exception ex)
        {
            Log.Write($"BeginListening failed: {ex.Message}");
            _tray.ShowBalloonTip(4000, "Vaani",
                "Couldn't access the microphone. Check Windows mic privacy settings.",
                WinForms.ToolTipIcon.Warning);
        }
    }

    private async Task FinishListeningAsync()
    {
        if (_state != State.Listening) return;

        float peak = _recorder.LastPeak;
        var wav = _recorder.StopAndGetWav();
        if (wav == null)
        {
            SetState(State.Idle);
            Sounds.Cancel();
            return;
        }

        if (peak < 0.02f)
        {
            Log.Write($"Peak {peak} below gate; skipping transcription.");
            SetState(State.Idle);
            Sounds.Cancel();
            return;
        }

        Sounds.Stop();
        SetState(State.Transcribing);

        string? text = await _transcriber.TranscribeAsync(wav, Settings.Language);

        SetState(State.Idle);
        if (!string.IsNullOrWhiteSpace(text))
        {
            var output = text.EndsWith(" ") ? text : text + " ";
            Log.Write($"Inserting {output.Length} chars via {Settings.InjectionMode}.");
            TextInjector.Insert(output, Settings.InjectionMode);
        }
    }

    private void SetState(State state)
    {
        _state = state;
        _floating.SetState(state switch
        {
            State.Listening => FloatingButton.VState.Listening,
            State.Transcribing => FloatingButton.VState.Transcribing,
            _ => FloatingButton.VState.Idle
        });
        UpdateTrayIcon();
    }

    // ---- Tray ----

    private void SetupTray()
    {
        _tray = new WinForms.NotifyIcon
        {
            Visible = true,
            Text = "Vaani — press Ctrl+Alt+D to dictate"
        };
        _tray.DoubleClick += (_, _) => ToggleDictation();
        UpdateTrayIcon();
        BuildTrayMenu();
    }

    private void UpdateTrayIcon()
    {
        var tint = _state switch
        {
            State.Listening => System.Drawing.Color.FromArgb(255, 255, 59, 78),
            State.Transcribing => System.Drawing.Color.FromArgb(255, 245, 158, 11),
            _ => System.Drawing.Color.FromArgb(255, 90, 170, 240)
        };
        if (_tray != null) _tray.Icon = TrayIconFactory.Create(tint);
    }

    private void BuildTrayMenu()
    {
        var menu = new WinForms.ContextMenuStrip();

        var toggle = new WinForms.ToolStripMenuItem(_state == State.Listening ? "Stop Dictation" : "Start Dictation",
            null, (_, _) => ToggleDictation());
        menu.Items.Add(toggle);
        menu.Items.Add("Shortcut: Ctrl+Alt+D") .Enabled = false;
        menu.Items.Add(new WinForms.ToolStripSeparator());

        var lang = new WinForms.ToolStripMenuItem("Language");
        foreach (var (code, name) in Settings.Languages)
        {
            var item = new WinForms.ToolStripMenuItem(name, null, (_, _) =>
            {
                Settings.Language = code; Settings.Save(); BuildTrayMenu(); BuildFloatingMenu();
            }) { Checked = Settings.Language == code };
            lang.DropDownItems.Add(item);
        }
        menu.Items.Add(lang);

        var sound = new WinForms.ToolStripMenuItem("Sound Effects", null, (_, _) =>
        {
            Settings.SoundEnabled = !Settings.SoundEnabled; Settings.Save(); BuildTrayMenu(); BuildFloatingMenu();
        }) { Checked = Settings.SoundEnabled };
        menu.Items.Add(sound);

        var floatItem = new WinForms.ToolStripMenuItem("Show Floating Button", null, (_, _) => ToggleFloating())
        { Checked = Settings.FloatingVisible };
        menu.Items.Add(floatItem);

        menu.Items.Add(new WinForms.ToolStripSeparator());
        menu.Items.Add("Open Debug Log", null, (_, _) => OpenLog());
        menu.Items.Add("Quit Vaani", null, (_, _) => Shutdown());

        _tray.ContextMenuStrip = menu;
    }

    private void BuildFloatingMenu()
    {
        var menu = new System.Windows.Controls.ContextMenu();

        var toggle = new System.Windows.Controls.MenuItem { Header = _state == State.Listening ? "Stop Dictation" : "Start Dictation" };
        toggle.Click += (_, _) => ToggleDictation();
        menu.Items.Add(toggle);
        menu.Items.Add(new System.Windows.Controls.Separator());

        var sound = new System.Windows.Controls.MenuItem { Header = "Sound Effects", IsChecked = Settings.SoundEnabled };
        sound.Click += (_, _) => { Settings.SoundEnabled = !Settings.SoundEnabled; Settings.Save(); BuildTrayMenu(); BuildFloatingMenu(); };
        menu.Items.Add(sound);

        var hide = new System.Windows.Controls.MenuItem { Header = "Hide Floating Button" };
        hide.Click += (_, _) => ToggleFloating();
        menu.Items.Add(hide);

        var log = new System.Windows.Controls.MenuItem { Header = "Open Debug Log" };
        log.Click += (_, _) => OpenLog();
        menu.Items.Add(log);

        menu.Items.Add(new System.Windows.Controls.Separator());
        var quit = new System.Windows.Controls.MenuItem { Header = "Quit Vaani" };
        quit.Click += (_, _) => Shutdown();
        menu.Items.Add(quit);

        _floating.ContextMenu = menu;
    }

    private void ToggleFloating()
    {
        Settings.FloatingVisible = !Settings.FloatingVisible;
        Settings.Save();
        if (Settings.FloatingVisible) _floating.Show(); else _floating.Hide();
        BuildTrayMenu();
        BuildFloatingMenu();
    }

    private void OpenLog()
    {
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Log.FilePath) { UseShellExecute = true }); }
        catch { }
    }

    private void OnExit(object sender, ExitEventArgs e)
    {
        try { _hotkey.Dispose(); } catch { }
        try { _transcriber.Dispose(); } catch { }
        if (_tray != null) { _tray.Visible = false; _tray.Dispose(); }
    }
}
