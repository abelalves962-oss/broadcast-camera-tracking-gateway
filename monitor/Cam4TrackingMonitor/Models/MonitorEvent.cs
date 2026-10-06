namespace Cam4TrackingMonitor.Models;

public enum EventSeverity { Info, Warning, Error }
public enum VisualState { Ok, Warning, Error, Offline, Unknown, Waiting, Inactive }
public enum ConnectionState { Offline, Connecting, Online, Rebooting }
public enum CommunicationPhase { Connect, Write, Read, Parse, Validation }
public enum PollFailureKind { ConnectionReset, Timeout, Other }

public sealed record MonitorEvent(
    DateTime Timestamp,
    EventSeverity Severity,
    string Category,
    string Event,
    string PreviousValue,
    string NewValue,
    string Result,
    string Message);

public sealed record CommandResult(bool Success, string Response, string? Error = null,
    CommunicationPhase? FailurePhase = null, string? ExceptionType = null, string? SocketError = null,
    int BytesReceived = 0, string ConnectResult = "N/A", string WriteResult = "N/A", string ReadResult = "N/A",
    double ConnectMs = 0, double WriteMs = 0, double ReadMs = 0, bool IsTimeout = false);
