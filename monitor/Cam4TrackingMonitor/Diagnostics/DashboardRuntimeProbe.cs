using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Cam4TrackingMonitor.Collections;
using Cam4TrackingMonitor.Controls;
using Cam4TrackingMonitor.Models;
using Cam4TrackingMonitor.Services;
using Cam4TrackingMonitor.ViewModels;

namespace Cam4TrackingMonitor.Diagnostics;

internal static class DashboardRuntimeProbe
{
    public static async Task RunAsync(Window window, TabControl tabs, MainViewModel main)
    {
        if (Environment.GetEnvironmentVariable("CAM4_DASHBOARD_SIMULATION") == "1")
            await RunSimulationAsync(window, main.Dashboard);
        if (Environment.GetEnvironmentVariable("CAM4_DASHBOARD_SOAK") == "1")
            await RunSoakAsync(window, tabs, main);
    }

    private static async Task RunSimulationAsync(Window window, DashboardViewModel dashboard)
    {
        var output = new StringBuilder();
        TestRateCalculator(output);
        TestCircularBuffer(output);

        var start = DateTime.Now.AddMinutes(-5);
        for (var index = 0; index < 305; index++)
        {
            var sessionIndex = index < 220 ? index : index - 220;
            var uart = (uint)(sessionIndex >= 45 ? 1 : 0);
            var rxBad = (uint)(sessionIndex >= 90 ? 1 : 0);
            var syncLost = (uint)(sessionIndex >= 120 ? 1 : 0);
            var freeDError = (uint)(sessionIndex >= 145 ? 1 : 0);
            var freeDDrop = (uint)(sessionIndex >= 170 ? 1 : 0);
            var ethDown = (uint)(sessionIndex >= 195 ? 1 : 0);
            var telemetry = CreateTelemetry(start.AddSeconds(index), sessionIndex, uart, rxBad, syncLost, freeDError, freeDDrop, ethDown);
            dashboard.Update(telemetry, new TelemetryStates(Cam4TrackingMonitor.Models.VisualState.Ok, Cam4TrackingMonitor.Models.VisualState.Ok, Cam4TrackingMonitor.Models.VisualState.Ok, Cam4TrackingMonitor.Models.VisualState.Ok));
            if (index is 30 or 75) dashboard.RecordPollFailure(telemetry.Timestamp.AddMilliseconds(400));
        }
        Check(output, dashboard.HistoryCount == TelemetryHistoryService.MaximumSamples, "Dashboard history limited to 300");
        Check(output, dashboard.History.Any(sample => sample.Events.HasFlag(TelemetryEvent.BoxRestart)), "BOX RESTART marker generated");
        Check(output, dashboard.History.Any(sample => sample.Events.HasFlag(TelemetryEvent.UartError)), "UART marker generated");
        Check(output, dashboard.History.Any(sample => sample.Events.HasFlag(TelemetryEvent.RxBad)), "RX BAD marker generated");
        Check(output, dashboard.History.Any(sample => sample.Events.HasFlag(TelemetryEvent.SyncLost)), "SYNC LOST marker generated");
        Check(output, dashboard.History.Any(sample => sample.Events.HasFlag(TelemetryEvent.FreeDError)), "FREE-D ERR marker generated");
        Check(output, dashboard.History.Any(sample => sample.Events.HasFlag(TelemetryEvent.FreeDDrop)), "FREE-D DROP marker generated");
        Check(output, dashboard.History.Any(sample => sample.Events.HasFlag(TelemetryEvent.EthernetDown)), "ETH DOWN marker generated");

        window.WindowState = WindowState.Normal;
        window.Width = 1920;
        window.Height = 1080;
        window.Left = 0;
        window.Top = 0;
        await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
        var directory = Path.Combine(AppContext.BaseDirectory, "DashboardDiagnostics");
        Directory.CreateDirectory(directory);
        SaveScreenshot(window, Path.Combine(directory, "CAM4_Dashboard_1920x1080.png"), 1920, 1080);
        await File.WriteAllTextAsync(Path.Combine(directory, "simulation-tests.log"), output.ToString());
    }

