using Cam4TrackingMonitor.Collections;
using Cam4TrackingMonitor.Models;

namespace Cam4TrackingMonitor.Services;

public sealed class TelemetryHistoryService
{
    public const int MaximumSamples = 300;
    private readonly FixedSizeCircularBuffer<TelemetrySample> _samples = new(MaximumSamples);
    private readonly CounterRateCalculator _rates = new();

    public int Count => _samples.Count;
    public IReadOnlyList<TelemetrySample> AddTelemetry(TrackingBoxTelemetry telemetry)
    {
        _samples.Add(_rates.Add(telemetry));
        return _samples.Snapshot();
    }

    public IReadOnlyList<TelemetrySample> AddPollFailure(DateTime timestamp)
    {
        _samples.Add(new TelemetrySample(timestamp, false, false, null, null, null, null,
            null, null, null, null, null, TelemetryEvent.PollFailure));
        return _samples.Snapshot();
    }
}
