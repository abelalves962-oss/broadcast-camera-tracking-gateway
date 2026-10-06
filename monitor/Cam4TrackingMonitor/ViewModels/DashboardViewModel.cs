using Cam4TrackingMonitor.Models;
using Cam4TrackingMonitor.Services;

namespace Cam4TrackingMonitor.ViewModels;

public sealed class DashboardViewModel : ObservableObject
{
    private readonly TelemetryHistoryService _historyService = new();
    private TrackingBoxTelemetry? _telemetry;
    private IReadOnlyList<TelemetrySample> _history = Array.Empty<TelemetrySample>();
    private ConnectionState _connectionState;
    private string _boxIp = "192.0.2.10";
    private double _lastPollMs;
    private long _pollFailures;
    private TelemetryStates _states = new(VisualState.Unknown, VisualState.Unknown, VisualState.Unknown, VisualState.Unknown);
    public TrackingBoxTelemetry? Telemetry => _telemetry;
    public IReadOnlyList<TelemetrySample> History => _history;
    public int HistoryCount => _historyService.Count;
    public ConnectionState ConnectionState { get => _connectionState; set { if (Set(ref _connectionState, value)) { OnPropertyChanged(nameof(TcpStatus)); OnPropertyChanged(nameof(PollStatus)); } } }
    public string BoxIp { get => _boxIp; set => Set(ref _boxIp, value); }
    public string LastUpdate => Telemetry?.Timestamp.ToString("HH:mm:ss") ?? "N/A";
    public string Uptime => Telemetry?.Uptime ?? "N/A";
    public string TcpStatus => ConnectionState.ToString().ToUpperInvariant();
    public string NetworkState => _states.Network.ToString().ToUpperInvariant();
    public string SerialState => _states.Serial.ToString().ToUpperInvariant();
    public string SyncState => _states.Sync.ToString().ToUpperInvariant();
    public string FreeDState => _states.FreeD.ToString().ToUpperInvariant();
    public string SerialRate => RateText(_history.LastOrDefault()?.RxTotalRate, "pkt/s");
    public string SyncRate => RateText(_history.LastOrDefault()?.SyncRate, "sync/s");
    public string FreeDRate => RateText(_history.LastOrDefault()?.FreeDTotalRate, "TX/s");
    public string CommunicationRate
    {
        get
        {
            if (_history.Count == 0) return "N/A";
            return $"{_history.Count(item => item.PollSuccess) * 100d / _history.Count:0.0}% OK";
        }
    }
    public string LinkStatus => Telemetry?.EthernetLink switch { 1 => "UP", 0 => "DOWN", _ => "N/A" };
    public string PollStatus => ConnectionState == ConnectionState.Online ? "OK" : "FAILED";
    public string PollSuccessRate => CommunicationRate;
    public string LastPoll => _lastPollMs <= 0 ? "N/A" : $"{_lastPollMs:0.0} ms";
    public long PollFailures => _pollFailures;
    public HealthMetric UartHealth { get; } = new("UART ERR");
    public HealthMetric RxBadHealth { get; } = new("RX BAD");
    public HealthMetric SyncLostHealth { get; } = new("SYNC LOST");
    public HealthMetric FreeDErrorHealth { get; } = new("FREE-D ERR");
    public HealthMetric FreeDDropHealth { get; } = new("FREE-D DROP");
    public HealthMetric EthernetDownHealth { get; } = new("ETH DOWN");

    public void Update(TrackingBoxTelemetry telemetry, TelemetryStates states)
    {
        var oldUptime = _telemetry?.Uptime;
        _telemetry = telemetry;
        _history = _historyService.AddTelemetry(telemetry);
        UartHealth.Observe(telemetry.UartError, telemetry.Timestamp);
        RxBadHealth.Observe(telemetry.RxBad, telemetry.Timestamp);
        SyncLostHealth.Observe(telemetry.SyncLost, telemetry.Timestamp);
        FreeDErrorHealth.Observe(telemetry.FreeDError, telemetry.Timestamp);
        FreeDDropHealth.Observe(telemetry.FreeDDrop, telemetry.Timestamp);
        EthernetDownHealth.Observe(telemetry.EthernetDown, telemetry.Timestamp);
        OnPropertyChanged(nameof(Telemetry));
        NotifyDashboardTelemetry();
        OnPropertyChanged(nameof(LastUpdate));
        OnPropertyChanged(nameof(LinkStatus));
        if (!string.Equals(oldUptime, telemetry.Uptime, StringComparison.Ordinal)) OnPropertyChanged(nameof(Uptime));
        var networkChanged = _states.Network != states.Network;
        var serialChanged = _states.Serial != states.Serial;
        var syncChanged = _states.Sync != states.Sync;
        var freeDChanged = _states.FreeD != states.FreeD;
        _states = states;
        if (networkChanged) OnPropertyChanged(nameof(NetworkState));
        if (serialChanged) OnPropertyChanged(nameof(SerialState));
        if (syncChanged) OnPropertyChanged(nameof(SyncState));
        if (freeDChanged) OnPropertyChanged(nameof(FreeDState));
    }

    public void RecordPollFailure(DateTime timestamp)
    {
        _pollFailures++;
        _history = _historyService.AddPollFailure(timestamp);
        NotifyDashboardTelemetry();
    }

    public void RecordPollSuccess(double elapsedMs)
    {
        _lastPollMs = elapsedMs;
        OnPropertyChanged(nameof(PollStatus));
        OnPropertyChanged(nameof(PollSuccessRate));
        OnPropertyChanged(nameof(LastPoll));
        OnPropertyChanged(nameof(PollFailures));
    }

    private void NotifyDashboardTelemetry()
    {
        OnPropertyChanged(nameof(History));
        OnPropertyChanged(nameof(HistoryCount));
        OnPropertyChanged(nameof(SerialRate));
        OnPropertyChanged(nameof(SyncRate));
        OnPropertyChanged(nameof(FreeDRate));
        OnPropertyChanged(nameof(CommunicationRate));
        OnPropertyChanged(nameof(PollStatus));
        OnPropertyChanged(nameof(PollSuccessRate));
        OnPropertyChanged(nameof(LastPoll));
        OnPropertyChanged(nameof(PollFailures));
        OnPropertyChanged(nameof(UartHealth));
        OnPropertyChanged(nameof(RxBadHealth));
        OnPropertyChanged(nameof(SyncLostHealth));
        OnPropertyChanged(nameof(FreeDErrorHealth));
        OnPropertyChanged(nameof(FreeDDropHealth));
        OnPropertyChanged(nameof(EthernetDownHealth));
    }

    private static string RateText(double? value, string unit) => value.HasValue ? $"{value.Value:0.0} {unit}" : $"0.0 {unit}";
    public string Value(uint? value) => value?.ToString() ?? "N/A";
}