    private static async Task RunSoakAsync(Window window, TabControl tabs, MainViewModel main)
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "DashboardDiagnostics");
        Directory.CreateDirectory(directory);
        var output = new StringBuilder();
        using var process = Process.GetCurrentProcess();
        var previousCpu = process.TotalProcessorTime;
        var previousTime = DateTime.UtcNow;
        for (var minute = 0; minute <= 20; minute++)
        {
            if (minute == 10) tabs.SelectedIndex = 1;
            process.Refresh();
            var now = DateTime.UtcNow;
            var cpuNow = process.TotalProcessorTime;
            var elapsed = Math.Max(0.001, (now - previousTime).TotalSeconds);
            var cpu = (cpuNow - previousCpu).TotalSeconds / elapsed / Environment.ProcessorCount * 100;
            var polling = main.PollingDiagnostics;
            output.AppendLine($"minute={minute}; tab={(tabs.SelectedIndex == 0 ? "DASHBOARD" : "CONFIGURATION")}; cpu_pct={cpu:F2}; working_mb={process.WorkingSet64 / 1048576d:F1}; private_mb={process.PrivateMemorySize64 / 1048576d:F1}; handles={process.HandleCount}; threads={process.Threads.Count}; history={main.Dashboard.HistoryCount}; chart_renders={TelemetryChart.RenderCount}; polls={polling.PollCount}; in_progress={polling.InProgress}; max_concurrent={polling.MaxConcurrent}");
            previousCpu = cpuNow;
            previousTime = now;
            if (minute < 20) await Task.Delay(TimeSpan.FromMinutes(1));
        }
        await File.WriteAllTextAsync(Path.Combine(directory, "soak-20-minutes.log"), output.ToString());
    }

    private static void TestRateCalculator(StringBuilder output)
    {
        var calculator = new CounterRateCalculator();
        var start = DateTime.Now;
        var first = calculator.Add(CreateTelemetry(start, 0, 0, 0, 0, 0, 0, 0));
        Check(output, first.SessionStart && first.RxTotalRate is null, "First sample has no rate");
        var active = calculator.Add(CreateTelemetry(start.AddSeconds(1.5), 1, 0, 0, 0, 0, 0, 0));
        Check(output, Math.Abs(active.RxTotalRate!.Value - 40) < 0.001, "Rate uses actual 1.5 second interval");
        var stopped = calculator.Add(CreateTelemetry(start.AddSeconds(2.5), 1, 0, 0, 0, 0, 0, 0));
        Check(output, stopped.RxTotalRate == 0, "Stopped counter produces zero rate");
        var events = calculator.Add(CreateTelemetry(start.AddSeconds(3.5), 2, 1, 1, 1, 1, 1, 1));
        Check(output, events.Events.HasFlag(TelemetryEvent.UartError | TelemetryEvent.RxBad | TelemetryEvent.SyncLost | TelemetryEvent.FreeDError | TelemetryEvent.FreeDDrop | TelemetryEvent.EthernetDown), "All increment events detected");
        var reset = calculator.Add(CreateTelemetry(start.AddSeconds(4.5), 0, 0, 0, 0, 0, 0, 0));
        Check(output, reset.SessionStart && reset.Events.HasFlag(TelemetryEvent.BoxRestart) && reset.RxTotalRate is null, "Reset starts new session without negative rate");
    }

    private static void TestCircularBuffer(StringBuilder output)
    {
        var buffer = new FixedSizeCircularBuffer<int>(300);
        for (var index = 0; index < 400; index++) buffer.Add(index);
        var snapshot = buffer.Snapshot();
        Check(output, buffer.Count == 300 && snapshot[0] == 100 && snapshot[^1] == 399, "Circular buffer remains bounded and ordered");
    }

    private static TrackingBoxTelemetry CreateTelemetry(DateTime timestamp, int index, uint uart, uint rxBad, uint syncLost, uint freeDError, uint freeDDrop, uint ethDown)
    {
        var count = (uint)Math.Max(0, index);
        return new TrackingBoxTelemetry
        {
            Timestamp = timestamp, Uptime = $"0d 00:{index / 60:00}:{index % 60:00}",
            RxTotal = count * 60, RxOk = count * 60 - Math.Min(count * 60, rxBad), RxBad = rxBad,
            UartError = uart, Ore = 0, Fe = 0, Ne = 0, Pe = 0, FrameBad = rxBad,
            LastRxEventMs = 3, LastRxOkMs = 3, SyncTotal = count * 60, SyncLost = syncLost, LastSyncMs = 3,
            FreeDTotal = count * 120, FreeDOk = count * 120 - Math.Min(count * 120, freeDError),
            FreeDError = freeDError, FreeDDrop = freeDDrop, LastFreeDMs = 3,
            EthernetLink = 1, EthernetDown = ethDown, EthernetUp = ethDown + 1
        };
    }

    private static void Check(StringBuilder output, bool condition, string description)
    {
        output.AppendLine($"{(condition ? "PASS" : "FAIL")} {description}");
        if (!condition) throw new InvalidOperationException($"Dashboard simulation failed: {description}");
    }

    private static void SaveScreenshot(Window window, string path, int width, int height)
    {
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(window);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path);
        encoder.Save(stream);
    }
}
