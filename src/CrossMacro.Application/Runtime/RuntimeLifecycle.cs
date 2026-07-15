
namespace CrossMacro.Application.Runtime;

public sealed class RuntimeLifecycle : IRuntimeLifecycle
{
    private readonly IReadOnlyList<RuntimeLifecycleStep> _steps;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly List<RuntimeLifecycleStep> _startedSteps = [];
    private bool _started;
    private bool _stopped;

    public RuntimeLifecycle(IReadOnlyList<RuntimeLifecycleStep> steps)
    {
        _steps = steps ?? throw new ArgumentNullException(nameof(steps));
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_started)
            {
                return;
            }

            try
            {
                foreach (var step in _steps)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await step.StartAsync(cancellationToken).ConfigureAwait(false);
                    _startedSteps.Add(step);
                }
                _started = true;
                _stopped = false;
            }
            catch (Exception startError)
            {
                var cleanupErrors = await StopStartedStepsAsync(CancellationToken.None).ConfigureAwait(false);
                _started = false;
                _stopped = true;
                ThrowWithCleanupErrors(startError, cleanupErrors);
            }
        }
        finally { _gate.Release(); }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_stopped)
            {
                return;
            }

            var cleanupErrors = await StopStartedStepsAsync(cancellationToken).ConfigureAwait(false);
            _started = false;
            _stopped = true;
            if (cleanupErrors.Count > 0)
            {
                throw new AggregateException("Runtime shutdown failed.", cleanupErrors);
            }
        }
        finally { _gate.Release(); }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync(CancellationToken.None).ConfigureAwait(false);
        _gate.Dispose();
    }

    private async Task<List<Exception>> StopStartedStepsAsync(CancellationToken cancellationToken)
    {
        var errors = new List<Exception>();
        for (var index = _startedSteps.Count - 1; index >= 0; index--)
        {
            try { await _startedSteps[index].StopAsync(cancellationToken).ConfigureAwait(false); }
            catch (Exception ex) { errors.Add(new InvalidOperationException($"Runtime step '{_startedSteps[index].Name}' failed to stop.", ex)); }
        }
        _startedSteps.Clear();
        return errors;
    }

    private static void ThrowWithCleanupErrors(Exception startError, IReadOnlyList<Exception> cleanupErrors)
    {
        if (cleanupErrors.Count is 0)
        {
            throw startError;
        }

        var errors = new List<Exception> { startError };
        errors.AddRange(cleanupErrors);
        throw new AggregateException("Runtime startup and rollback failed.", errors);
    }
}
