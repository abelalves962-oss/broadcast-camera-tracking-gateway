using Cam4TrackingMonitor.Parsers;
using Cam4TrackingMonitor.Models;
using Cam4TrackingMonitor.Services;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Cam4TrackingMonitor.Controls;

if (args.Length == 2 && args[0] is "--stress-offline" or "--stress-simulated")
    return await RunStressAsync(args[0], int.Parse(args[1]));

static async Task<int> RunStressAsync(string mode, int seconds)
{
    using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(seconds));
    TcpListener? listener = null;
    Task? server = null;
    var port = 1;
    if (mode == "--stress-simulated")
    {
        listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var localListener = listener;
        server = Task.Run(async () =>
        {
            while (!stop.IsCancellationRequested)
            {
                try
                {
                    using var client = await localListener.AcceptTcpClientAsync(stop.Token);
                    await using var stream = client.GetStream();
                    var request = new byte[16];
                    _ = await stream.ReadAsync(request, stop.Token);
                    var response = $"?D UART_ERR:0 LAST:0x00000000 ORE:0 FE:0 NE:0 PE:0 FRAME_BAD:0 RX_TOTAL:0 RX_OK:0 RX_BAD:0 LAST_RX_EVENT_MS:1000 LAST_RX_OK_MS:1000 SYNC_TOTAL:0 SYNC_LOST:0 LAST_SYNC_MS:1000 FREED_TOTAL:0 FREED_OK:0 FREED_ERR:0 FREED_DROP:0 LAST_FREED_MS:1000 ETH_LINK:1 ETH_DOWN:0 ETH_UP:1 UPTIME:0d 00:00:01 ?\n";
                    await stream.WriteAsync(Encoding.ASCII.GetBytes(response), stop.Token);
                }
                catch (OperationCanceledException) { break; }
            }
        });
    }

    var clientTransport = new Cam4TcpClient { Timeout = TimeSpan.FromMilliseconds(500) };
    var commands = new Cam4CommandService(clientTransport) { Host = "127.0.0.1", Port = port };
    var polling = new DiagnosticPollingService(commands, new DiagnosticParser(), new ConnectionManager());
    long received = 0;
    polling.TelemetryReceived += (_, _) => Interlocked.Increment(ref received);
    using var process = Process.GetCurrentProcess();
    process.Refresh();
    var cpuStart = process.TotalProcessorTime;
    var memoryStart = process.WorkingSet64;
    var started = Stopwatch.StartNew();
    await polling.RunAsync(stop.Token);
    started.Stop();
    if (server is not null) { try { await server; } catch (OperationCanceledException) { } }
    listener?.Stop();
    process.Refresh();
    var cpu = process.TotalProcessorTime - cpuStart;
    var diagnostics = polling.GetDiagnostics();
    Console.WriteLine($"STRESS mode={mode} elapsed_s={started.Elapsed.TotalSeconds:F1} polls={diagnostics.PollCount} received={received} max_concurrent={diagnostics.MaxConcurrent} in_progress={diagnostics.InProgress} failures={diagnostics.ConsecutiveFailures} last_ms={diagnostics.LastDurationMs:F1} max_ms={diagnostics.MaxDurationMs:F1} cpu_s={cpu.TotalSeconds:F3} cpu_percent={(cpu.TotalMilliseconds / (started.Elapsed.TotalMilliseconds * Environment.ProcessorCount) * 100):F3} memory_start_mb={memoryStart / 1048576d:F1} memory_end_mb={process.WorkingSet64 / 1048576d:F1} threads={process.Threads.Count} pending_work={ThreadPool.PendingWorkItemCount}");
    return diagnostics.MaxConcurrent == 1 && diagnostics.InProgress == 0 ? 0 : 2;
}

var failures = new List<string>();
void Check(bool condition, string name) { if (!condition) failures.Add(name); else Console.WriteLine($"PASS: {name}"); }

TestMaintenanceAuthorization();

