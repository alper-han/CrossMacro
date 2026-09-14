namespace CrossMacro.UI.ViewModels.Design;

internal sealed class DesignUiDispatcher : IUiDispatcher
{
    internal static DesignUiDispatcher Instance { get; } = new();
    public bool CheckAccess() => true;
    public void Post(Action action) => action();
    public Task InvokeAsync(Action action) { action(); return Task.CompletedTask; }
    public Task InvokeAsync(Func<Task> action) => action();
    public Task<T> InvokeAsync<T>(Func<T> callback) => Task.FromResult(callback());
}
