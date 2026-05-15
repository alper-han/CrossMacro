namespace CrossMacro.Platform.Abstractions;

public interface IPlatformStartupNotificationProvider
{
    PlatformStartupNotification? GetStartupNotification();
}

public sealed record PlatformStartupNotification(
    string Title,
    string Message,
    PlatformStartupNotificationSeverity Severity);

public enum PlatformStartupNotificationSeverity
{
    Success = 0,
    Warning = 1,
    Error = 2
}
