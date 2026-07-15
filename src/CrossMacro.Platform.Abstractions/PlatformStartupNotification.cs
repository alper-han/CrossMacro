namespace CrossMacro.Platform.Abstractions;

public sealed record class PlatformStartupNotification(
    string Title,
    string Message,
    PlatformStartupNotificationSeverity Severity);
