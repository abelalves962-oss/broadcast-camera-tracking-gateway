using System.Windows;
using System.Diagnostics;
using Cam4TrackingMonitor.Logging;
using Cam4TrackingMonitor.Models;
using Cam4TrackingMonitor.Parsers;
using Cam4TrackingMonitor.Services;

namespace Cam4TrackingMonitor.ViewModels;

public sealed class MainViewModel : ObservableObject, IDisposable
{
    private readonly CancellationTokenSource _shutdown = new();
    private readonly DiagnosticPollingService _polling;
    private readonly EventDetector _events;
    private readonly EventLogger _logger;
    private readonly ConnectionManager _connection;
    private readonly Cam4CommandService _commands;
    private readonly Cam4TcpClient _tcpClient;
    private readonly TelemetryStateEvaluator _stateEvaluator = new();
    private ConnectionState _connectionState;
    private bool _wasEverOnline;

    public DashboardViewModel Dashboard { get; }
    public ConfigurationViewModel Configuration { get; }
    public EventsViewModel Events { get; }
    public ConnectionState ConnectionState { get => _connectionState; private set { Set(ref _connectionState, value); OnPropertyChanged(nameof(ConnectionStatus)); } }
    public string ConnectionStatus => ConnectionState.ToString().ToUpperInvariant();
    public string BoxIp => _commands.Host;
    public PollingDiagnostics PollingDiagnostics => _polling.GetDiagnostics();
    public double TcpTimeoutSeconds
    {
        get => _tcpClient.Timeout.TotalSeconds;
        set { _tcpClient.Timeout = TimeSpan.FromSeconds(Math.Clamp(value, 1, 30)); OnPropertyChanged(); }
    }

    public MainViewModel()
    {
        _logger = new EventLogger();
        _tcpClient = new Cam4TcpClient();
        _commands = new Cam4CommandService(_tcpClient);
        var maintenanceAuthorization = new MaintenanceAuthorizationService();
        _connection = new ConnectionManager();
        var diagnosticParser = new DiagnosticParser();
        _polling = new DiagnosticPollingService(_commands, diagnosticParser, _connection);
        _events = new EventDetector(_logger);
        Dashboard = new DashboardViewModel { BoxIp = _commands.Host };
        Events = new EventsViewModel(_logger);
        Configuration = new ConfigurationViewModel(new ConfigurationService(_commands, _polling, new ConfigurationParser(), diagnosticParser, _connection, _logger), maintenanceAuthorization);
        _connection.StateChanged += ConnectionChanged;
        _polling.TelemetryReceived += TelemetryReceived;
        _polling.DiagnosticsUpdated += PollingDiagnosticsUpdated;
        _polling.PollFailedDetailed += PollFailedDetailed;
        _polling.PollSucceededDetailed += PollSucceededDetailed;
        Configuration.HostChanged += (_, host) => { _commands.Host = host; Dashboard.BoxIp = host; OnPropertyChanged(nameof(BoxIp)); };
        Configuration.TelemetryValidated += (_, telemetry) => TelemetryReceived(this, telemetry);
        if (!string.Equals(Environment.GetEnvironmentVariable("CAM4_MONITOR_DISABLE_POLLING"), "1", StringComparison.Ordinal))
            _ = _polling.RunAsync(_shutdown.Token);
    }

    private void ConnectionChanged(object? sender, ConnectionState state)
    {
        var previous = ConnectionState;
        Application.Current.Dispatcher.BeginInvoke(() => { ConnectionState = state; Dashboard.ConnectionState = state; });
        var eventName = state switch
        {
            ConnectionState.Online => _wasEverOnline && previous == ConnectionState.Offline ? "TRACKING BOX RECOVERED" : "TRACKING BOX ONLINE",
            ConnectionState.Offline when previous != ConnectionState.Offline => "TRACKING BOX OFFLINE",
            _ => null
        };
        if (eventName is not null)
            _ = _logger.WriteAsync(new MonitorEvent(DateTime.Now,
                state == ConnectionState.Offline ? EventSeverity.Error : EventSeverity.Info,
                "NETWORK", eventName, previous.ToString(), state.ToString(), "TRANSITION", _commands.Host));
        if (state == ConnectionState.Online) _wasEverOnline = true;
    }

