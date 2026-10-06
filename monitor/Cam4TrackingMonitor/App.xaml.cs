using System.Configuration;
using System.Data;
using System.Windows;
using System.Windows.Threading;
using Cam4TrackingMonitor.Logging;
using Cam4TrackingMonitor.Models;

namespace Cam4TrackingMonitor;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private readonly EventLogger _logger = new();

    protected override void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        base.OnStartup(e);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        try
        {
            _logger.WriteAsync(new MonitorEvent(
                DateTime.Now,
                EventSeverity.Error,
                "APPLICATION",
                "UNHANDLED UI EXCEPTION",
                string.Empty,
                e.Exception.GetType().FullName ?? e.Exception.GetType().Name,
                "FAILED",
                e.Exception.ToString())).GetAwaiter().GetResult();
        }
        catch
        {
            // O registro de falha nunca deve substituir a exceção original.
        }

        e.Handled = false;
    }
}

