namespace CrossMacro.Platform.Abstractions;

public sealed record PlatformStartupNotification(
    string Title,
    string Message,
    PlatformStartupNotificationSeverity Severity);