void TestMaintenanceAuthorization()
{
    var testDirectory = Path.Combine(Path.GetTempPath(), $"cam4-auth-tests-{Guid.NewGuid():N}");
    var settingsPath = Path.Combine(testDirectory, "maintenance-auth.json");
    var originalPassword = Convert.ToBase64String(RandomNumberGenerator.GetBytes(18));
    var newPassword = Convert.ToBase64String(RandomNumberGenerator.GetBytes(18));
    var incorrectPassword = Convert.ToBase64String(RandomNumberGenerator.GetBytes(18));
    try
    {
        var authorization = new MaintenanceAuthorizationService(settingsPath);
        Check(!File.Exists(settingsPath), "Autorização: iniciar não cria credencial padrão");
        Check(!authorization.CreatePassword("12345") && !File.Exists(settingsPath),
            "Autorização: senha com menos de 6 caracteres é recusada");
        Check(authorization.CreatePassword(originalPassword) && File.Exists(settingsPath),
            "Autorização: cadastro cria maintenance-auth.json");

        var firstJson = File.ReadAllText(settingsPath);
        using var firstDocument = JsonDocument.Parse(firstJson);
        var firstRoot = firstDocument.RootElement;
        var firstSalt = Convert.FromBase64String(firstRoot.GetProperty("Salt").GetString()!);
        var firstHash = Convert.FromBase64String(firstRoot.GetProperty("Hash").GetString()!);
        Check(!firstJson.Contains(originalPassword, StringComparison.Ordinal),
            "Autorização: JSON não contém senha em texto claro");
        Check(firstRoot.GetProperty("Version").GetInt32() == 1 &&
              firstRoot.GetProperty("Iterations").GetInt32() == 210_000 &&
              firstSalt.Length == 16 && firstHash.Length == 32,
            "Autorização: JSON usa versão, 210000 iterações, salt 128-bit e hash 256-bit");
        Check(authorization.VerifyPassword(originalPassword), "Autorização: senha correta autentica");
        Check(!authorization.VerifyPassword(incorrectPassword), "Autorização: senha incorreta é recusada");

        var jsonBeforeInvalidChange = File.ReadAllText(settingsPath);
        Check(authorization.ChangePasswordCredentials(incorrectPassword, newPassword) == PasswordChangeResult.IncorrectCurrentPassword &&
              File.ReadAllText(settingsPath) == jsonBeforeInvalidChange,
            "Autorização: senha atual incorreta não altera o arquivo");
        Check(authorization.ChangePasswordCredentials(originalPassword, newPassword) == PasswordChangeResult.Success,
            "Autorização: troca de senha funciona");
        var secondJson = File.ReadAllText(settingsPath);
        using var secondDocument = JsonDocument.Parse(secondJson);
        var secondSalt = Convert.FromBase64String(secondDocument.RootElement.GetProperty("Salt").GetString()!);
        Check(!firstSalt.SequenceEqual(secondSalt), "Autorização: troca gera novo salt aleatório");
        Check(!authorization.VerifyPassword(originalPassword), "Autorização: senha antiga deixa de funcionar");
        Check(authorization.VerifyPassword(newPassword), "Autorização: senha nova autentica");

        File.Delete(settingsPath);
        Check(!authorization.VerifyPassword(newPassword) && !authorization.IsAuthorized,
            "Autorização: arquivo apagado é tratado sem exceção e sem autorização");
        var restartedAuthorization = new MaintenanceAuthorizationService(settingsPath);
        Check(!File.Exists(settingsPath) && !restartedAuthorization.VerifyPassword(newPassword),
            "Autorização: reinício não recria credencial padrão");
        CryptographicOperations.ZeroMemory(firstSalt);
        CryptographicOperations.ZeroMemory(firstHash);
        CryptographicOperations.ZeroMemory(secondSalt);
    }
    finally
    {
        if (Directory.Exists(testDirectory)) Directory.Delete(testDirectory, true);
    }
}

await TestAutomaticRecoveryAsync();

