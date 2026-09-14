namespace CrossMacro.Application.Automation;

/// <summary>Serializes task mutations with replacement of the active profile's task stores.</summary>
public sealed class AutomationTaskMutationGate(AutomationTaskAuthorization? authorization = null) : IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly AutomationTaskAuthorization _authorization = authorization ?? new();

    internal void AuthorizeMacroPath(string? macroPath) => _authorization.AuthorizeMacroPath(macroPath);
    private long _scopeGeneration;
    private int _faulted;

    public long ScopeGeneration => Interlocked.Read(ref _scopeGeneration);
    public bool IsFaulted => Volatile.Read(ref _faulted) is not 0;

    /// <summary>Closes task access when profile replacement and rollback both fail. A new host session is required.</summary>
    public void MarkFaulted() => Interlocked.Exchange(ref _faulted, 1);

    internal void EnsureAvailable()
    {
        if (IsFaulted)
        {
            throw new TaskScopeUnavailableException();
        }
    }

    /// <summary>Invalidates existing task drafts before profile publication. Call only while holding this gate.</summary>
    public long AdvanceScopeGeneration() => Interlocked.Increment(ref _scopeGeneration);

    internal void EnsureCurrentScope(long? expectedScopeGeneration)
    {
        EnsureAvailable();
        if (expectedScopeGeneration is { } expected && expected != ScopeGeneration)
        {
            throw new TaskScopeConflictException();
        }
    }

    internal async Task<T> RunScopedAsync<T>(
        SemaphoreSlim operationGate,
        long? expectedScopeGeneration,
        Func<Task<T>> operation,
        CancellationToken cancellationToken)
    {
        await operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await RunAsync(async () =>
            {
                EnsureCurrentScope(expectedScopeGeneration);
                return await operation().ConfigureAwait(false);
            }, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _ = operationGate.Release();
        }
    }

    public async Task<T> RunAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await operation().ConfigureAwait(false);
        }
        finally
        {
            _ = _gate.Release();
        }
    }

    public async Task RunAsync(Func<Task> operation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await operation().ConfigureAwait(false);
        }
        finally
        {
            _ = _gate.Release();
        }
    }

    public void Dispose() => _gate.Dispose();
}
