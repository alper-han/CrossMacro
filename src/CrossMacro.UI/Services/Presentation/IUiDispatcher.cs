namespace CrossMacro.UI.Services.Presentation;

/// <summary>Owns dispatch and completion semantics for presentation work.</summary>
public interface IUiDispatcher
{
    public bool CheckAccess();
    public void Post(Action action);
    public Task InvokeAsync(Action action);
    public Task InvokeAsync(Func<Task> action);
    public Task<T> InvokeAsync<T>(Func<T> callback);
}
