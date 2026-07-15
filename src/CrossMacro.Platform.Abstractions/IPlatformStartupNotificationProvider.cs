namespace CrossMacro.Platform.Abstractions;

public interface IPlatformStartupNotificationProvider
{
    PlatformStartupNotification? GetStartupNotification();
}
