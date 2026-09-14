using System.Runtime.ExceptionServices;

namespace CrossMacro.Application.Runtime;

/// <summary>Serializes desktop startup, shutdown and temporary profile suspension.</summary>
public sealed class AutomationRuntimeSession(IReadOnlyList<AutomationRuntimeComponent> components) : IRuntimeLifecycle
{
    private readonly IReadOnlyList<AutomationRuntimeComponent> _components = components ?? throw new ArgumentNullException(nameof(components));
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly HashSet<AutomationRuntimeComponent> _owned = [];
    private readonly HashSet<AutomationRuntimeComponent> _failedStops = [];
    private bool _shutdown;
    private int _faulted;

    public bool IsFaulted => Volatile.Read(ref _faulted) is not 0;
    public void MarkFaulted() => Volatile.Write(ref _faulted, 1);

    private void EnsureAvailable()
    {
        if (IsFaulted)
        { throw new InvalidOperationException("The active profile could not be restored. Restart the application before starting automation."); }
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            EnsureAvailable();
            try
            {
                _shutdown = false;
                await StartComponentsAsync(_components, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception startError) when (startError is not OutOfMemoryException)
            {
                try { await StopComponentsAsync(_components.Where(_owned.Contains).ToArray(), CancellationToken.None).ConfigureAwait(false); }
                catch (Exception cleanupError) when (cleanupError is not OutOfMemoryException)
                { throw new AggregateException("Runtime startup and cleanup failed.", startError, cleanupError); }
                throw;
            }
        }
        finally { _ = _gate.Release(); }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _shutdown = true;
            await StopComponentsAsync(_components.Where(component => _owned.Contains(component) || component.IsRunning()).ToArray(), cancellationToken).ConfigureAwait(false);
        }
        finally { _ = _gate.Release(); }
    }

    /// <summary>Keeps the lifecycle gate held while profile state is replaced.</summary>
    public async Task RunSuspendedAsync(Func<Task> replaceProfile, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(replaceProfile);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            EnsureAvailable();
            if (_shutdown) { throw new InvalidOperationException("Runtime has stopped; profile replacement is unavailable."); }
            var running = _components.Where(component => component.IsRunning()).ToArray();
            Exception? operationError = null;
            try
            {
                await StopComponentsAsync(_components, cancellationToken).ConfigureAwait(false);
                await replaceProfile().ConfigureAwait(false);
            }
            catch (Exception error) when (error is not OutOfMemoryException) { operationError = error; }

            if (IsFaulted)
            {
                if (operationError is not null) { ExceptionDispatchInfo.Throw(operationError); }
                EnsureAvailable();
            }

            try
            {
                // A component that did not quiesce must never acquire a second lifetime.
                await StartComponentsAsync(running.Where(component => component.IsQuiescent?.Invoke() is not false), CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception restoreError) when (restoreError is not OutOfMemoryException)
            {
                if (operationError is not null) { throw new AggregateException("Profile replacement and runtime restoration failed.", operationError, restoreError); }
                throw;
            }
            if (operationError is not null) { ExceptionDispatchInfo.Throw(operationError); }
        }
        finally { _ = _gate.Release(); }
    }

    public ValueTask DisposeAsync() => new(StopAsync(CancellationToken.None));

    private async Task StartComponentsAsync(IEnumerable<AutomationRuntimeComponent> components, CancellationToken cancellationToken)
    {
        foreach (var component in components)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (component.IsEnabled?.Invoke() is false || component.IsRunning() || _owned.Contains(component) || _failedStops.Contains(component)) { continue; }
            // Track before invoking startup so partially initialized services are also cleaned up.
            _ = _owned.Add(component);
            await component.StartAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task StopComponentsAsync(IReadOnlyList<AutomationRuntimeComponent> components, CancellationToken cancellationToken)
    {
        var errors = new List<Exception>();
        for (var index = components.Count - 1; index >= 0; index--)
        {
            var component = components[index];
            try
            {
                await component.StopAsync(cancellationToken).ConfigureAwait(false);
                if (component.IsQuiescent?.Invoke() is false)
                {
                    throw new InvalidOperationException($"The {component.Name} did not quiesce.");
                }
                _ = _owned.Remove(component);
                _ = _failedStops.Remove(component);
            }
            catch (Exception error) when (error is not OutOfMemoryException)
            {
                _ = _failedStops.Add(component);
                errors.Add(new InvalidOperationException($"Failed to stop {component.Name}: {error.Message}", error));
            }
        }
        if (errors.Count is 1) { throw errors[0]; }
        if (errors.Count > 0) { throw new AggregateException("Runtime services did not quiesce.", errors); }
    }
}
