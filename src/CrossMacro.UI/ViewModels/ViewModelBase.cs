namespace CrossMacro.UI.ViewModels;

public abstract class ViewModelBase : ObservableObject
{
    /// <summary>
    /// Posts <paramref name="action"/> to the UI thread (fire-and-forget). Runs inline when
    /// already on the UI thread or in headless tests (no Application).
    /// </summary>
    protected static void PostToUiThread(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (Avalonia.Application.Current is null || Dispatcher.UIThread.CheckAccess())
        {
            action();
            return;
        }

        Dispatcher.UIThread.Post(action);
    }

    /// <summary>
    /// Runs <paramref name="action"/> on the UI thread and awaits completion. Runs inline when
    /// already on the UI thread or in headless tests (no Application).
    /// </summary>
    protected static async Task RunOnUiThreadAsync(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (Avalonia.Application.Current is null || Dispatcher.UIThread.CheckAccess())
        {
            action();
            return;
        }

        await Dispatcher.UIThread.InvokeAsync(action);
    }
}
