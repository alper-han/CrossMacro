
namespace CrossMacro.Cli.Serialization;

public sealed record class TaskListData<T>(
    [property: JsonPropertyName("count")] int Count,
    [property: JsonPropertyName("tasks")] IReadOnlyList<T> Tasks
);
