using Cam4TrackingMonitor.Logging;
using Cam4TrackingMonitor.Models;

namespace Cam4TrackingMonitor.Services;

public sealed class EventDetector
{
    private readonly EventLogger _logger;
    private readonly SemaphoreSlim _processLock = new(1, 1);
    private TrackingBoxTelemetry? _previous;
    public EventDetector(EventLogger logger) => _logger = logger;

    public async Task ProcessAsync(TrackingBoxTelemetry current)
    {
        await _processLock.WaitAsync().ConfigureAwait(false);
        try
        {
        if (_previous is null) { _previous = current; return; }
        if (ResetDetected(_previous, current))
        {
            await Log(EventSeverity.Info, "SYSTEM", "COUNTERS RESET/WRAP", _previous.RxTotal, current.RxTotal, "RESET");
            _previous = current;
            return;
        }
        await DetectCounter("SERIAL", "RX_BAD", _previous.RxBad, current.RxBad, EventSeverity.Warning);
        await DetectCounter("UART", "UART_ERR", _previous.UartError, current.UartError, EventSeverity.Warning);
        await DetectCounter("UART", "ORE", _previous.Ore, current.Ore, EventSeverity.Warning);
        await DetectCounter("UART", "FE", _previous.Fe, current.Fe, EventSeverity.Warning);
        await DetectCounter("UART", "NE", _previous.Ne, current.Ne, EventSeverity.Warning);
        await DetectCounter("UART", "PE", _previous.Pe, current.Pe, EventSeverity.Warning);
        await DetectCounter("FREE-D", "FREED_ERR", _previous.FreeDError, current.FreeDError, EventSeverity.Error);
        await DetectCounter("FREE-D", "FREED_DROP", _previous.FreeDDrop, current.FreeDDrop, EventSeverity.Warning);

        if (_previous.EthernetLink != current.EthernetLink && current.EthernetLink.HasValue)
            await Log(current.EthernetLink == 1 ? EventSeverity.Info : EventSeverity.Error, "NETWORK", current.EthernetLink == 1 ? "ETHERNET UP" : "ETHERNET DOWN", _previous.EthernetLink, current.EthernetLink, "TRANSITION");

        var hadSync = _previous.SyncTotal > 0;
        var hasSync = current.SyncTotal > 0;
        var wasSyncOk = hadSync && IsRecent(_previous.LastSyncMs, 1000);
        var syncOk = hasSync && IsRecent(current.LastSyncMs, 1000);
        if ((hadSync || hasSync) && wasSyncOk != syncOk)
            await Log(syncOk ? EventSeverity.Info : EventSeverity.Error, "SYNC", syncOk ? "SYNC RESTORED" : "SYNC LOST", wasSyncOk, syncOk, "TRANSITION");
        _previous = current;
        }
        finally { _processLock.Release(); }
    }

    public void Reset() => _previous = null;

    private async Task DetectCounter(string category, string name, uint? previous, uint? current, EventSeverity severity)
    {
        if (!previous.HasValue || !current.HasValue || previous == current) return;
        if (current < previous)
            await Log(EventSeverity.Info, "SYSTEM", "COUNTER RESET/WRAP", previous, current, name);
        else
            await Log(severity, category, $"{name} +{current - previous}", previous, current, "INCREMENT");
    }

    private Task Log(EventSeverity severity, string category, string name, object? oldValue, object? newValue, string result)
        => _logger.WriteAsync(new MonitorEvent(DateTime.Now, severity, category, name, oldValue?.ToString() ?? "N/A", newValue?.ToString() ?? "N/A", result, string.Empty));
    private static bool IsRecent(uint? milliseconds, uint threshold) => milliseconds.HasValue && milliseconds.Value <= threshold;
    private static bool ResetDetected(TrackingBoxTelemetry previous, TrackingBoxTelemetry current)
        => (previous.RxTotal.HasValue && current.RxTotal < previous.RxTotal)
           || (previous.SyncTotal.HasValue && current.SyncTotal < previous.SyncTotal)
           || (previous.FreeDTotal.HasValue && current.FreeDTotal < previous.FreeDTotal);
}
