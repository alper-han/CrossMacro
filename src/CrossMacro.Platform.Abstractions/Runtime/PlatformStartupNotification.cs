namespace CrossMacro.Platform.Abstractions.Runtime;

public sealed record PlatformStartupNotification(
    string Title,
    string Message,
    PlatformStartupNotificationSeverity Severity);
