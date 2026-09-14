namespace CrossMacro.UI.ViewModels;

public abstract class ViewModelBase(IUiDispatcher? uiDispatcher = null) : ObservableObject
{
    protected IUiDispatcher UiDispatcher { get; } = uiDispatcher ?? AvaloniaUiDispatcher.Instance;

    protected void PostToUiThread(Action action) => UiDispatcher.Post(action);

    protected Task RunOnUiThreadAsync(Action action) => UiDispatcher.InvokeAsync(action);

    protected Task<T> RunOnUiThreadWithResultAsync<T>(Func<T> function) => UiDispatcher.InvokeAsync(function);
}