async Task TestAutomaticRecoveryAsync()
{
    var probe = new TcpListener(IPAddress.Loopback, 0);
    probe.Start();
    var recoveryPort = ((IPEndPoint)probe.LocalEndpoint).Port;
    probe.Stop();
    var recoveryConnection = new ConnectionManager();
    var recoveryClient = new Cam4TcpClient { Timeout = TimeSpan.FromMilliseconds(100), ReceiveIdleTimeout = TimeSpan.FromMilliseconds(20) };
    var recoveryCommands = new Cam4CommandService(recoveryClient) { Host = "127.0.0.1", Port = recoveryPort };
    var recoveryPolling = new DiagnosticPollingService(recoveryCommands, new DiagnosticParser(), recoveryConnection)
        { Interval = TimeSpan.FromMilliseconds(40) };
    var firstValidAfterOffline = 0;
    recoveryPolling.PollSucceededDetailed += (_, success) =>
    {
        if (success.FirstValidAfterOffline) Interlocked.Increment(ref firstValidAfterOffline);
    };
    using var pollingStop = new CancellationTokenSource();
    var pollingTask = recoveryPolling.RunAsync(pollingStop.Token);

    await Task.Delay(800);
    Check(recoveryConnection.State == ConnectionState.Offline, "C: Box desligada permanece OFFLINE");
    using var server1Stop = new CancellationTokenSource();
    var (server1, listener1) = StartDiagnosticServer(recoveryPort, server1Stop.Token);
    Check(await WaitForState(ConnectionState.Online, TimeSpan.FromSeconds(2)), "C: ligar Box recupera automaticamente ONLINE");

    listener1.Stop(); server1Stop.Cancel();
    try { await server1; } catch (Exception) { }
    Check(await WaitForState(ConnectionState.Offline, TimeSpan.FromSeconds(2)), "A: três falhas levam a OFFLINE");
    using var server2Stop = new CancellationTokenSource();
    var (server2, listener2) = StartDiagnosticServer(recoveryPort, server2Stop.Token);
    Check(await WaitForState(ConnectionState.Online, TimeSpan.FromSeconds(2)), "A/B: primeira resposta ?D válida recupera ONLINE");
    Check(firstValidAfterOffline >= 2, "Recuperações identificam FIRST VALID RESPONSE AFTER OFFLINE");
    var recoveryDiagnostics = recoveryPolling.GetDiagnostics();
    Check(recoveryDiagnostics.MaxConcurrent == 1, "Recuperação mantém no máximo um poll simultâneo");
    Check(recoveryDiagnostics.PollCount == recoveryDiagnostics.ValidResponses + recoveryDiagnostics.InvalidResponses &&
          recoveryDiagnostics.ConnectFailures + recoveryDiagnostics.WriteFailures + recoveryDiagnostics.ReadFailures +
          recoveryDiagnostics.ParseFailures + recoveryDiagnostics.ValidationFailures == recoveryDiagnostics.InvalidResponses,
        "Resumo da sessão classifica cada tentativa uma única vez");

    pollingStop.Cancel(); listener2.Stop(); server2Stop.Cancel();
    try { await pollingTask; } catch (OperationCanceledException) { }
    try { await server2; } catch (Exception) { }

    async Task<bool> WaitForState(ConnectionState expected, TimeSpan timeout)
    {
        var until = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < until) { if (recoveryConnection.State == expected) return true; await Task.Delay(20); }
        return false;
    }
}

static (Task Task, TcpListener Listener) StartDiagnosticServer(int port, CancellationToken token)
{
    var listener = new TcpListener(IPAddress.Loopback, port);
    listener.Start();
    var task = Task.Run(async () =>
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                using var accepted = await listener.AcceptTcpClientAsync(token);
                await using var stream = accepted.GetStream();
                var request = new byte[16];
                _ = await stream.ReadAsync(request, token);
                const string response = "?D RX_TOTAL:1 RX_OK:1 RX_BAD:0 SYNC_TOTAL:1 SYNC_LOST:0 FREED_TOTAL:1 FREED_OK:1 FREED_ERR:0 FREED_DROP:0 ETH_LINK:1 ETH_DOWN:2 ETH_UP:3 UPTIME:0d 00:00:01 ?\n";
                await stream.WriteAsync(Encoding.ASCII.GetBytes(response), token);
            }
            catch (OperationCanceledException) { break; }
            catch (SocketException) when (token.IsCancellationRequested) { break; }
        }
    });
    return (task, listener);
}

