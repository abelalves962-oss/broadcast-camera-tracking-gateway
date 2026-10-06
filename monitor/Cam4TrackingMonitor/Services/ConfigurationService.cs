using System.Globalization;
using Cam4TrackingMonitor.Logging;
using Cam4TrackingMonitor.Models;
using Cam4TrackingMonitor.Parsers;

namespace Cam4TrackingMonitor.Services;

public sealed class ConfigurationService
{
    private readonly Cam4CommandService _commands;
    private readonly DiagnosticPollingService _polling;
    private readonly ConfigurationParser _parser;
    private readonly DiagnosticParser _diagnosticParser;
    private readonly ConnectionManager _connection;
    private readonly EventLogger _logger;

    public TrackingBoxConfiguration? LastAppliedConfiguration { get; private set; }
    public string ActiveHost => _commands.Host;

    public ConfigurationService(Cam4CommandService commands, DiagnosticPollingService polling, ConfigurationParser parser,
        DiagnosticParser diagnosticParser, ConnectionManager connection, EventLogger logger)
        => (_commands, _polling, _parser, _diagnosticParser, _connection, _logger) = (commands, polling, parser, diagnosticParser, connection, logger);

    public async Task<(bool Success, TrackingBoxConfiguration? Configuration, string Message)> ReadAsync(CancellationToken cancellationToken = default)
    {
        _polling.Pause();
        try
        {
            var result = await _commands.ExecuteAsync("??", cancellationToken: cancellationToken);
            if (!result.Success) return (false, null, result.Error ?? "Falha TCP.");
            return _parser.TryParse(result.Response, out var configuration)
                ? (true, configuration, "Configuração lida com sucesso.")
                : (false, null, "Resposta de configuração inválida ou incompleta.");
        }
        finally { _polling.Resume(); }
    }

    public async Task<(bool Success, string Message)> ApplyAsync(TrackingBoxConfiguration current, TrackingBoxConfiguration edited, CancellationToken cancellationToken = default)
    {
        var validation = Validate(edited);
        if (validation is not null) return (false, validation);
        var changes = BuildChanges(current, edited).ToList();
        if (changes.Count == 0) return (true, "Nenhuma alteração pendente.");

        _polling.Pause();
        try
        {
            foreach (var change in changes)
            {
                var result = await _commands.ExecuteAsync(change.Command, cancellationToken: cancellationToken);
                var accepted = result.Success && !result.Response.Contains("Falha", StringComparison.OrdinalIgnoreCase)
                    && !result.Response.Contains("nao identificado", StringComparison.OrdinalIgnoreCase)
                    && !result.Response.Contains("nao validado", StringComparison.OrdinalIgnoreCase);
                await LogConfig(change.Name, change.OldValue, change.NewValue, accepted ? "SUCCESS" : "FAILED", result.Response.Length > 0 ? result.Response.Trim() : result.Error ?? string.Empty);
                if (!accepted) return (false, $"Falha ao aplicar {change.Name}: {result.Error ?? result.Response}");
            }
            LastAppliedConfiguration = edited.Clone();
            return (true, $"{changes.Count} alteração(ões) aplicada(s) em RAM. A configuração ainda não foi salva.");
        }
        finally { _polling.Resume(); }
    }

    public async Task<CommandResult> SaveAsync(CancellationToken cancellationToken = default)
    {
        _polling.Pause();
        try
        {
            var result = await _commands.ExecuteAsync("salva:", cancellationToken: cancellationToken);
            await _logger.WriteAsync(new MonitorEvent(DateTime.Now, result.Success ? EventSeverity.Info : EventSeverity.Error, "SYSTEM", "SAVE", string.Empty, string.Empty,
                result.Success ? "ACK RECEIVED" : "FAILED", result.Success ? result.Response.Trim() : result.Error ?? string.Empty));
            return result;
        }
        finally { _polling.Resume(); }
    }

    public async Task<CommandResult> RestoreDefaultAsync(CancellationToken cancellationToken = default)
    {
        _polling.Pause();
        try
        {
            var result = await _commands.ExecuteAsync("default:", cancellationToken: cancellationToken);
            await _logger.WriteAsync(new MonitorEvent(DateTime.Now, result.Success ? EventSeverity.Warning : EventSeverity.Error, "SYSTEM", "DEFAULT", string.Empty, "DEFAULT RAM",
                result.Success ? "SUCCESS" : "FAILED", result.Success ? result.Response.Trim() : result.Error ?? string.Empty));
            return result;
        }
        finally { _polling.Resume(); }
    }

