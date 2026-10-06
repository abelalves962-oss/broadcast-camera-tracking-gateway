using System.Globalization;
using System.Text.RegularExpressions;
using Cam4TrackingMonitor.Models;

namespace Cam4TrackingMonitor.Services;

public sealed partial class CounterRateCalculator
{
    private TrackingBoxTelemetry? _previous;

    public TelemetrySample Add(TrackingBoxTelemetry current)
    {
        var previous = _previous;
        _previous = current;
        if (previous is null)
            return Empty(current.Timestamp, true, current.EthernetLink, TelemetryEvent.None);

        var elapsed = (current.Timestamp - previous.Timestamp).TotalSeconds;
        var restart = IsRestart(previous, current);
        if (restart || elapsed <= 0)
            return Empty(current.Timestamp, true, current.EthernetLink, restart ? TelemetryEvent.BoxRestart : TelemetryEvent.None);

        var events = IncrementEvents(previous, current);
        return new TelemetrySample(current.Timestamp, true, false,
            Rate(previous.RxTotal, current.RxTotal, elapsed),
            Rate(previous.RxOk, current.RxOk, elapsed),
            Rate(previous.RxBad, current.RxBad, elapsed),
            Rate(previous.SyncTotal, current.SyncTotal, elapsed),
            Rate(previous.FreeDTotal, current.FreeDTotal, elapsed),
            Rate(previous.FreeDOk, current.FreeDOk, elapsed),
            Rate(previous.FreeDError, current.FreeDError, elapsed),
            Rate(previous.FreeDDrop, current.FreeDDrop, elapsed), current.EthernetLink, events);
    }

    private static TelemetrySample Empty(DateTime timestamp, bool sessionStart, uint? ethernetLink, TelemetryEvent events) =>
        new(timestamp, true, sessionStart, null, null, null, null, null, null, null, null, ethernetLink, events);

    private static double? Rate(uint? previous, uint? current, double seconds) =>
        previous.HasValue && current.HasValue && current >= previous
            ? (current.Value - previous.Value) / seconds
            : null;

    private static bool IsRestart(TrackingBoxTelemetry previous, TrackingBoxTelemetry current)
    {
        var oldUptime = ParseUptime(previous.Uptime);
        var newUptime = ParseUptime(current.Uptime);
        if (oldUptime.HasValue && newUptime.HasValue && newUptime < oldUptime) return true;
        return Decreased(previous.RxTotal, current.RxTotal) || Decreased(previous.RxOk, current.RxOk) ||
               Decreased(previous.RxBad, current.RxBad) || Decreased(previous.SyncTotal, current.SyncTotal) ||
               Decreased(previous.SyncLost, current.SyncLost) || Decreased(previous.FreeDTotal, current.FreeDTotal) ||
               Decreased(previous.FreeDOk, current.FreeDOk) || Decreased(previous.FreeDError, current.FreeDError) ||
               Decreased(previous.FreeDDrop, current.FreeDDrop) || Decreased(previous.UartError, current.UartError) ||
               Decreased(previous.Ore, current.Ore) || Decreased(previous.Fe, current.Fe) ||
               Decreased(previous.Ne, current.Ne) || Decreased(previous.Pe, current.Pe) ||
               Decreased(previous.FrameBad, current.FrameBad) || Decreased(previous.EthernetDown, current.EthernetDown) ||
               Decreased(previous.EthernetUp, current.EthernetUp);
    }

    private static bool Decreased(uint? previous, uint? current) => previous.HasValue && current.HasValue && current < previous;
    private static bool Increased(uint? previous, uint? current) => previous.HasValue && current.HasValue && current > previous;

    private static TelemetryEvent IncrementEvents(TrackingBoxTelemetry previous, TrackingBoxTelemetry current)
    {
        var events = TelemetryEvent.None;
        if (Increased(previous.UartError, current.UartError)) events |= TelemetryEvent.UartError;
        if (Increased(previous.RxBad, current.RxBad)) events |= TelemetryEvent.RxBad;
        if (Increased(previous.FrameBad, current.FrameBad)) events |= TelemetryEvent.FrameBad;
        if (Increased(previous.Ore, current.Ore)) events |= TelemetryEvent.OverrunError;
        if (Increased(previous.Fe, current.Fe)) events |= TelemetryEvent.FramingError;
        if (Increased(previous.Ne, current.Ne)) events |= TelemetryEvent.NoiseError;
        if (Increased(previous.Pe, current.Pe)) events |= TelemetryEvent.ParityError;
        if (Increased(previous.SyncLost, current.SyncLost)) events |= TelemetryEvent.SyncLost;
        if (Increased(previous.FreeDError, current.FreeDError)) events |= TelemetryEvent.FreeDError;
        if (Increased(previous.FreeDDrop, current.FreeDDrop)) events |= TelemetryEvent.FreeDDrop;
        if (Increased(previous.EthernetDown, current.EthernetDown)) events |= TelemetryEvent.EthernetDown;
        if (Increased(previous.EthernetUp, current.EthernetUp)) events |= TelemetryEvent.EthernetUp;
        return events;
    }

    private static TimeSpan? ParseUptime(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var match = UptimeRegex().Match(value);
        if (!match.Success) return null;
        if (!uint.TryParse(match.Groups[1].Value, CultureInfo.InvariantCulture, out var days) ||
            !int.TryParse(match.Groups[2].Value, CultureInfo.InvariantCulture, out var hours) ||
            !int.TryParse(match.Groups[3].Value, CultureInfo.InvariantCulture, out var minutes) ||
            !int.TryParse(match.Groups[4].Value, CultureInfo.InvariantCulture, out var seconds)) return null;
        return TimeSpan.FromDays(days) + new TimeSpan(hours, minutes, seconds);
    }

    [GeneratedRegex(@"^(\d+)d\s+(\d{1,2}):(\d{2}):(\d{2})$")]
    private static partial Regex UptimeRegex();
}
