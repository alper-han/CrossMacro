namespace CrossMacro.UI.Tests;

internal sealed class ImmediateUiDispatcher : IUiDispatcher
{
    public static ImmediateUiDispatcher Instance { get; } = new();
    public bool CheckAccess() => true;
    public void Post(Action action) => action();
    public Task InvokeAsync(Action action) { action(); return Task.CompletedTask; }
    public Task InvokeAsync(Func<Task> action) => action();
    public Task<T> InvokeAsync<T>(Func<T> callback) => Task.FromResult(callback());
}