var diagnostic = "?D UART_ERR:3 LAST:0x00000008 ORE:2 FE:1 NE:0 PE:0 FRAME_BAD:7 " +
                 "RX_TOTAL:582341 RX_OK:582329 RX_BAD:12 LAST_RX_EVENT_MS:4 LAST_RX_OK_MS:4 " +
                 "SYNC_TOTAL:582337 SYNC_LOST:2 LAST_SYNC_MS:6 FREED_TOTAL:1164630 FREED_OK:1164601 " +
                 "FREED_ERR:29 FREED_DROP:5 LAST_FREED_MS:8 ETH_LINK:1 ETH_DOWN:2 ETH_UP:2 " +
                 "UNKNOWN_FUTURE:999 UPTIME:3d 17:42:09 ?\n";
var diagnosticParser = new DiagnosticParser();
Check(diagnosticParser.TryParse(diagnostic, out var telemetry), "Parser ?D aceita resposta realista");
Check(telemetry.RxTotal == 582341 && telemetry.RxBad == 12, "Campos RX por nome");
Check(telemetry.LastUartError == 8, "Campo hexadecimal LAST");
Check(telemetry.FreeDDrop == 5 && telemetry.Uptime == "3d 17:42:09", "Free-D drop e uptime");
var tcpDiagnostic = diagnostic.Replace("UNKNOWN_FUTURE:999", "TCP_ACCEPT:10 TCP_RX_OK:9 TCP_RX_ERR:1 TCP_SEND_CALL:9 TCP_SEND_RET_OK:8 TCP_SEND_RET_ERR:1 TCP_SENDOK:7 TCP_TIMEOUT:1 TCP_CLOSE_WAIT:2 TCP_DISCON:2 TCP_REOPEN:1 TCP_LAST_SR:0x17 TCP_LAST_IR:0x10 TCP_LAST_RECV:308 TCP_LAST_SEND:-1 TCP_LAST_TX_FSR:2048 UNKNOWN_FUTURE:999");
Check(diagnosticParser.TryParse(tcpDiagnostic, out var tcpTelemetry) && tcpTelemetry.TcpAccept == 10 && tcpTelemetry.TcpSendOk == 7 && tcpTelemetry.TcpLastSr == 0x17 && tcpTelemetry.TcpLastSend == -1,
    "Parser ?D novo aceita instrumentacao TCP");
var legacyTcp = "?D RX_TOTAL:1 ETH_LINK:1 UPTIME:0d 00:00:01 ?\n";
Check(diagnosticParser.TryParse(legacyTcp, out var legacyTelemetry) && legacyTelemetry.TcpAccept is null && legacyTelemetry.TcpLastSr is null,
    "Firmware legado sem campos TCP vira N/A");
var incomplete = "?D RX_TOTAL:10 UNKNOWN:77 UPTIME:0d 00:00:01 ?\n";
Check(diagnosticParser.TryParse(incomplete, out var partial), "Resposta incompleta não derruba parser");
Check(partial.RxTotal == 10 && partial.SyncTotal is null, "Campo conhecido ausente vira N/A/null");

var waitingTelemetry = new TrackingBoxTelemetry { EthernetLink = 1, RxTotal = 0, RxOk = 0, RxBad = 0, UartError = 0, SyncTotal = 0, SyncLost = 0, FreeDTotal = 0, FreeDError = 0, FreeDDrop = 0 };
var waitingStates = new TelemetryStateEvaluator().Evaluate(waitingTelemetry, ConnectionState.Online);
Check(waitingStates.Serial == VisualState.Waiting, "Serial sem dados fica WAITING");
Check(waitingStates.Sync == VisualState.Waiting, "SYNC sem sinal fica WAITING");
Check(waitingStates.FreeD == VisualState.Inactive, "Free-D sem RX/SYNC fica INACTIVE");

