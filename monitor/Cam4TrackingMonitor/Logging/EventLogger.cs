using System.Diagnostics;
using System.IO;
using System.Text;
using Cam4TrackingMonitor.Models;

namespace Cam4TrackingMonitor.Logging;

public sealed class EventLogger
{
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    public string LogDirectory { get; } = Path.Combine(AppContext.BaseDirectory, "Logs");
    public event EventHandler<MonitorEvent>? EventWritten;

    public EventLogger() => Directory.CreateDirectory(LogDirectory);

    public async Task WriteAsync(MonitorEvent item)
    {
        var path = Path.Combine(LogDirectory, $"CAM4_{item.Timestamp:yyyy-MM-dd}.log");
        var line = $"{item.Timestamp:yyyy-MM-dd HH:mm:ss.fff}\t{item.Severity}\t{Clean(item.Category)}\t{Clean(item.Event)}\t{Clean(item.PreviousValue)}\t{Clean(item.NewValue)}\t{Clean(item.Result)}\t{Clean(item.Message)}{Environment.NewLine}";
        await _writeLock.WaitAsync().ConfigureAwait(false);
        try { await File.AppendAllTextAsync(path, line, Encoding.UTF8).ConfigureAwait(false); }
        finally { _writeLock.Release(); }
        EventWritten?.Invoke(this, item);
    }

    public async Task WriteDiagnosticAsync(string line)
    {
        var path = CurrentTechnicalLogPath;
        await _writeLock.WaitAsync().ConfigureAwait(false);
        try { await File.AppendAllTextAsync(path, $"{Clean(line)}{Environment.NewLine}", Encoding.UTF8).ConfigureAwait(false); }
        finally { _writeLock.Release(); }
    }

    public string CurrentLogPath => Path.Combine(LogDirectory, $"CAM4_{DateTime.Now:yyyy-MM-dd}.log");
    public string CurrentTechnicalLogPath => Path.Combine(LogDirectory, $"Cam4TrackingMonitor_{DateTime.Now:yyyyMMdd}.log");
    public void OpenFolder() => Process.Start(new ProcessStartInfo("explorer.exe", LogDirectory) { UseShellExecute = true });
    private static string Clean(string value) => (value ?? string.Empty).Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ');
}
