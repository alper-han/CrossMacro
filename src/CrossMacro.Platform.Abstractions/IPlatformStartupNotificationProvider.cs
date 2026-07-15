namespace CrossMacro.Platform.Abstractions;

public interface IPlatformStartupNotificationProvider
{
    public PlatformStartupNotification? GetStartupNotification();
}