var healthStart = DateTime.Now;
var health = new HealthMetric("ETH DOWN");
health.Observe(0, healthStart);
Check(health.State == HealthMetricState.Ok, "Health contador zero aparece OK");
health.Observe(1, healthStart.AddSeconds(1));
Check(health.State == HealthMetricState.Active, "Health incremento recente aparece ACTIVE");
health.Refresh(healthStart.AddSeconds(7));
Check(health.State == HealthMetricState.Event && health.Value == 1, "Health ACTIVE retorna a EVENT após 5 segundos");
var historicalHealth = new HealthMetric("ETH DOWN");
historicalHealth.Observe(1, healthStart);
Check(historicalHealth.State == HealthMetricState.Event, "Contador histórico inicial maior que zero aparece EVENT");

var history = new TelemetryHistoryService();
var linkSample = new TrackingBoxTelemetry { Timestamp = healthStart, EthernetLink = 1, EthernetDown = 1 };
var afterValid = history.AddTelemetry(linkSample);
var afterFailure = history.AddPollFailure(healthStart.AddSeconds(1));
Check(afterValid[^1].EthernetLink == 1 && afterValid[^1].PollSuccess, "ETH LINK e poll válido registrados independentemente");
Check(afterFailure[^1].EthernetLink is null && !afterFailure[^1].PollSuccess && afterFailure[^1].Events == TelemetryEvent.PollFailure,
    "Falha APP BOX não cria ETH_DOWN artificial");

var bothUpHistory = new TelemetryHistoryService();
var networkStart = healthStart.AddMinutes(1);
IReadOnlyList<TelemetrySample> bothUpSamples = Array.Empty<TelemetrySample>();
for (var index = 0; index < 300; index++)
    bothUpSamples = bothUpHistory.AddTelemetry(new TrackingBoxTelemetry
        { Timestamp = networkStart.AddSeconds(index), EthernetLink = 1 });
Check(bothUpSamples.Count == 300 && bothUpSamples.All(sample => sample.EthernetLink == 1 && sample.PollSuccess),
    "Network: ETH LINK e APP BOX UP preservados em 300 amostras");
Check(TelemetryChart.MapNetworkLevel(1, true) != TelemetryChart.MapNetworkLevel(1, false) &&
      TelemetryChart.MapNetworkLevel(1, true) is > 0 and < 1 &&
      TelemetryChart.MapNetworkLevel(1, false) is > 0 and < 1,
    "Network: duas linhas UP usam níveis internos visíveis e separados");

var appAlternatingHistory = new TelemetryHistoryService();
IReadOnlyList<TelemetrySample> appAlternatingSamples = Array.Empty<TelemetrySample>();
for (var index = 0; index < 300; index++)
{
    appAlternatingSamples = appAlternatingHistory.AddTelemetry(new TrackingBoxTelemetry
        { Timestamp = networkStart.AddSeconds(index * 2), EthernetLink = 1 });
    if ((index & 1) != 0)
        appAlternatingSamples = appAlternatingHistory.AddPollFailure(networkStart.AddSeconds(index * 2 + 1));
}
Check(appAlternatingSamples.Where(sample => sample.PollSuccess).All(sample => sample.EthernetLink == 1) &&
      appAlternatingSamples.Any(sample => !sample.PollSuccess && sample.EthernetLink is null),
    "Network: falhas APP BOX não derrubam ETH LINK");

var ethAlternatingHistory = new TelemetryHistoryService();
IReadOnlyList<TelemetrySample> ethAlternatingSamples = Array.Empty<TelemetrySample>();
for (var index = 0; index < 300; index++)
    ethAlternatingSamples = ethAlternatingHistory.AddTelemetry(new TrackingBoxTelemetry
        { Timestamp = networkStart.AddSeconds(index), EthernetLink = (uint)(index % 2) });
