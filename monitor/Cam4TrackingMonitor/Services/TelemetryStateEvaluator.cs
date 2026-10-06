using Cam4TrackingMonitor.Models;

namespace Cam4TrackingMonitor.Services;

public sealed record TelemetryStates(VisualState Network, VisualState Serial, VisualState Sync, VisualState FreeD);

public sealed class TelemetryStateEvaluator
{
    private TrackingBoxTelemetry? _previous;

    public TelemetryStates Evaluate(TrackingBoxTelemetry telemetry, ConnectionState connection)
    {
        if (connection == ConnectionState.Offline) return new(VisualState.Offline, VisualState.Offline, VisualState.Offline, VisualState.Offline);
        var network = telemetry.EthernetLink switch { 1 => VisualState.Ok, 0 => VisualState.Error, _ => VisualState.Unknown };
        var serial = telemetry.RxTotal == 0 && telemetry.RxOk == 0 && telemetry.RxBad == 0 && telemetry.UartError == 0
            ? VisualState.Waiting : AgeState(telemetry.LastRxOkMs, 1000, 3000);
        var sync = telemetry.SyncTotal == 0 && telemetry.SyncLost == 0
            ? VisualState.Waiting : AgeState(telemetry.LastSyncMs, 1000, 3000);
        var freeD = telemetry.FreeDTotal == 0 && telemetry.FreeDError == 0 && telemetry.FreeDDrop == 0
            ? (telemetry.RxTotal == 0 || telemetry.SyncTotal == 0 ? VisualState.Inactive : VisualState.Waiting)
            : AgeState(telemetry.LastFreeDMs, 1500, 4000);
        if (_previous is not null)
        {
            if (Increased(_previous.RxBad, telemetry.RxBad) || Increased(_previous.UartError, telemetry.UartError)) serial = VisualState.Warning;
            if (Increased(_previous.FreeDError, telemetry.FreeDError) || Increased(_previous.FreeDDrop, telemetry.FreeDDrop)) freeD = VisualState.Warning;
        }
        _previous = telemetry;
        return new(network, serial, sync, freeD);
    }

    private static VisualState AgeState(uint? age, uint warning, uint error)
    {
        if (!age.HasValue) return VisualState.Unknown;
        if (age.Value <= warning) return VisualState.Ok;
        return age.Value <= error ? VisualState.Warning : VisualState.Error;
    }
    private static bool Increased(uint? previous, uint? current) => previous.HasValue && current.HasValue && current > previous;
}