    private void TelemetryReceived(object? sender, TrackingBoxTelemetry telemetry)
    {
        _ = _events.ProcessAsync(telemetry);
        var states = _stateEvaluator.Evaluate(telemetry, _connection.State);
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            Dashboard.Update(telemetry, states);
            Events.UpdateTrackingBoxTcp(telemetry);
        });
    }

    private void PollingDiagnosticsUpdated(object? sender, PollingDiagnostics diagnostics)
    {
        using var process = Process.GetCurrentProcess();
        _ = _logger.WriteAsync(new MonitorEvent(DateTime.Now, EventSeverity.Info, "DIAGNOSTICS", "POLLING HEALTH",
            string.Empty, diagnostics.PollCount.ToString(), "SNAPSHOT",
            $"in_progress={diagnostics.InProgress}; max_concurrent={diagnostics.MaxConcurrent}; last_ms={diagnostics.LastDurationMs:F1}; max_ms={diagnostics.MaxDurationMs:F1}; failures={diagnostics.ConsecutiveFailures}; valid={diagnostics.ValidResponses}; invalid={diagnostics.InvalidResponses}; events_memory={Events.EventCount}; pending_work={ThreadPool.PendingWorkItemCount}; threads={process.Threads.Count}; memory_mb={process.WorkingSet64 / 1048576d:F1}"));
    }

    private void PollFailedDetailed(object? sender, PollFailureDiagnostic failure)
    {
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            Dashboard.RecordPollFailure(failure.Timestamp);
            Events.UpdatePollingDiagnostics(_polling.GetDiagnostics());
        });
        var message = FormattableString.Invariant($"{failure.Timestamp:yyyy-MM-dd HH:mm:ss.fff} | POLL={failure.PollNumber} | FAILURE | IP={failure.TargetIp} | PORT={failure.TargetPort} | STAGE={failure.Phase.ToString().ToUpperInvariant()} | CLASS={failure.FailureKind.ToString().ToUpperInvariant()} | CONNECT={failure.ConnectMs:F1}ms | WRITE={failure.WriteMs:F1}ms | READ={failure.ReadMs:F1}ms | TOTAL={failure.ElapsedMs:F1}ms | BYTES={failure.BytesReceived} | RESPONSE_LENGTH={failure.RawResponseLength} | ConnectResult={failure.ConnectResult} | WriteResult={failure.WriteResult} | ReadResult={failure.ReadResult} | ParseResult={failure.ParseResult} | ValidationResult={failure.ValidationResult} | Exception={failure.ExceptionType ?? "N/A"} | SocketError={failure.SocketError ?? "N/A"} | Message={failure.ExceptionMessage ?? "N/A"} | STATE_BEFORE={failure.StateBefore.ToString().ToUpperInvariant()} | STATE_AFTER={failure.StateAfter.ToString().ToUpperInvariant()} | CONSECUTIVE_FAILURES={failure.ConsecutiveFailures}");
        _ = _logger.WriteDiagnosticAsync(message);
    }

    private void PollSucceededDetailed(object? sender, PollSuccessDiagnostic success)
    {
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            Dashboard.RecordPollSuccess(success.ElapsedMs);
            Events.UpdatePollingDiagnostics(_polling.GetDiagnostics());
        });
        var recovery = success.FirstValidAfterOffline ? " FIRST VALID RESPONSE AFTER OFFLINE; transition=OFFLINE -> ONLINE;" : string.Empty;
        var message = FormattableString.Invariant($"{success.Timestamp:yyyy-MM-dd HH:mm:ss.fff} | POLL={success.PollNumber} | VALID | IP={success.TargetIp} | PORT={success.TargetPort} | CONNECT={success.ConnectMs:F1}ms | WRITE={success.WriteMs:F1}ms | READ={success.ReadMs:F1}ms | TOTAL={success.ElapsedMs:F1}ms | BYTES={success.BytesReceived} | RESPONSE_LENGTH={success.RawResponseLength} | STATE_BEFORE={success.StateBefore.ToString().ToUpperInvariant()} | STATE_AFTER={success.StateAfter.ToString().ToUpperInvariant()} | CONSECUTIVE_FAILURES={success.ConsecutiveFailures} | ETH_LINK={success.EthernetLink?.ToString() ?? "N/A"} | ETH_DOWN={success.EthernetDown?.ToString() ?? "N/A"} | ETH_UP={success.EthernetUp?.ToString() ?? "N/A"} | UPTIME={success.Uptime ?? "N/A"}{recovery}");
        _ = _logger.WriteDiagnosticAsync(message);
    }

    public void Dispose() { _shutdown.Cancel(); _shutdown.Dispose(); }
}
