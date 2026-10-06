using Cam4TrackingMonitor.Models;

namespace Cam4TrackingMonitor.Services;

public sealed class ConnectionManager
{
    private int _consecutiveFailures;
    private ConnectionState _state = ConnectionState.Offline;
    public ConnectionState State { get => _state; private set { if (_state == value) return; _state = value; StateChanged?.Invoke(this, value); } }
    public event EventHandler<ConnectionState>? StateChanged;
    public void Connecting() => State = ConnectionState.Connecting;
    public void TransactionSucceeded()
    {
        _consecutiveFailures = 0;
        State = ConnectionState.Online;
    }
    public void TransactionFailed()
    {
        _consecutiveFailures++;
        if (_consecutiveFailures >= 3) State = ConnectionState.Offline;
    }
    public void Connected() => TransactionSucceeded();
    public void Disconnected() { _consecutiveFailures = 3; State = ConnectionState.Offline; }
    public void Rebooting() => State = ConnectionState.Rebooting;
}
