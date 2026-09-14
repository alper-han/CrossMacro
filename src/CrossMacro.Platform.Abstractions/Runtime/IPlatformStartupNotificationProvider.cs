namespace CrossMacro.Platform.Abstractions.Runtime;

public interface IPlatformStartupNotificationProvider
{
    public PlatformStartupNotification? GetStartupNotification();
}
