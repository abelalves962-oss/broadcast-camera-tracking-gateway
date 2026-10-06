using System.Diagnostics;
using System.Net.Sockets;
using Cam4TrackingMonitor.Models;
using Cam4TrackingMonitor.Parsers;

namespace Cam4TrackingMonitor.Services;

public sealed record PollingDiagnostics(long PollCount, int InProgress, int MaxConcurrent, double LastDurationMs,
    double AverageDurationMs, double MaxDurationMs, int ConsecutiveFailures, long ValidResponses, long InvalidResponses,
    long ConnectFailures, long WriteFailures, long ReadFailures, long ParseFailures, long ValidationFailures,
    long ConnectionResets, long Timeouts, long OtherFailures);
public sealed record PollFailureDiagnostic(DateTime Timestamp, long PollNumber, string TargetIp, int TargetPort,
    CommunicationPhase Phase, double ElapsedMs, double ConnectMs, double WriteMs, double ReadMs,
    string? ExceptionType, string? SocketError, string? ExceptionMessage, bool IsTimeout, PollFailureKind FailureKind, int BytesReceived,
    int RawResponseLength, int ConsecutiveFailures, uint? EthernetLink, uint? EthernetDown, uint? EthernetUp, string? Uptime,
    ConnectionState StateBefore, ConnectionState StateAfter, string ConnectResult, string WriteResult, string ReadResult,
    string ParseResult, string ValidationResult);
public sealed record PollSuccessDiagnostic(DateTime Timestamp, long PollNumber, string TargetIp, int TargetPort,
    double ElapsedMs, double ConnectMs, double WriteMs, double ReadMs, int BytesReceived, int RawResponseLength,
    uint? EthernetLink, uint? EthernetDown, uint? EthernetUp, string? Uptime,
    ConnectionState StateBefore, ConnectionState StateAfter, int ConsecutiveFailures, bool FirstValidAfterOffline);

public sealed class DiagnosticPollingService
{
    private readonly Cam4CommandService _commands;
    private readonly DiagnosticParser _parser;
    private readonly ConnectionManager _connection;
    private volatile bool _paused;
    private int _runnerActive;
    private int _inProgress;
    private int _maxConcurrent;
    private int _consecutiveFailures;
    private long _pollCount;
    private long _validResponses;
    private long _invalidResponses;
    private long _lastDurationTicks;
    private long _totalDurationTicks;
    private long _maxDurationTicks;
    private long _connectFailures;
    private long _writeFailures;
    private long _readFailures;
    private long _parseFailures;
    private long _validationFailures;
    private long _connectionResets;
    private long _timeouts;
    private long _otherFailures;
    private TrackingBoxTelemetry? _lastValidTelemetry;

    public TimeSpan Interval { get; set; } = TimeSpan.FromSeconds(1);
    public event EventHandler<TrackingBoxTelemetry>? TelemetryReceived;
    public event EventHandler<string>? PollingFailed;
    public event EventHandler<PollingDiagnostics>? DiagnosticsUpdated;
    public event EventHandler<PollFailureDiagnostic>? PollFailedDetailed;
    public event EventHandler<PollSuccessDiagnostic>? PollSucceededDetailed;

    public DiagnosticPollingService(Cam4CommandService commands, DiagnosticParser parser, ConnectionManager connection)
        => (_commands, _parser, _connection) = (commands, parser, connection);

    public void Pause() => _paused = true;
    public void Resume() => _paused = false;

