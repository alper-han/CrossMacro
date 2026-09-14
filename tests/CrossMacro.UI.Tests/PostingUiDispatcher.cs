namespace CrossMacro.UI.Tests;

internal sealed class PostingUiDispatcher(Action<Action> post) : IUiDispatcher
{
    public bool CheckAccess() => true;
    public void Post(Action action) => post(action);
    public Task InvokeAsync(Action action) { action(); return Task.CompletedTask; }
    public Task InvokeAsync(Func<Task> action) => action();
    public Task<T> InvokeAsync<T>(Func<T> callback) => Task.FromResult(callback());
}
