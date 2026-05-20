namespace WSLManager.Models;

public enum LogLevel
{
    Debug,
    Info,
    Success,
    Warning,
    Error
}

public sealed record LogEntry(DateTime Timestamp, LogLevel Level, string Message);
