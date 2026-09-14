namespace CrossMacro.UI.Tests;

internal sealed class SwitchingUiDispatcher : IUiDispatcher
{
    public IUiDispatcher Target { get; set; } = ImmediateUiDispatcher.Instance;
    public bool CheckAccess() => Target.CheckAccess();
    public void Post(Action action) => Target.Post(action);
    public Task InvokeAsync(Action action) => Target.InvokeAsync(action);
    public Task InvokeAsync(Func<Task> action) => Target.InvokeAsync(action);
    public Task<T> InvokeAsync<T>(Func<T> callback) => Target.InvokeAsync(callback);
}
