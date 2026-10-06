using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Cam4TrackingMonitor.Models;

namespace Cam4TrackingMonitor.Controls;

public enum TelemetryChartKind { Serial, Sync, FreeD, Network }

public sealed class TelemetryChart : FrameworkElement
{
    private const double WindowSeconds = 300;
    private const double NetworkEthDownLevel = 0.15;
    private const double NetworkEthUpLevel = 0.85;
    private const double NetworkAppDownLevel = 0.22;
    private const double NetworkAppUpLevel = 0.78;
    private static readonly Typeface Typeface = new("Segoe UI");
    private IReadOnlyList<TelemetrySample> _renderedSamples = Array.Empty<TelemetrySample>();
    private Rect _plotArea;
    private static long _renderCount;
    public static long RenderCount => Interlocked.Read(ref _renderCount);

    public static readonly DependencyProperty SamplesProperty = DependencyProperty.Register(
        nameof(Samples), typeof(IReadOnlyList<TelemetrySample>), typeof(TelemetryChart),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnSamplesChanged));
    public static readonly DependencyProperty KindProperty = DependencyProperty.Register(
        nameof(Kind), typeof(TelemetryChartKind), typeof(TelemetryChart),
        new FrameworkPropertyMetadata(TelemetryChartKind.Serial, FrameworkPropertyMetadataOptions.AffectsRender));

    public IReadOnlyList<TelemetrySample>? Samples { get => (IReadOnlyList<TelemetrySample>?)GetValue(SamplesProperty); set => SetValue(SamplesProperty, value); }
    public TelemetryChartKind Kind { get => (TelemetryChartKind)GetValue(KindProperty); set => SetValue(KindProperty, value); }

    public TelemetryChart()
    {
        SnapsToDevicePixels = true;
        IsHitTestVisible = true;
        IsVisibleChanged += (_, args) => { if ((bool)args.NewValue) InvalidateVisual(); };
    }

    private static void OnSamplesChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        var chart = (TelemetryChart)sender;
        if (chart.IsVisible) chart.InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        Interlocked.Increment(ref _renderCount);
        base.OnRender(dc);
        var background = Brush("ChartBackground", Color.FromRgb(0x0D, 0x16, 0x1F));
        var grid = Brush("ChartGrid", Color.FromRgb(0x26, 0x37, 0x46));
        var secondary = Brush("TextSecondary", Color.FromRgb(0xAA, 0xB8, 0xC4));
        dc.DrawRoundedRectangle(background, new Pen(grid, 1), new Rect(0.5, 0.5, Math.Max(0, ActualWidth - 1), Math.Max(0, ActualHeight - 1)), 6, 6);
        if (ActualWidth < 120 || ActualHeight < 80) return;

        _plotArea = new Rect(44, 12, Math.Max(1, ActualWidth - 56), Math.Max(1, ActualHeight - 38));
        DrawGrid(dc, grid, secondary);
        _renderedSamples = Samples ?? Array.Empty<TelemetrySample>();
        if (_renderedSamples.Count == 0)
        {
            DrawText(dc, "AGUARDANDO TELEMETRIA", secondary, 12, new Point(_plotArea.Left + 12, _plotArea.Top + 12));
            return;
        }

        var series = GetSeries();
        var maximum = Kind == TelemetryChartKind.Network ? 1d : series.SelectMany(item => _renderedSamples.Select(item.Selector)).Where(value => value.HasValue)
            .Select(value => value!.Value).DefaultIfEmpty(1).Max();
        maximum = Kind == TelemetryChartKind.Network ? 1 : Math.Max(1, maximum * 1.12);
        DrawText(dc, Kind == TelemetryChartKind.Network ? "UP" : maximum.ToString("0.#", CultureInfo.InvariantCulture), secondary, 10, new Point(4, _plotArea.Top - 3));
        DrawText(dc, Kind == TelemetryChartKind.Network ? "DOWN" : "0", secondary, 10, new Point(4, _plotArea.Bottom - 10));
        foreach (var item in series) DrawSeries(dc, item, maximum);
        if (Kind == TelemetryChartKind.Network) DrawNetworkLegend(dc, series);
        DrawMarkers(dc);
    }

    private void DrawGrid(DrawingContext dc, Brush grid, Brush text)
    {
        var pen = new Pen(grid, 1);
        pen.Freeze();
        for (var row = 0; row <= 3; row++)
        {
            var y = _plotArea.Top + _plotArea.Height * row / 3;
            dc.DrawLine(pen, new Point(_plotArea.Left, y), new Point(_plotArea.Right, y));
        }
        for (var minute = 0; minute <= 5; minute++)
        {
            var x = _plotArea.Left + _plotArea.Width * minute / 5;
            dc.DrawLine(pen, new Point(x, _plotArea.Top), new Point(x, _plotArea.Bottom));
            var label = minute == 5 ? "agora" : $"-{5 - minute} min";
            var formatted = Format(label, text, 10);
            var left = minute == 5 ? x - formatted.Width : minute == 0 ? x : x - formatted.Width / 2;
            dc.DrawText(formatted, new Point(left, _plotArea.Bottom + 5));
        }
    }

    private void DrawSeries(DrawingContext dc, ChartSeries series, double maximum)
    {
        if (_renderedSamples.Count == 0) return;
        var newest = _renderedSamples[^1].Timestamp;
        StreamGeometry? geometry = null;
        StreamGeometryContext? context = null;
        var figureOpen = false;
        Point? previousPoint = null;
        try
        {
            foreach (var sample in _renderedSamples)
            {
                if (sample.SessionStart) { figureOpen = false; previousPoint = null; }
                var value = series.Selector(sample);
                if (!value.HasValue) continue;
                var secondsAgo = Math.Max(0, (newest - sample.Timestamp).TotalSeconds);
                if (secondsAgo > WindowSeconds) continue;
                var x = _plotArea.Right - secondsAgo / WindowSeconds * _plotArea.Width;
                var normalizedValue = Kind == TelemetryChartKind.Network
                    ? MapNetworkLevel(value.Value, series.IsEthernetLink)
                    : Math.Clamp(value.Value / maximum, 0, 1);
                var y = _plotArea.Bottom - normalizedValue * _plotArea.Height;
                geometry ??= new StreamGeometry();
                context ??= geometry.Open();
                var point = new Point(x, y);
                if (!figureOpen) { context.BeginFigure(point, false, false); figureOpen = true; }
                else
                {
                    if (Kind == TelemetryChartKind.Network && previousPoint.HasValue)
                        context.LineTo(new Point(x, previousPoint.Value.Y), true, false);
                    context.LineTo(point, true, false);
                }
                previousPoint = point;
            }
        }
        finally { context?.Close(); }
        if (geometry is null) return;
        geometry.Freeze();
        var pen = new Pen(series.Brush, series.Thickness) { LineJoin = PenLineJoin.Round };
        pen.Freeze();
        dc.DrawGeometry(null, pen, geometry);
    }

    private void DrawMarkers(DrawingContext dc)
    {
        if (_renderedSamples.Count == 0) return;
        var relevantEvents = RelevantEventsFor(Kind);
        var newest = _renderedSamples[^1].Timestamp;
        foreach (var sample in _renderedSamples.Where(item => (item.Events & relevantEvents) != TelemetryEvent.None))
        {
            var secondsAgo = (newest - sample.Timestamp).TotalSeconds;
            if (secondsAgo is < 0 or > WindowSeconds) continue;
            var x = _plotArea.Right - secondsAgo / WindowSeconds * _plotArea.Width;
            dc.DrawRectangle(Brush("ChartError", Colors.Red), null, new Rect(x - 1, _plotArea.Top, 2, 9));
        }
    }

    internal static TelemetryEvent RelevantEventsFor(TelemetryChartKind kind) => kind switch
        {
            TelemetryChartKind.Serial => TelemetryEvent.UartError | TelemetryEvent.RxBad | TelemetryEvent.FrameBad |
                TelemetryEvent.OverrunError | TelemetryEvent.FramingError | TelemetryEvent.NoiseError | TelemetryEvent.ParityError,
            TelemetryChartKind.Sync => TelemetryEvent.SyncLost,
            TelemetryChartKind.FreeD => TelemetryEvent.FreeDError | TelemetryEvent.FreeDDrop,
            _ => TelemetryEvent.EthernetDown
        };

    private void DrawNetworkLegend(DrawingContext dc, IReadOnlyList<ChartSeries> series)
    {
        var x = _plotArea.Left + 8;
        foreach (var item in series)
        {
            dc.DrawLine(new Pen(item.Brush, 2.5), new Point(x, _plotArea.Top + 9), new Point(x + 18, _plotArea.Top + 9));
            var label = Format(item.Name, Brush("TextSecondary", Colors.LightGray), 10);
            dc.DrawText(label, new Point(x + 23, _plotArea.Top + 2));
            x += 28 + label.Width;
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_renderedSamples.Count == 0 || !_plotArea.Contains(e.GetPosition(this))) { ToolTip = null; return; }
        var point = e.GetPosition(this);
        var newest = _renderedSamples[^1].Timestamp;
        var target = newest - TimeSpan.FromSeconds((_plotArea.Right - point.X) / _plotArea.Width * WindowSeconds);
        var sample = _renderedSamples.MinBy(item => Math.Abs((item.Timestamp - target).Ticks));
        if (sample is null) return;
        var values = GetSeries().Select(item => $"{item.Name}: {item.Selector(sample)?.ToString("0.0", CultureInfo.InvariantCulture) ?? "N/A"}");
        var eventText = sample.Events == TelemetryEvent.None ? "nenhum" : sample.Events.ToString();
        ToolTip = $"{sample.Timestamp:HH:mm:ss}\n{string.Join("\n", values)}\nEventos: {eventText}";
    }

    protected override void OnMouseLeave(MouseEventArgs e) { ToolTip = null; base.OnMouseLeave(e); }

    private ChartSeries[] GetSeries() => Kind switch
    {
        TelemetryChartKind.Serial =>
        [new("RX TOTAL/s", sample => sample.RxTotalRate, Brush("ChartAmber", Colors.Orange), 2.2),
         new("RX OK/s", sample => sample.RxOkRate, Brush("ChartCyan", Colors.Cyan), 1.4),
         new("RX BAD/s", sample => sample.RxBadRate, Brush("ChartError", Colors.Red), 1.4)],
        TelemetryChartKind.Sync =>
        [new("SYNC/s", sample => sample.SyncRate, Brush("ChartAmber", Colors.Orange), 2.2)],
        TelemetryChartKind.FreeD =>
        [new("TX/s", sample => sample.FreeDTotalRate, Brush("ChartAmber", Colors.Orange), 2.2),
         new("OK/s", sample => sample.FreeDOkRate, Brush("ChartCyan", Colors.Cyan), 1.4),
         new("ERR/s", sample => sample.FreeDErrorRate, Brush("ChartError", Colors.Red), 1.4),
         new("DROP/s", sample => sample.FreeDDropRate, Brush("ChartWarning", Colors.Gold), 1.4)],
        _ => [new("ETH LINK (UP/DOWN)", sample => sample.EthernetLink, Brush("ChartCyan", Colors.Cyan), 2.4, true),
              new("APP ↔ BOX (POLL SUCCESS)", sample => sample.PollSuccess ? 1d : 0d, Brush("ChartAmber", Colors.Orange), 2.0)]
    };

    internal static double MapNetworkLevel(double value, bool isEthernetLink)
    {
        var up = value >= 0.5;
        return (isEthernetLink, up) switch
        {
            (true, true) => NetworkEthUpLevel,
            (true, false) => NetworkEthDownLevel,
            (false, true) => NetworkAppUpLevel,
            _ => NetworkAppDownLevel
        };
    }

    private static Brush Brush(string key, Color fallback) => Application.Current.TryFindResource(key) as Brush ?? new SolidColorBrush(fallback);
    private static void DrawText(DrawingContext dc, string text, Brush brush, double size, Point location) => dc.DrawText(Format(text, brush, size), location);
    private static FormattedText Format(string text, Brush brush, double size) => new(text, CultureInfo.CurrentCulture,
        FlowDirection.LeftToRight, Typeface, size, brush, VisualTreeHelper.GetDpi(Application.Current.MainWindow).PixelsPerDip);
    private sealed record ChartSeries(string Name, Func<TelemetrySample, double?> Selector, Brush Brush, double Thickness,
        bool IsEthernetLink = false);
}
