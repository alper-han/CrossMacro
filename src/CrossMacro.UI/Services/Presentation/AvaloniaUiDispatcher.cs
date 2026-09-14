namespace CrossMacro.UI.Services.Presentation;

public sealed class AvaloniaUiDispatcher : IUiDispatcher
{
    public static AvaloniaUiDispatcher Instance { get; } = new();

    public bool CheckAccess() => Dispatcher.UIThread.CheckAccess();

    public void Post(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (CheckAccess())
        {
            action();
            return;
        }
        Dispatcher.UIThread.Post(action);
    }

    public async Task InvokeAsync(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (CheckAccess())
        {
            action();
            return;
        }
        await Dispatcher.UIThread.InvokeAsync(action);
    }

    public async Task InvokeAsync(Func<Task> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (CheckAccess())
        {
            await action().ConfigureAwait(true);
            return;
        }
        await Dispatcher.UIThread.InvokeAsync(action).ConfigureAwait(false);
    }

    public async Task<T> InvokeAsync<T>(Func<T> callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        if (CheckAccess())
        {
            return callback();
        }
        return await Dispatcher.UIThread.InvokeAsync(callback);
    }
}
