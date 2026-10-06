using Cam4TrackingMonitor.Models;
using System.IO;
using System.Net.Sockets;

namespace Cam4TrackingMonitor.Services;

public sealed class Cam4CommandService
{
    private readonly Cam4TcpClient _client;
    private readonly SemaphoreSlim _mutex = new(1, 1);
    public string Host { get; set; } = "192.0.2.10";
    public int Port { get; set; } = 1110;

    public Cam4CommandService(Cam4TcpClient client) => _client = client;

    public async Task<CommandResult> ExecuteAsync(string command, bool expectResponse = true, CancellationToken cancellationToken = default)
    {
        await _mutex.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var transport = await _client.ExecuteMeasuredAsync(Host, Port, command, expectResponse, cancellationToken).ConfigureAwait(false);
            return new CommandResult(true, transport.Response, BytesReceived: transport.BytesReceived,
                ConnectResult: "OK", WriteResult: "OK", ReadResult: expectResponse ? "OK" : "NOT_REQUESTED",
                ConnectMs: transport.ConnectMs, WriteMs: transport.WriteMs, ReadMs: transport.ReadMs);
        }
        catch (Exception exception) when (exception is SocketException or IOException or TimeoutException or OperationCanceledException)
        {
            return new CommandResult(false, string.Empty, exception.Message, ExceptionType: exception.GetType().FullName);
        }
        catch (Cam4TransportException exception)
        {
            return new CommandResult(false, string.Empty, exception.Message, exception.Phase,
                exception.InnerException?.GetType().FullName ?? exception.GetType().FullName, exception.SocketError,
                exception.BytesReceived,
                exception.Phase == CommunicationPhase.Connect ? "FAILED" : "OK",
                exception.Phase == CommunicationPhase.Connect ? "NOT_ATTEMPTED" : exception.Phase == CommunicationPhase.Write ? "FAILED" : "OK",
                exception.Phase is CommunicationPhase.Connect or CommunicationPhase.Write ? "NOT_ATTEMPTED" : "FAILED",
                exception.ConnectMs, exception.WriteMs, exception.ReadMs, exception.IsTimeout);
        }
        finally
        {
            _mutex.Release();
        }
    }
}
