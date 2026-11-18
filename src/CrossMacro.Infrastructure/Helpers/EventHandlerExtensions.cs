
namespace CrossMacro.Infrastructure.Helpers;

internal static class EventHandlerExtensions
{
    /// <summary>
    /// Raises <paramref name="handler"/> without letting a subscriber's exception escape into the
    /// service that raised it; the failure is logged instead.
    /// </summary>
    public static void RaiseSafely<TEventArgs>(
        this EventHandler<TEventArgs>? handler,
        object sender,
        TEventArgs args,
        string source)
    {
        try
        {
            handler?.Invoke(sender, args);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.Warning(ex, "[{Source}] An event subscriber threw", source);
        }
    }
}