Check(ethAlternatingSamples.All(sample => sample.PollSuccess) &&
      ethAlternatingSamples.Any(sample => sample.EthernetLink == 0) &&
      ethAlternatingSamples.Any(sample => sample.EthernetLink == 1),
    "Network: somente ETH LINK alterna quando polling permanece válido");
var networkMarkerMask = TelemetryChart.RelevantEventsFor(TelemetryChartKind.Network);
Check((networkMarkerMask & TelemetryEvent.PollFailure) == 0,
    "PollFailure não gera marcador vermelho de Network");
Check((networkMarkerMask & TelemetryEvent.EthernetDown) != 0,
    "Incremento ETH_DOWN gera marcador vermelho de Network");

var readReset = new CommandResult(false, string.Empty, "reset", CommunicationPhase.Read,
    typeof(IOException).FullName, SocketError.ConnectionReset.ToString());
var writeReset = readReset with { FailurePhase = CommunicationPhase.Write };
Check(DiagnosticPollingService.ClassifyFailure(readReset) == PollFailureKind.ConnectionReset &&
      readReset.FailurePhase == CommunicationPhase.Read && writeReset.FailurePhase == CommunicationPhase.Write,
    "ConnectionReset preserva classificação READ/WRITE da etapa");
var timeoutResult = new CommandResult(false, string.Empty, "timeout", CommunicationPhase.Read,
    typeof(OperationCanceledException).FullName, IsTimeout: true);
Check(DiagnosticPollingService.ClassifyFailure(timeoutResult) == PollFailureKind.Timeout,
    "Timeout identificado corretamente");

var eventCalculator = new CounterRateCalculator();
_ = eventCalculator.Add(new TrackingBoxTelemetry { Timestamp = healthStart, UartError = 0, RxBad = 0, FrameBad = 0, Ore = 0, Fe = 0, Ne = 0, Pe = 0 });
var zeroRates = eventCalculator.Add(new TrackingBoxTelemetry { Timestamp = healthStart.AddSeconds(1), UartError = 0, RxBad = 0, FrameBad = 0, Ore = 0, Fe = 0, Ne = 0, Pe = 0 });
Check(zeroRates.Events == TelemetryEvent.None, "Taxa e contadores zero não geram marca de erro");
var serialEvents = eventCalculator.Add(new TrackingBoxTelemetry { Timestamp = healthStart.AddSeconds(2), UartError = 1, RxBad = 1, FrameBad = 1, Ore = 1, Fe = 1, Ne = 1, Pe = 1 });
var serialMask = TelemetryEvent.UartError | TelemetryEvent.RxBad | TelemetryEvent.FrameBad | TelemetryEvent.OverrunError |
                 TelemetryEvent.FramingError | TelemetryEvent.NoiseError | TelemetryEvent.ParityError;
Check((serialEvents.Events & serialMask) == serialMask, "Eventos seriais reais são marcados por incremento");

var connection = new ConnectionManager();
connection.Connecting();
connection.TransactionSucceeded();
connection.TransactionFailed();
Check(connection.State == ConnectionState.Online, "Primeira falha mantém ONLINE");
connection.TransactionFailed();
Check(connection.State == ConnectionState.Online, "Segunda falha mantém ONLINE");
connection.TransactionFailed();
Check(connection.State == ConnectionState.Offline, "Terceira falha fica OFFLINE");
connection.TransactionSucceeded();
Check(connection.State == ConnectionState.Online, "Primeira resposta válida recupera ONLINE");