    public PollingDiagnostics GetDiagnostics() => new(
        Interlocked.Read(ref _pollCount), Volatile.Read(ref _inProgress), Volatile.Read(ref _maxConcurrent),
        ToMilliseconds(Interlocked.Read(ref _lastDurationTicks)), AverageDurationMilliseconds(),
        ToMilliseconds(Interlocked.Read(ref _maxDurationTicks)), Volatile.Read(ref _consecutiveFailures),
        Interlocked.Read(ref _validResponses), Interlocked.Read(ref _invalidResponses),
        Interlocked.Read(ref _connectFailures), Interlocked.Read(ref _writeFailures), Interlocked.Read(ref _readFailures),
        Interlocked.Read(ref _parseFailures), Interlocked.Read(ref _validationFailures),
        Interlocked.Read(ref _connectionResets), Interlocked.Read(ref _timeouts), Interlocked.Read(ref _otherFailures));

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(ref _runnerActive, 1) != 0) return;
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(Interval, cancellationToken).ConfigureAwait(false);
                if (_paused) continue;
                await PollOnceAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        finally { Volatile.Write(ref _runnerActive, 0); }
    }

    private async Task PollOnceAsync(CancellationToken cancellationToken)
    {
        var concurrent = Interlocked.Increment(ref _inProgress);
        UpdateMax(ref _maxConcurrent, concurrent);
        var started = Stopwatch.GetTimestamp();
        try
        {
            if (Interlocked.Read(ref _pollCount) == 0) _connection.Connecting();
            var result = await _commands.ExecuteAsync("?D", cancellationToken: cancellationToken).ConfigureAwait(false);
            var pollNumber = Interlocked.Increment(ref _pollCount);
            if (result.Success && _parser.TryParse(result.Response, out var telemetry))
            {
                var stateBefore = _connection.State;
                Interlocked.Increment(ref _validResponses);
                Volatile.Write(ref _consecutiveFailures, 0);
                _connection.TransactionSucceeded();
                _lastValidTelemetry = telemetry;
                PollSucceededDetailed?.Invoke(this, new PollSuccessDiagnostic(DateTime.Now, pollNumber, _commands.Host,
                    _commands.Port, Stopwatch.GetElapsedTime(started).TotalMilliseconds,
                    result.ConnectMs, result.WriteMs, result.ReadMs, result.BytesReceived, result.Response.Length,
                    telemetry.EthernetLink, telemetry.EthernetDown, telemetry.EthernetUp, telemetry.Uptime,
                    stateBefore, _connection.State, 0, stateBefore == ConnectionState.Offline));
                TelemetryReceived?.Invoke(this, telemetry);
            }
            else
            {
                Interlocked.Increment(ref _invalidResponses);
                var consecutiveFailures = Interlocked.Increment(ref _consecutiveFailures);
                var phase = result.FailurePhase ?? CommunicationPhase.Parse;
                CountFailure(phase, result);
                var last = _lastValidTelemetry;
                var stateBefore = _connection.State;
                _connection.TransactionFailed();
                var stateAfter = _connection.State;
                PollFailedDetailed?.Invoke(this, new PollFailureDiagnostic(DateTime.Now, pollNumber, _commands.Host,
                    _commands.Port, phase, Stopwatch.GetElapsedTime(started).TotalMilliseconds,
                    result.ConnectMs, result.WriteMs, result.ReadMs, result.ExceptionType, result.SocketError,
                    result.Error, result.IsTimeout, ClassifyFailure(result), result.BytesReceived, result.Response.Length,
                    consecutiveFailures, last?.EthernetLink, last?.EthernetDown, last?.EthernetUp, last?.Uptime,
                    stateBefore, stateAfter, result.ConnectResult, result.WriteResult, result.ReadResult,
                    result.Success ? "FAILED" : "NOT_ATTEMPTED", "FAILED"));
                if (stateBefore != ConnectionState.Offline && stateAfter == ConnectionState.Offline)
                    PollingFailed?.Invoke(this, result.Error ?? "Resposta de diagnóstico inválida ou incompleta.");
            }
        }
        finally
        {
            var elapsed = Stopwatch.GetTimestamp() - started;
            Interlocked.Exchange(ref _lastDurationTicks, elapsed);
            Interlocked.Add(ref _totalDurationTicks, elapsed);
            UpdateMax(ref _maxDurationTicks, elapsed);
            Interlocked.Decrement(ref _inProgress);
            var snapshot = GetDiagnostics();
            if (snapshot.PollCount > 0 && snapshot.PollCount % 60 == 0) DiagnosticsUpdated?.Invoke(this, snapshot);
        }
    }

    private static void UpdateMax(ref int target, int value)
    {
        int current;
        while (value > (current = Volatile.Read(ref target)) && Interlocked.CompareExchange(ref target, value, current) != current) { }
    }

    private static void UpdateMax(ref long target, long value)
    {
        long current;
        while (value > (current = Interlocked.Read(ref target)) && Interlocked.CompareExchange(ref target, value, current) != current) { }
    }

    private static double ToMilliseconds(long ticks) => ticks * 1000d / Stopwatch.Frequency;

    private double AverageDurationMilliseconds()
    {
        var count = Interlocked.Read(ref _pollCount);
        return count == 0 ? 0 : ToMilliseconds(Interlocked.Read(ref _totalDurationTicks)) / count;
    }

    private void CountFailure(CommunicationPhase phase, CommandResult result)
    {
        switch (phase)
        {
            case CommunicationPhase.Connect: Interlocked.Increment(ref _connectFailures); break;
            case CommunicationPhase.Write: Interlocked.Increment(ref _writeFailures); break;
            case CommunicationPhase.Read: Interlocked.Increment(ref _readFailures); break;
            case CommunicationPhase.Parse: Interlocked.Increment(ref _parseFailures); break;
            case CommunicationPhase.Validation: Interlocked.Increment(ref _validationFailures); break;
        }

        switch (ClassifyFailure(result))
        {
            case PollFailureKind.ConnectionReset: Interlocked.Increment(ref _connectionResets); break;
            case PollFailureKind.Timeout: Interlocked.Increment(ref _timeouts); break;
            default: Interlocked.Increment(ref _otherFailures); break;
        }
    }

    internal static PollFailureKind ClassifyFailure(CommandResult result)
    {
        if (result.IsTimeout || string.Equals(result.SocketError, SocketError.TimedOut.ToString(), StringComparison.OrdinalIgnoreCase))
            return PollFailureKind.Timeout;
        if (string.Equals(result.SocketError, SocketError.ConnectionReset.ToString(), StringComparison.OrdinalIgnoreCase) ||
            string.Equals(result.SocketError, SocketError.ConnectionAborted.ToString(), StringComparison.OrdinalIgnoreCase))
            return PollFailureKind.ConnectionReset;
        return PollFailureKind.Other;
    }
}
