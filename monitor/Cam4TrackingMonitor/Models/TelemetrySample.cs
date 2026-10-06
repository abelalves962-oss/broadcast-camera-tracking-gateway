namespace Cam4TrackingMonitor.Models;

[Flags]
public enum TelemetryEvent
{
    None = 0,
    UartError = 1,
    RxBad = 2,
    SyncLost = 4,
    FreeDError = 8,
    FreeDDrop = 16,
    EthernetDown = 32,
    BoxRestart = 64,
    PollFailure = 128,
    EthernetUp = 256,
    FrameBad = 512,
    OverrunError = 1024,
    FramingError = 2048,
    NoiseError = 4096,
    ParityError = 8192
}

public sealed record TelemetrySample(
    DateTime Timestamp,
    bool PollSuccess,
    bool SessionStart,
    double? RxTotalRate,
    double? RxOkRate,
    double? RxBadRate,
    double? SyncRate,
    double? FreeDTotalRate,
    double? FreeDOkRate,
    double? FreeDErrorRate,
    double? FreeDDropRate,
    uint? EthernetLink,
    TelemetryEvent Events);