var listener = new TcpListener(IPAddress.Loopback, 0);
listener.Start();
var endpoint = (IPEndPoint)listener.LocalEndpoint;
var server = Task.Run(async () =>
{
    using var accepted = await listener.AcceptTcpClientAsync();
    await using var stream = accepted.GetStream();
    var request = new byte[16];
    _ = await stream.ReadAsync(request);
    await stream.WriteAsync(Encoding.ASCII.GetBytes(diagnostic));
    accepted.Client.LingerState = new LingerOption(true, 0);
});
var measuredResponse = await new Cam4TcpClient().ExecuteMeasuredAsync("127.0.0.1", endpoint.Port, "?D", true, CancellationToken.None);
await server;
listener.Stop();
Check(diagnosticParser.TryParse(measuredResponse.Response, out _), "Resposta ?D preservada quando peer fecha imediatamente");
Check(measuredResponse.BytesReceived == Encoding.ASCII.GetByteCount(diagnostic) &&
      measuredResponse.ConnectMs >= 0 && measuredResponse.WriteMs >= 0 && measuredResponse.ReadMs >= 0,
    "Poll válido registra bytes e tempos de CONNECT/WRITE/READ");

var resetListener = new TcpListener(IPAddress.Loopback, 0);
resetListener.Start();
var resetEndpoint = (IPEndPoint)resetListener.LocalEndpoint;
var acceptedAttempts = 0;
var resetServer = Task.Run(async () =>
{
    using var accepted = await resetListener.AcceptTcpClientAsync();
    Interlocked.Increment(ref acceptedAttempts);
    accepted.Client.LingerState = new LingerOption(true, 0);
});
var resetClient = new Cam4TcpClient { Timeout = TimeSpan.FromMilliseconds(300) };
var resetCommand = new Cam4CommandService(resetClient) { Host = "127.0.0.1", Port = resetEndpoint.Port };
var singleFailure = await resetCommand.ExecuteAsync("?D");
await resetServer;
await Task.Delay(100);
resetListener.Stop();
Check(!singleFailure.Success && acceptedAttempts == 1,
    "Uma falha continua sendo uma tentativa sem retry oculto");

var configurationText = "? Configuracao geral ?\n? Ver : 12 ?\n? IP Server : 192.0.2.10 ?\n? SN : 255.255.255.0 ?\n? GW : 192.0.2.1 ?\n? DNS : 0.0.0.0 ?\n" +
    "? Status DHCP : OFF ?\n? Numero renders : 2 ?\n? IP R1 : 192.0.2.20 ?\n? Porta R1 : 1111 ?\n? IP R2 : 192.0.2.21 ?\n? Porta R2 : 1112 ?\n" +
    "? IP R3 : 192.0.2.22 ?\n? Porta R3 : 1111 ?\n? IP R4 : 192.0.2.23 ?\n? Porta R4 : 1111 ?\n? IP R5 : 192.0.2.24 ?\n? Porta R5 : 1111 ?\n" +
    "? IP R6 : 192.0.2.25 ?\n? Porta R6 : 1111 ?\n? IP R7 : 192.0.2.26 ?\n? Porta R7 : 1111 ?\n? Ordem de envio dos eixos: YXZ ?\n? Ordem de envio dos angulos: PTR ?\n" +
    "? X: 0, Y: 1, Z: 0, PAN: 1, TILT: 0, ROLL: 0, FOCO: 0, ZOOM: 1 ?\n? OffsetX : 1.500000, OffsetY : -2.000000, OffsetZ : 0.000000, OffsetPan : 0.250000, OffsetTilt : 0.000000, OffsetRoll : -0.500000 ?\n";
var configurationParser = new ConfigurationParser();
Check(configurationParser.TryParse(configurationText, out var configuration), "Parser ?? aceita resposta do firmware");
Check(configuration.Ip == "192.0.2.10" && configuration.DestinationCount == 2, "Rede e número de destinos");
Check(configuration.Destinations[1].Port == 1112, "Destino Free-D por número");
Check(configuration.AxisOrder == "YXZ" && configuration.AngleOrder == "PTR", "Ordens de tracking");
Check(configuration.Inversions.Y && configuration.Inversions.Pan && configuration.Inversions.Zoom, "Inversões");
Check(configuration.Offsets.X == 1.5 && configuration.Offsets.Roll == -0.5, "Offsets");

if (failures.Count > 0) { Console.Error.WriteLine($"FAILURES ({failures.Count}): {string.Join(", ", failures)}"); return 1; }
Console.WriteLine("All parser tests passed.");
return 0;
