namespace Cam4TrackingMonitor.Models;

public sealed class TrackingBoxTelemetry
{
    public DateTime Timestamp { get; init; } = DateTime.Now;
    public string RawResponse { get; init; } = string.Empty;
    public uint? UartError { get; init; }
    public uint? LastUartError { get; init; }
    public uint? Ore { get; init; }
    public uint? Fe { get; init; }
    public uint? Ne { get; init; }
    public uint? Pe { get; init; }
    public uint? FrameBad { get; init; }
    public uint? RxTotal { get; init; }
    public uint? RxOk { get; init; }
    public uint? RxBad { get; init; }
    public uint? LastRxEventMs { get; init; }
    public uint? LastRxOkMs { get; init; }
    public uint? SyncTotal { get; init; }
    public uint? SyncLost { get; init; }
    public uint? LastSyncMs { get; init; }
    public uint? FreeDTotal { get; init; }
    public uint? FreeDOk { get; init; }
    public uint? FreeDError { get; init; }
    public uint? FreeDDrop { get; init; }
    public uint? LastFreeDMs { get; init; }
    public uint? EthernetLink { get; init; }
    public uint? EthernetDown { get; init; }
    public uint? EthernetUp { get; init; }
    public string? Uptime { get; init; }

    // Instrumentacao do servidor TCP 1110 no firmware (ausente em firmware legado).
    public uint? TcpAccept { get; init; }
    public uint? TcpRxOk { get; init; }
    public uint? TcpRxErr { get; init; }
    public uint? TcpSendCall { get; init; }
    public uint? TcpSendRetOk { get; init; }
    public uint? TcpSendRetErr { get; init; }
    public uint? TcpSendOk { get; init; }
    public uint? TcpTimeout { get; init; }
    public uint? TcpCloseWait { get; init; }
    public uint? TcpDiscon { get; init; }
    public uint? TcpReopen { get; init; }
    public uint? TcpLastSr { get; init; }
    public uint? TcpLastIr { get; init; }
    public int? TcpLastRecv { get; init; }
    public int? TcpLastSend { get; init; }
    public uint? TcpLastTxFsr { get; init; }
}
