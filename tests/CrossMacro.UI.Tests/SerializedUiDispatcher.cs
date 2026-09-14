namespace CrossMacro.UI.Tests;

/// <summary>Executes immediately while preserving the UI contract that callbacks cannot overlap.</summary>
internal sealed class SerializedUiDispatcher : IUiDispatcher
{
    private readonly Lock _gate = new();

    public bool CheckAccess() => _gate.IsHeldByCurrentThread;
    public void Post(Action action) { lock (_gate) { action(); } }
    public Task InvokeAsync(Action action) { lock (_gate) { action(); return Task.CompletedTask; } }
    public Task InvokeAsync(Func<Task> action) { lock (_gate) { return action(); } }
    public Task<T> InvokeAsync<T>(Func<T> callback) { lock (_gate) { return Task.FromResult(callback()); } }
}
