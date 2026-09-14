
namespace CrossMacro.Application.Automation;

/// <summary>
/// Represents a stable application-level view of a task collection.
/// </summary>
/// <remarks>
/// The collection membership and ordering are copied at construction time and
/// exposed through a read-only wrapper. The task management workflows supply detached task snapshots, so editing a
/// result cannot change the runtime before a successful commit.
/// </remarks>
public sealed record TaskCollectionResult<T>
{
    public TaskCollectionResult(IReadOnlyList<T> tasks, long scopeGeneration = 0)
    {
        Tasks = tasks;
        ScopeGeneration = scopeGeneration;
    }

    public long ScopeGeneration { get; }

    public IReadOnlyList<T> Tasks
    {
        get;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            field = Array.AsReadOnly(value.ToArray());
        }
    }

    public void Deconstruct(out IReadOnlyList<T> tasks) => tasks = Tasks;
}
