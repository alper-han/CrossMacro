namespace CrossMacro.Application.Automation;

/// <summary>Flows host authorization to the task selected under the mutation gate, without extending the gate over execution.</summary>
public sealed class AutomationTaskAuthorization
{
    private readonly AsyncLocal<Action<string?>?> _authorizeMacro = new();

    public async Task<T> RunAsync<T>(Action<string?> authorizeMacro, Func<Task<T>> operation)
    {
        ArgumentNullException.ThrowIfNull(authorizeMacro);
        ArgumentNullException.ThrowIfNull(operation);
        var previous = _authorizeMacro.Value;
        _authorizeMacro.Value = path =>
        {
            previous?.Invoke(path);
            authorizeMacro(path);
        };
        try
        {
            return await operation().ConfigureAwait(false);
        }
        finally
        {
            _authorizeMacro.Value = previous;
        }
    }

    internal void AuthorizeMacroPath(string? macroPath) => _authorizeMacro.Value?.Invoke(macroPath);
}
