using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace CrossMacro.Cli.Serialization;

public sealed record TaskListData<T>(
    [property: JsonPropertyName("count")] int Count,
    [property: JsonPropertyName("tasks")] IReadOnlyList<T> Tasks
);
