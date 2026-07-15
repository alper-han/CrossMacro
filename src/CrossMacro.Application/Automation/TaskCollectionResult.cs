
namespace CrossMacro.Application.Automation;

public sealed record class TaskCollectionResult<T>(IReadOnlyList<T> Tasks);
