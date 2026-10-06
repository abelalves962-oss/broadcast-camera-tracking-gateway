using System.Net.Sockets;
using System.Text;
using System.IO;
using System.Diagnostics;
using Cam4TrackingMonitor.Models;

namespace Cam4TrackingMonitor.Services;

public sealed class Cam4TransportException : Exception
{
    public CommunicationPhase Phase { get; }
    public int BytesReceived { get; }
    public string? SocketError { get; }
    public double ConnectMs { get; }
    public double WriteMs { get; }
    public double ReadMs { get; }
    public bool IsTimeout { get; }
    public Cam4TransportException(CommunicationPhase phase, int bytesReceived, Exception inner,
        double connectMs, double writeMs, double readMs, bool isTimeout)
        : base(inner.Message, inner)
    {
        Phase = phase;
        BytesReceived = bytesReceived;
        SocketError = FindSocketException(inner)?.SocketErrorCode.ToString();
        ConnectMs = connectMs;
        WriteMs = writeMs;
        ReadMs = readMs;
        IsTimeout = isTimeout;
    }
    private static SocketException? FindSocketException(Exception? exception)
    {
        while (exception is not null) { if (exception is SocketException socket) return socket; exception = exception.InnerException; }
        return null;
    }
}

public sealed record Cam4TransportResult(string Response, int BytesReceived, double ConnectMs, double WriteMs, double ReadMs);

public sealed class Cam4TcpClient
{
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(3);
    public TimeSpan ReceiveIdleTimeout { get; set; } = TimeSpan.FromMilliseconds(200);

    public async Task<string> ExecuteAsync(string host, int port, string command, bool expectResponse, CancellationToken cancellationToken)
        => (await ExecuteMeasuredAsync(host, port, command, expectResponse, cancellationToken).ConfigureAwait(false)).Response;

    public async Task<Cam4TransportResult> ExecuteMeasuredAsync(string host, int port, string command, bool expectResponse,
        CancellationToken cancellationToken)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(Timeout);
        using var client = new TcpClient { NoDelay = true };
        var connectStarted = Stopwatch.GetTimestamp();
        double connectMs;
        try
        {
            await client.ConnectAsync(host, port, timeoutCts.Token).ConfigureAwait(false);
            connectMs = Stopwatch.GetElapsedTime(connectStarted).TotalMilliseconds;
        }
        catch (Exception exception) when (exception is SocketException or IOException or OperationCanceledException)
        {
            connectMs = Stopwatch.GetElapsedTime(connectStarted).TotalMilliseconds;
            throw new Cam4TransportException(CommunicationPhase.Connect, 0, exception, connectMs, 0, 0,
                IsTimeout(timeoutCts, cancellationToken));
        }
        await using var stream = client.GetStream();
        var request = Encoding.ASCII.GetBytes(command);
        var writeStarted = Stopwatch.GetTimestamp();
        double writeMs;
        try
        {
            await stream.WriteAsync(request, timeoutCts.Token).ConfigureAwait(false);
            await stream.FlushAsync(timeoutCts.Token).ConfigureAwait(false);
            writeMs = Stopwatch.GetElapsedTime(writeStarted).TotalMilliseconds;
        }
        catch (Exception exception) when (exception is SocketException or IOException or OperationCanceledException)
        {
            writeMs = Stopwatch.GetElapsedTime(writeStarted).TotalMilliseconds;
            throw new Cam4TransportException(CommunicationPhase.Write, 0, exception, connectMs, writeMs, 0,
                IsTimeout(timeoutCts, cancellationToken));
        }
        if (!expectResponse) return new Cam4TransportResult(string.Empty, 0, connectMs, writeMs, 0);

        using var content = new MemoryStream();
        var buffer = new byte[4096];
        var readStarted = Stopwatch.GetTimestamp();
        while (true)
        {
            using var readCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token);
            if (content.Length > 0) readCts.CancelAfter(ReceiveIdleTimeout);
            try
            {
                var count = await stream.ReadAsync(buffer, readCts.Token).ConfigureAwait(false);
                if (count == 0) break;
                content.Write(buffer, 0, count);
                if (IsComplete(command, content)) break;
            }
            catch (OperationCanceledException) when (!timeoutCts.IsCancellationRequested && content.Length > 0)
            {
                break;
            }
            catch (Exception exception) when (content.Length > 0 && exception is IOException or SocketException)
            {
                // O firmware pode encerrar/resetar o socket imediatamente após enviar a resposta.
                break;
            }
            catch (Exception exception) when (exception is SocketException or IOException or OperationCanceledException)
            {
                throw new Cam4TransportException(CommunicationPhase.Read, checked((int)content.Length), exception,
                    connectMs, writeMs, Stopwatch.GetElapsedTime(readStarted).TotalMilliseconds,
                    IsTimeout(timeoutCts, cancellationToken));
            }
        }
        return new Cam4TransportResult(Encoding.ASCII.GetString(content.ToArray()), checked((int)content.Length),
            connectMs, writeMs, Stopwatch.GetElapsedTime(readStarted).TotalMilliseconds);
    }

    private static bool IsTimeout(CancellationTokenSource timeoutCts, CancellationToken callerToken) =>
        timeoutCts.IsCancellationRequested && !callerToken.IsCancellationRequested;

    private static bool IsComplete(string command, MemoryStream content)
    {
        if (!string.Equals(command.Trim(), "?D", StringComparison.OrdinalIgnoreCase)) return false;
        var bytes = content.GetBuffer().AsSpan(0, checked((int)content.Length));
        return bytes.EndsWith(" ?\n"u8) || bytes.EndsWith(" ?\r\n"u8);
    }
}
