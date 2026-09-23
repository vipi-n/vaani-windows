using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Vaani;

public partial class FloatingButton : Window
{
    public enum VState { Idle, Listening, Transcribing }

    private Ellipse _disc = null!;
    private Ellipse _rim = null!;
    private Ellipse _ring = null!;
    private readonly Rectangle[] _bars = new Rectangle[5];
    private DropShadowEffect _glow = null!;

    private VState _state = VState.Idle;
    private float _level;
    private double _pulse;
    private readonly DispatcherTimer _anim;

    private static readonly double[] BaseHeights = { 0.42, 0.72, 1.0, 0.72, 0.42 };
    private const double DiscSize = 42, MaxBarH = 20;

    public event Action? ToggleRequested;

    public FloatingButton()
    {
        InitializeComponent();
        BuildVisuals();
        UpdateVisual();

        _anim = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
        _anim.Tick += (_, _) =>
        {
            if (_state != VState.Listening) return;
            _pulse += 0.2;
            double a = 0.22 + 0.20 * Math.Sin(_pulse);
            _ring.Opacity = Math.Max(0, a);
            UpdateBars();
        };

        MouseLeftButtonDown += OnLeftDown;
        MouseRightButtonUp += OnRightUp;
        Loaded += (_, _) => RestorePosition();
    }

    private void BuildVisuals()
    {
        _glow = new DropShadowEffect { BlurRadius = 20, ShadowDepth = 0, Opacity = 0.85 };

        _disc = new Ellipse
        {
            Width = DiscSize, Height = DiscSize,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Effect = _glow
        };
        _rim = new Ellipse
        {
            Width = DiscSize, Height = DiscSize,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Stroke = new SolidColorBrush(Color.FromArgb(0x60, 0xFF, 0xFF, 0xFF)),
            StrokeThickness = 1
        };
        _ring = new Ellipse
        {
            Width = 54, Height = 54,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            StrokeThickness = 1.5,
            Visibility = Visibility.Collapsed
        };

        var barPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        for (int i = 0; i < _bars.Length; i++)
        {
            var bar = new Rectangle
            {
                Width = 3.4, RadiusX = 1.7, RadiusY = 1.7,
                Margin = new Thickness(1.6, 0, 1.6, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Fill = new SolidColorBrush(Color.FromArgb(0xFA, 0xFF, 0xFF, 0xFF)),
                Height = BaseHeights[i] * MaxBarH
            };
            _bars[i] = bar;
            barPanel.Children.Add(bar);
        }

        Root.Children.Add(_disc);
        Root.Children.Add(_rim);
        Root.Children.Add(_ring);
        Root.Children.Add(barPanel);
    }

    public void SetState(VState state)
    {
        _state = state;
        Dispatcher.Invoke(() =>
        {
            _ring.Visibility = state == VState.Listening ? Visibility.Visible : Visibility.Collapsed;
            if (state == VState.Listening) _anim.Start(); else { _anim.Stop(); ResetBars(); }
            UpdateVisual();
        });
    }

    public void UpdateLevel(float level)
    {
        _level = Math.Clamp(level, 0f, 1f);
        if (_state == VState.Listening) Dispatcher.Invoke(UpdateBars);
    }

    private void UpdateVisual()
    {
        GradientStopCollection stops;
        Color glow;
        switch (_state)
        {
            case VState.Listening:
                stops = new GradientStopCollection {
                    new GradientStop(Color.FromRgb(0xFF, 0x3B, 0x4E), 0),
                    new GradientStop(Color.FromRgb(0xFF, 0x78, 0x5E), 1) };
                glow = Color.FromRgb(0xFF, 0x3B, 0x4E);
                break;
            case VState.Transcribing:
                stops = new GradientStopCollection {
                    new GradientStop(Color.FromRgb(0xF5, 0x9E, 0x0B), 0),
                    new GradientStop(Color.FromRgb(0xF9, 0xB9, 0x59), 1) };
                glow = Color.FromRgb(0xF5, 0x9E, 0x0B);
                break;
            default:
                stops = new GradientStopCollection {
                    new GradientStop(Color.FromRgb(0x43, 0x38, 0xCA), 0),
                    new GradientStop(Color.FromRgb(0x4F, 0x7C, 0xFF), 0.55),
                    new GradientStop(Color.FromRgb(0x22, 0xD3, 0xEE), 1) };
                glow = Color.FromRgb(0x22, 0xD3, 0xEE);
                break;
        }
        _disc.Fill = new LinearGradientBrush(stops, new Point(0, 0), new Point(1, 1));
        _glow.Color = glow;
        _ring.Stroke = new SolidColorBrush(glow);
    }

    private void UpdateBars()
    {
        for (int i = 0; i < _bars.Length; i++)
        {
            double f = BaseHeights[i];
            if (_state == VState.Listening) f = Math.Min(1.0, BaseHeights[i] * (0.40 + _level * 1.25));
            _bars[i].Height = Math.Max(3.4, MaxBarH * f);
        }
    }

    private void ResetBars()
    {
        for (int i = 0; i < _bars.Length; i++)
            _bars[i].Height = BaseHeights[i] * MaxBarH;
    }

    // ---- Interaction: click vs drag ----
    private void OnLeftDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        double l0 = Left, t0 = Top;
        try { DragMove(); } catch { }
        if (Math.Abs(Left - l0) < 3 && Math.Abs(Top - t0) < 3)
            ToggleRequested?.Invoke();
        else
            SavePosition();
    }

    private void OnRightUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (ContextMenu != null) { ContextMenu.IsOpen = true; }
    }

    private void RestorePosition()
    {
        var s = App.Settings;
        var wa = SystemParameters.WorkArea;
        if (s.FloatingX >= 0 && s.FloatingY >= 0)
        {
            Left = s.FloatingX; Top = s.FloatingY;
        }
        else
        {
            Left = wa.Right - Width - 40;
            Top = wa.Bottom - Height - 60;
        }
    }

    private void SavePosition()
    {
        App.Settings.FloatingX = Left;
        App.Settings.FloatingY = Top;
        App.Settings.Save();
    }
}
