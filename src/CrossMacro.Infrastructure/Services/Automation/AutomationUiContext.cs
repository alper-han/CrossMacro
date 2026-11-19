namespace CrossMacro.Infrastructure.Services.Automation;

/// <summary>
/// The synchronization context an automation service was created on. Task collections are bound to the UI, so
/// changes to them are marshalled back to that context; without one (CLI, MCP, tests) work runs inline.
/// </summary>
internal sealed class AutomationUiContext
{
    private SynchronizationContext? _context = SynchronizationContext.Current;

    /// <summary>Adopts the current context if none was captured at construction.</summary>
    public void EnsureCaptured()
    {
        if (_context is null && SynchronizationContext.Current is not null)
        {
            _context = SynchronizationContext.Current;
        }
    }

    /// <summary>Runs <paramref name="action"/> on the captured context without waiting for it.</summary>
    public void Post(Action action)
    {
        var context = _context;
        if (context is not null)
        {
            context.Post(_ => action(), state: null);
        }
        else
        {
            action();
        }
    }

    /// <summary>Runs <paramref name="action"/> on the captured context and completes when it has run.</summary>
    public Task InvokeAsync(Action action)
    {
        var context = _context;
        if (context is null || SynchronizationContext.Current == context)
        {
            action();
            return Task.CompletedTask;
        }

        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        context.Post(state =>
        {
            try
            {
                action();
                _ = completion.TrySetResult();
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                _ = completion.TrySetException(ex);
            }
        }, state: null);
        return completion.Task;
    }
}
