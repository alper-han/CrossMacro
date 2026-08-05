
namespace CrossMacro.Application.Automation;

/// <summary>
/// Represents a stable application-level view of a task collection.
/// </summary>
/// <remarks>
/// The collection membership and ordering are copied at construction time and
/// exposed through a read-only wrapper. Task instances themselves remain the
/// original domain objects so existing editor and binding semantics are not
/// changed.
/// </remarks>
public sealed record TaskCollectionResult<T>
{
    public TaskCollectionResult(IReadOnlyList<T> tasks)
    {
        Tasks = tasks;
    }

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
