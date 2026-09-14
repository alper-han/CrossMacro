namespace CrossMacro.UI.Views.Tabs;

/// <summary>
/// Observes tab-close work that is initiated from an Avalonia routed event.
/// Routed-event handlers cannot return a task, so this boundary keeps an
/// unexpected close failure from escaping through an <c>async void</c> handler.
/// </summary>
internal static class EditorTabCloseActionRunner
{
    internal static async Task ExecuteAsync(
        Func<Task> closeAsync,
        Action<Exception> reportFailure)
    {
        ArgumentNullException.ThrowIfNull(closeAsync);
        ArgumentNullException.ThrowIfNull(reportFailure);

        try
        {
            await closeAsync().ConfigureAwait(true);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            reportFailure(exception);
        }
    }
}
