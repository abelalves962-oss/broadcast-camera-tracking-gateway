using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using Cam4TrackingMonitor.Logging;
using Cam4TrackingMonitor.Models;
using Cam4TrackingMonitor.Services;
using Microsoft.Win32;

namespace Cam4TrackingMonitor.ViewModels;

public sealed class EventsViewModel : ObservableObject
{
    private readonly EventLogger _logger;
    private int _eventCount;
    public ObservableCollection<MonitorEvent> Events { get; } = new();
    public int EventCount => Volatile.Read(ref _eventCount);
    public RelayCommand OpenLogsCommand { get; }
    public RelayCommand ExportLogCommand { get; }
    public long TotalPolls => _diagnostics.PollCount;
    public long ValidPolls => _diagnostics.ValidResponses;
    public long FailedPolls => _diagnostics.InvalidResponses;
    public long ConnectFailures => _diagnostics.ConnectFailures;
    public long WriteFailures => _diagnostics.WriteFailures;
    public long ReadFailures => _diagnostics.ReadFailures;
    public long ParseFailures => _diagnostics.ParseFailures;
    public long ValidationFailures => _diagnostics.ValidationFailures;
    public long ConnectionResets => _diagnostics.ConnectionResets;
    public long Timeouts => _diagnostics.Timeouts;
    public long OtherFailures => _diagnostics.OtherFailures;
    public string AverageResponse => $"{_diagnostics.AverageDurationMs:0.0} ms";
    public string MaximumResponse => $"{_diagnostics.MaxDurationMs:0.0} ms";
    private TrackingBoxTelemetry? _tcpTelemetry;
    private static string Value(uint? value) => value?.ToString() ?? "N/A";
    private static string SignedValue(int? value) => value?.ToString() ?? "N/A";
    private static string HexValue(uint? value) => value.HasValue ? $"0x{value.Value:X2}" : "N/A";
    public string TcpAccept => Value(_tcpTelemetry?.TcpAccept);
    public string TcpRx => $"{Value(_tcpTelemetry?.TcpRxOk)} / {Value(_tcpTelemetry?.TcpRxErr)}";
    public string TcpSendCall => Value(_tcpTelemetry?.TcpSendCall);
    public string TcpSendRet => $"{Value(_tcpTelemetry?.TcpSendRetOk)} / {Value(_tcpTelemetry?.TcpSendRetErr)}";
    public string TcpSendOk => Value(_tcpTelemetry?.TcpSendOk);
    public string TcpTimeout => Value(_tcpTelemetry?.TcpTimeout);
    public string TcpCloseWait => Value(_tcpTelemetry?.TcpCloseWait);
    public string TcpDiscon => Value(_tcpTelemetry?.TcpDiscon);
    public string TcpReopen => Value(_tcpTelemetry?.TcpReopen);
    public string TcpDisconReopen => $"{TcpDiscon} / {TcpReopen}";
    public string TcpLastSr => HexValue(_tcpTelemetry?.TcpLastSr);
    public string TcpLastIr => HexValue(_tcpTelemetry?.TcpLastIr);
    public string TcpLastRecv => SignedValue(_tcpTelemetry?.TcpLastRecv);
    public string TcpLastSend => SignedValue(_tcpTelemetry?.TcpLastSend);
    public string TcpLastTxFsr => Value(_tcpTelemetry?.TcpLastTxFsr);
    private PollingDiagnostics _diagnostics = new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

    public EventsViewModel(EventLogger logger)
    {
        _logger = logger;
        logger.EventWritten += (_, item) => Application.Current.Dispatcher.BeginInvoke(() => { Events.Insert(0, item); while (Events.Count > 1000) Events.RemoveAt(Events.Count - 1); Volatile.Write(ref _eventCount, Events.Count); });
        OpenLogsCommand = new RelayCommand(logger.OpenFolder);
        ExportLogCommand = new RelayCommand(Export);
    }

    public void UpdatePollingDiagnostics(PollingDiagnostics diagnostics)
    {
        _diagnostics = diagnostics;
        OnPropertyChanged(nameof(TotalPolls));
        OnPropertyChanged(nameof(ValidPolls));
        OnPropertyChanged(nameof(FailedPolls));
        OnPropertyChanged(nameof(ConnectFailures));
        OnPropertyChanged(nameof(WriteFailures));
        OnPropertyChanged(nameof(ReadFailures));
        OnPropertyChanged(nameof(ParseFailures));
        OnPropertyChanged(nameof(ValidationFailures));
        OnPropertyChanged(nameof(ConnectionResets));
        OnPropertyChanged(nameof(Timeouts));
        OnPropertyChanged(nameof(OtherFailures));
        OnPropertyChanged(nameof(AverageResponse));
        OnPropertyChanged(nameof(MaximumResponse));
    }

    public void UpdateTrackingBoxTcp(TrackingBoxTelemetry telemetry)
    {
        _tcpTelemetry = telemetry;
        foreach (var property in new[] { nameof(TcpAccept), nameof(TcpRx), nameof(TcpSendCall), nameof(TcpSendRet), nameof(TcpSendOk), nameof(TcpTimeout), nameof(TcpCloseWait), nameof(TcpDiscon), nameof(TcpReopen), nameof(TcpDisconReopen), nameof(TcpLastSr), nameof(TcpLastIr), nameof(TcpLastRecv), nameof(TcpLastSend), nameof(TcpLastTxFsr) })
            OnPropertyChanged(property);
    }

    private void Export()
    {
        if (!File.Exists(_logger.CurrentLogPath)) return;
        var dialog = new SaveFileDialog { FileName = Path.GetFileName(_logger.CurrentLogPath), Filter = "Log (*.log)|*.log|Todos os arquivos (*.*)|*.*" };
        if (dialog.ShowDialog() == true) File.Copy(_logger.CurrentLogPath, dialog.FileName, true);
    }
}
