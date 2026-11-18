namespace CrossMacro.Infrastructure.Services.Automation;

/// <summary>How one kind of automation task is copied, persisted and kept consistent with runtime state.</summary>
internal sealed class AutomationTaskStoreOptions<TTask>
    where TTask : class
{
    /// <summary>Names the task kind in log and error messages.</summary>
    public required string Name { get; init; }
    public required Func<TTask, Guid> GetId { get; init; }
    public required Func<TTask, TTask> Copy { get; init; }
    public required Func<TTask, TTask, bool> HasSameConfiguration { get; init; }
    public required Action<TTask, TTask> ApplyConfiguration { get; init; }
    public required Func<CancellationToken, Task<IReadOnlyList<TTask>?>> Load { get; init; }
    public required Func<IReadOnlyList<TTask>, CancellationToken, Task> Save { get; init; }

    /// <summary>Finalizes a committed snapshot under the store lock; the second argument is the live task, if any.</summary>
    public Action<TTask, TTask?>? PrepareCommit { get; init; }

    /// <summary>Finalizes a loaded task under the store lock before it becomes live.</summary>
    public Action<TTask>? PrepareLoaded { get; init; }

    /// <summary>Runs under the store lock before every live task is dropped to be replaced by a load.</summary>
    public Action? Replacing { get; init; }

    /// <summary>Runs under the store lock for a live task that a commit removes.</summary>
    public Action<TTask>? Removed { get; init; }

    /// <summary>Runs under the store lock for a live task whose configuration a commit changes.</summary>
    public Action<TTask>? Reconfiguring { get; init; }
}