    public async Task<(bool Success, TrackingBoxConfiguration? Configuration, TrackingBoxTelemetry? Telemetry, string Message)> RebootAndReconnectAsync(string newHost, CancellationToken cancellationToken = default)
    {
        var activeHost = _commands.Host;
        _polling.Pause();
        _connection.Rebooting();
        try
        {
            await _logger.WriteAsync(new MonitorEvent(DateTime.Now, EventSeverity.Info, "SYSTEM", "REBOOT", _commands.Host, newHost, "SENT", "Desconexão esperada."));
            _ = await _commands.ExecuteAsync("reboot:", expectResponse: false, cancellationToken);
            var candidateHosts = new[] { activeHost, newHost }.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            var delays = new[] { 1000, 2000, 4000, 8000, 8000 };
            foreach (var delay in delays)
            {
                await Task.Delay(delay, cancellationToken);
                foreach (var candidateHost in candidateHosts)
                {
                    _commands.Host = candidateHost;
                    var configResult = await _commands.ExecuteAsync("??", cancellationToken: cancellationToken);
                    if (!configResult.Success || !_parser.TryParse(configResult.Response, out var configuration)) continue;
                    var diagnosticResult = await _commands.ExecuteAsync("?D", cancellationToken: cancellationToken);
                    if (!diagnosticResult.Success || !_diagnosticParser.TryParse(diagnosticResult.Response, out var telemetry)) continue;
                    _connection.Connected();
                    var persisted = LastAppliedConfiguration is null || Equivalent(LastAppliedConfiguration, configuration);
                    if (!persisted)
                        await _logger.WriteAsync(new MonitorEvent(DateTime.Now, EventSeverity.Error, "CONFIG", "PERSISTENCE VALIDATION", "EXPECTED", "DIFFERENT", "FAILED", "A configuração relida após reboot difere da última configuração aplicada."));
                    return (true, configuration, telemetry, persisted ? "Tracking Box reconectada; configuração e diagnóstico validados." : "Tracking Box reconectada, mas a configuração persistida difere da esperada. Revise o log.");
                }
            }
            _commands.Host = activeHost;
            _connection.Disconnected();
            return (false, null, null, "A Tracking Box não respondeu no novo endereço após o reboot.");
        }
        finally { _polling.Resume(); }
    }

    public static bool HasNetworkChanges(TrackingBoxConfiguration current, TrackingBoxConfiguration edited)
        => current.Ip != edited.Ip || current.SubnetMask != edited.SubnetMask || current.Gateway != edited.Gateway || current.Dns != edited.Dns;

    private static string? Validate(TrackingBoxConfiguration value)
    {
        foreach (var (name, ip) in new[] { ("IP", value.Ip), ("Máscara", value.SubnetMask), ("Gateway", value.Gateway), ("DNS", value.Dns) }
            .Concat(value.Destinations.Select(destination => ($"IP destino {destination.Number}", destination.Ip))))
            if (!IsStrictIpv4(ip)) return $"{name} inválido: {ip}";
        if (value.DestinationCount is < 0 or > 7) return "O número de destinos deve estar entre 0 e 7.";
        if (value.Destinations.Any(destination => destination.Port is < 1 or > 65535)) return "As portas UDP devem estar entre 1 e 65535.";
        if (!new[] { "XYZ", "XZY", "YXZ", "YZX", "ZXY", "ZYX" }.Contains(value.AxisOrder)) return "Ordem de eixos inválida.";
        if (!new[] { "PTR", "PRT", "TPR", "TRP", "RPT", "RTP" }.Contains(value.AngleOrder)) return "Ordem de ângulos inválida.";
        return null;
    }

    private static bool IsStrictIpv4(string value) => value.Split('.') is { Length: 4 } parts && parts.All(part => byte.TryParse(part, out _));

    private static bool Equivalent(TrackingBoxConfiguration expected, TrackingBoxConfiguration actual)
    {
        bool Close(double a, double b) => Math.Abs(a - b) < 0.0001;
        return expected.Ip == actual.Ip && expected.SubnetMask == actual.SubnetMask && expected.Gateway == actual.Gateway && expected.Dns == actual.Dns
            && expected.DhcpFlag == actual.DhcpFlag && expected.DestinationCount == actual.DestinationCount
            && expected.Destinations.Zip(actual.Destinations).All(pair => pair.First.Ip == pair.Second.Ip && pair.First.Port == pair.Second.Port)
            && expected.AxisOrder == actual.AxisOrder && expected.AngleOrder == actual.AngleOrder
            && expected.Inversions.X == actual.Inversions.X && expected.Inversions.Y == actual.Inversions.Y && expected.Inversions.Z == actual.Inversions.Z
            && expected.Inversions.Pan == actual.Inversions.Pan && expected.Inversions.Tilt == actual.Inversions.Tilt && expected.Inversions.Roll == actual.Inversions.Roll
            && expected.Inversions.Focus == actual.Inversions.Focus && expected.Inversions.Zoom == actual.Inversions.Zoom
            && Close(expected.Offsets.X, actual.Offsets.X) && Close(expected.Offsets.Y, actual.Offsets.Y) && Close(expected.Offsets.Z, actual.Offsets.Z)
            && Close(expected.Offsets.Pan, actual.Offsets.Pan) && Close(expected.Offsets.Tilt, actual.Offsets.Tilt) && Close(expected.Offsets.Roll, actual.Offsets.Roll);
    }

