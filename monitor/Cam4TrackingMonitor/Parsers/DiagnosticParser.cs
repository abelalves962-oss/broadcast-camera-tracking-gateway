using System.Globalization;
using System.Text.RegularExpressions;
using Cam4TrackingMonitor.Models;

namespace Cam4TrackingMonitor.Parsers;

public sealed partial class DiagnosticParser
{
    [GeneratedRegex(@"(?<name>[A-Z][A-Z0-9_]*):(?<value>0x[0-9A-Fa-f]+|-?\d+)")]
    private static partial Regex FieldRegex();

    [GeneratedRegex(@"UPTIME:(?<value>\d+d\s+\d{2}:\d{2}:\d{2})")]
    private static partial Regex UptimeRegex();

    public bool TryParse(string response, out TrackingBoxTelemetry telemetry)
    {
        var fields = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in FieldRegex().Matches(response ?? string.Empty))
        {
            var text = match.Groups["value"].Value;
            long? parsed = text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                ? uint.TryParse(text[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var hexValue) ? hexValue : (long?)null
                : long.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var decimalValue) ? decimalValue : null;
            if (parsed.HasValue)
                fields[match.Groups["name"].Value] = parsed.Value;
        }

        uint? Get(string name) => fields.TryGetValue(name, out var value) && value >= 0 && value <= uint.MaxValue ? (uint)value : null;
        int? GetInt(string name) => fields.TryGetValue(name, out var value) && value >= int.MinValue && value <= int.MaxValue ? (int)value : null;
        var uptimeMatch = UptimeRegex().Match(response ?? string.Empty);
        telemetry = new TrackingBoxTelemetry
        {
            RawResponse = response ?? string.Empty,
            UartError = Get("UART_ERR"), LastUartError = Get("LAST"), Ore = Get("ORE"), Fe = Get("FE"), Ne = Get("NE"), Pe = Get("PE"),
            FrameBad = Get("FRAME_BAD"), RxTotal = Get("RX_TOTAL"), RxOk = Get("RX_OK"), RxBad = Get("RX_BAD"),
            LastRxEventMs = Get("LAST_RX_EVENT_MS"), LastRxOkMs = Get("LAST_RX_OK_MS"),
            SyncTotal = Get("SYNC_TOTAL"), SyncLost = Get("SYNC_LOST"), LastSyncMs = Get("LAST_SYNC_MS"),
            FreeDTotal = Get("FREED_TOTAL"), FreeDOk = Get("FREED_OK"), FreeDError = Get("FREED_ERR"), FreeDDrop = Get("FREED_DROP"), LastFreeDMs = Get("LAST_FREED_MS"),
            EthernetLink = Get("ETH_LINK"), EthernetDown = Get("ETH_DOWN"), EthernetUp = Get("ETH_UP"),
            Uptime = uptimeMatch.Success ? uptimeMatch.Groups["value"].Value : null,
            TcpAccept = Get("TCP_ACCEPT"), TcpRxOk = Get("TCP_RX_OK"), TcpRxErr = Get("TCP_RX_ERR"),
            TcpSendCall = Get("TCP_SEND_CALL"), TcpSendRetOk = Get("TCP_SEND_RET_OK"), TcpSendRetErr = Get("TCP_SEND_RET_ERR"),
            TcpSendOk = Get("TCP_SENDOK"), TcpTimeout = Get("TCP_TIMEOUT"), TcpCloseWait = Get("TCP_CLOSE_WAIT"),
            TcpDiscon = Get("TCP_DISCON"), TcpReopen = Get("TCP_REOPEN"), TcpLastSr = Get("TCP_LAST_SR"), TcpLastIr = Get("TCP_LAST_IR"),
            TcpLastRecv = GetInt("TCP_LAST_RECV"), TcpLastSend = GetInt("TCP_LAST_SEND"), TcpLastTxFsr = Get("TCP_LAST_TX_FSR")
        };
        return response?.Contains("?D", StringComparison.OrdinalIgnoreCase) == true && fields.Count > 0;
    }
}
