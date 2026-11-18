
namespace CrossMacro.Cli.Serialization;

public sealed record TaskListData<T>(
    int Count,
    IReadOnlyList<T> Tasks
);
