namespace CrossMacro.Mcp.Tests;

/// <summary>Holds one task and counts commits, leaving the interface-specific operations to derived fakes.</summary>
internal abstract class SingleTaskStore<TTask>(TTask task)
{
    public TTask Current { get; set; } = task;

    public int CommitCount { get; private set; }

    public IReadOnlyList<TTask> Tasks => [Current];

    public virtual Task LoadAsync() => Task.CompletedTask;

    public Task SaveAsync() => throw new NotSupportedException();

    public virtual Task CommitAsync(IReadOnlyList<TTask> tasks, CancellationToken cancellationToken = default)
    {
        CommitCount++;
        return Task.CompletedTask;
    }
}
