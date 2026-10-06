namespace Cam4TrackingMonitor.Models;

public enum HealthMetricState { Ok, Event, Active }

public sealed class HealthMetric(string name, TimeSpan? activeDuration = null)
{
    private uint? _previousValue;
    private DateTime? _lastIncrementTime;

    public string Name { get; } = name;
    public uint? Value { get; private set; }
    public TimeSpan ActiveDuration { get; } = activeDuration ?? TimeSpan.FromSeconds(5);
    public DateTime? LastIncrementTime => _lastIncrementTime;
    public HealthMetricState State { get; private set; } = HealthMetricState.Ok;
    public string StateText => State.ToString().ToUpperInvariant();

    public void Observe(uint? value, DateTime timestamp)
    {
        if (_previousValue.HasValue && value.HasValue)
        {
            if (value > _previousValue) _lastIncrementTime = timestamp;
            else if (value < _previousValue) _lastIncrementTime = null;
        }

        _previousValue = value;
        Value = value;
        Refresh(timestamp);
    }

    public void Refresh(DateTime timestamp)
    {
        State = Value switch
        {
            null or 0 => HealthMetricState.Ok,
            _ when _lastIncrementTime.HasValue && timestamp - _lastIncrementTime.Value <= ActiveDuration => HealthMetricState.Active,
            _ => HealthMetricState.Event
        };
    }
}
