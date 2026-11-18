
namespace CrossMacro.UI.ViewModels;

internal static class PlatformStartupNotificationSelector
{
    /// <summary>The first notification a provider offers; a failing provider is skipped so it cannot block the others.</summary>
    internal static bool TryGet(
        IEnumerable<IPlatformStartupNotificationProvider> providers,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out PlatformStartupNotification? notification)
    {
        notification = null;
        foreach (var provider in providers)
        {
            try
            {
                notification = provider.GetStartupNotification();
                if (notification != null)
                {
                    return true;
                }
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                Log.Warning(ex, "[MainWindowViewModel] Platform startup notification provider failed");
            }
        }

        return false;
    }
}
