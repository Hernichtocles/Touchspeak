using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace TouchSpeak.Services;

/// <summary>
/// Verweil-Klick (Dwell / Hover-to-Click) für die Steuerung per Kopfmaus
/// (z. B. Orin HeadMouse Nano). Bleibt der Cursor lange genug über einer
/// Schaltfläche, wird deren Klick ausgelöst. Ein Fortschrittsring am Cursor
/// zeigt die verbleibende Zeit. Pro Ziel wird nur einmal geklickt – erst wenn
/// der Cursor das Element verlässt, kann es erneut ausgelöst werden.
/// </summary>
public sealed class DwellClicker : IDisposable
{
    private const double Radius = 22;
    private const double Thickness = 6;

    private readonly Window _window;
    private readonly Canvas _overlay;
    private readonly DispatcherTimer _timer;
    private readonly Ellipse _track;   // schwacher voller Kreis (Hintergrund)
    private readonly Path _arc;        // gefüllter Fortschrittsbogen

    private ButtonBase? _target;
    private long _dwellStartTicks;
    private bool _firedForCurrentTarget;

    public bool Enabled { get; set; }
    public double DwellSeconds { get; set; } = 1.2;

    public DwellClicker(Window window, Canvas overlay)
    {
        _window = window;
        _overlay = overlay;

        var accent = (Application.Current?.TryFindResource("AccentBackground") as Brush)
                     ?? new SolidColorBrush(Color.FromRgb(0x4C, 0x8B, 0xF5));

        _track = new Ellipse
        {
            Width = Radius * 2,
            Height = Radius * 2,
            Stroke = new SolidColorBrush(Color.FromArgb(0x66, 0xFF, 0xFF, 0xFF)),
            StrokeThickness = Thickness,
            IsHitTestVisible = false,
            Visibility = Visibility.Collapsed
        };
        _arc = new Path
        {
            Stroke = accent,
            StrokeThickness = Thickness,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            IsHitTestVisible = false,
            Visibility = Visibility.Collapsed
        };
        _overlay.Children.Add(_track);
        _overlay.Children.Add(_arc);

        _timer = new DispatcherTimer(DispatcherPriority.Input)
        {
            Interval = TimeSpan.FromMilliseconds(33) // ~30 fps
        };
        _timer.Tick += OnTick;
        _timer.Start();
    }

    private void OnTick(object? sender, EventArgs e)
    {
        if (!Enabled || !_window.IsActive)
        {
            Reset();
            return;
        }

        if (!GetCursorPos(out var pt))
        {
            Reset();
            return;
        }

        var screen = new Point(pt.X, pt.Y);
        Point inWindow, inOverlay;
        try
        {
            inWindow = _window.PointFromScreen(screen);   // physische Pixel -> DIPs (DPI-sicher)
            inOverlay = _overlay.PointFromScreen(screen);
        }
        catch
        {
            Reset();
            return;
        }

        var hit = _window.InputHitTest(inWindow) as DependencyObject;
        var button = FindButton(hit);

        if (button != _target)
        {
            _target = button;
            _dwellStartTicks = Stopwatch.GetTimestamp();
            _firedForCurrentTarget = false;
        }

        if (_target == null || _firedForCurrentTarget)
        {
            HideRing();
            return;
        }

        double elapsed = (Stopwatch.GetTimestamp() - _dwellStartTicks) / (double)Stopwatch.Frequency;
        double frac = Math.Clamp(elapsed / Math.Max(0.1, DwellSeconds), 0, 1);
        ShowRing(inOverlay, frac);

        if (frac >= 1.0)
        {
            _firedForCurrentTarget = true;
            HideRing();
            InvokeClick(_target);
        }
    }

    /// <summary>Sucht ab dem getroffenen Visual aufwärts die erste klickbare Schaltfläche.</summary>
    private static ButtonBase? FindButton(DependencyObject? node)
    {
        while (node != null)
        {
            if (node is ButtonBase b && b.IsEnabled && b.IsVisible)
                return b;
            node = VisualTreeHelper.GetParent(node);
        }
        return null;
    }

    /// <summary>Löst die Aktion der Schaltfläche aus (Click, Toggle bzw. RadioButton-Auswahl).</summary>
    private static void InvokeClick(ButtonBase b)
    {
        var peer = UIElementAutomationPeer.CreatePeerForElement(b);
        if (peer?.GetPattern(PatternInterface.Invoke) is IInvokeProvider invoke)
        {
            invoke.Invoke();
            return;
        }
        if (peer?.GetPattern(PatternInterface.SelectionItem) is ISelectionItemProvider select)
        {
            select.Select();
            return;
        }
        if (peer?.GetPattern(PatternInterface.Toggle) is IToggleProvider toggle)
        {
            toggle.Toggle();
            return;
        }
        b.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, b));
    }

    private void ShowRing(Point center, double frac)
    {
        Canvas.SetLeft(_track, center.X - Radius);
        Canvas.SetTop(_track, center.Y - Radius);
        _track.Visibility = Visibility.Visible;

        _arc.Data = BuildArc(center, Radius, frac);
        _arc.Visibility = Visibility.Visible;
    }

    private void HideRing()
    {
        _track.Visibility = Visibility.Collapsed;
        _arc.Visibility = Visibility.Collapsed;
    }

    private void Reset()
    {
        _target = null;
        _firedForCurrentTarget = false;
        HideRing();
    }

    private static Geometry BuildArc(Point c, double r, double frac)
    {
        if (frac <= 0) return Geometry.Empty;
        if (frac >= 1) frac = 0.9999; // voller Kreis lässt sich nicht als ein Bogen zeichnen

        const double start = -90; // oben beginnen
        double end = start + 360 * frac;
        var p0 = PointOnCircle(c, r, start);
        var p1 = PointOnCircle(c, r, end);
        bool isLarge = frac > 0.5;

        var figure = new PathFigure { StartPoint = p0, IsClosed = false };
        figure.Segments.Add(new ArcSegment(p1, new Size(r, r), 0, isLarge,
                                           SweepDirection.Clockwise, true));
        var geo = new PathGeometry();
        geo.Figures.Add(figure);
        geo.Freeze();
        return geo;
    }

    private static Point PointOnCircle(Point c, double r, double degrees)
    {
        double rad = degrees * Math.PI / 180.0;
        return new Point(c.X + r * Math.Cos(rad), c.Y + r * Math.Sin(rad));
    }

    public void Dispose()
    {
        _timer.Stop();
        _timer.Tick -= OnTick;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out POINT lpPoint);
}