    private static IEnumerable<ConfigChange> BuildChanges(TrackingBoxConfiguration old, TrackingBoxConfiguration value)
    {
        if (old.Ip != value.Ip) yield return new("IP", old.Ip, value.Ip, $"ip:{value.Ip}");
        if (old.SubnetMask != value.SubnetMask) yield return new("SUBNET", old.SubnetMask, value.SubnetMask, $"sn:{value.SubnetMask}");
        if (old.Gateway != value.Gateway) yield return new("GATEWAY", old.Gateway, value.Gateway, $"gw:{value.Gateway}");
        if (old.Dns != value.Dns) yield return new("DNS", old.Dns, value.Dns, $"dns:{value.Dns}");
        if (old.DhcpFlag != value.DhcpFlag) yield return new("DHCP FLAG", old.DhcpFlag.ToString(), value.DhcpFlag.ToString(), $"dhcp:{(value.DhcpFlag ? "on" : "off")}");
        if (old.DestinationCount != value.DestinationCount) yield return new("DESTINATIONS", old.DestinationCount.ToString(), value.DestinationCount.ToString(), $"clientes:{value.DestinationCount}");
        for (var index = 0; index < 7; index++)
        {
            var before = old.Destinations[index]; var after = value.Destinations[index]; var number = index + 1;
            if (before.Ip != after.Ip) yield return new($"IP R{number}", before.Ip, after.Ip, $"ip{number}:{after.Ip}");
            if (before.Port != after.Port) yield return new($"PORT R{number}", before.Port.ToString(), after.Port.ToString(), $"porta{number}:{after.Port}");
        }
        if (old.AxisOrder != value.AxisOrder) yield return new("AXIS ORDER", old.AxisOrder, value.AxisOrder, $"oe:{value.AxisOrder.ToLowerInvariant()}");
        if (old.AngleOrder != value.AngleOrder) yield return new("ANGLE ORDER", old.AngleOrder, value.AngleOrder, $"oa:{value.AngleOrder.ToLowerInvariant()}");
        foreach (var change in InversionChanges(old.Inversions, value.Inversions)) yield return change;
        foreach (var change in OffsetChanges(old.Offsets, value.Offsets)) yield return change;
    }

    private static IEnumerable<ConfigChange> InversionChanges(AxisInversions old, AxisInversions value)
    {
        foreach (var entry in new[] { ("X", old.X, value.X, "x"), ("Y", old.Y, value.Y, "y"), ("Z", old.Z, value.Z, "z"), ("PAN", old.Pan, value.Pan, "p"), ("TILT", old.Tilt, value.Tilt, "t"), ("ROLL", old.Roll, value.Roll, "r"), ("FOCUS", old.Focus, value.Focus, "f"), ("ZOOM", old.Zoom, value.Zoom, "Z") })
            if (entry.Item2 != entry.Item3) yield return new($"INVERT {entry.Item1}", entry.Item2.ToString(), entry.Item3.ToString(), $"inv:{entry.Item4}");
    }

    private static IEnumerable<ConfigChange> OffsetChanges(TrackingOffsets old, TrackingOffsets value)
    {
        foreach (var entry in new[] { ("X", old.X, value.X, "x"), ("Y", old.Y, value.Y, "y"), ("Z", old.Z, value.Z, "z"), ("PAN", old.Pan, value.Pan, "p"), ("TILT", old.Tilt, value.Tilt, "t"), ("ROLL", old.Roll, value.Roll, "r") })
            if (entry.Item2 != entry.Item3) yield return new($"OFFSET {entry.Item1}", F(entry.Item2), F(entry.Item3), $"of{entry.Item4}:{F(entry.Item3)}");
        static string F(double number) => number.ToString("0.######", CultureInfo.InvariantCulture);
    }

    private Task LogConfig(string name, string oldValue, string newValue, string result, string message)
        => _logger.WriteAsync(new MonitorEvent(DateTime.Now, result == "SUCCESS" ? EventSeverity.Info : EventSeverity.Error, "CONFIG", name, oldValue, newValue, result, message));
    private sealed record ConfigChange(string Name, string OldValue, string NewValue, string Command);
}
