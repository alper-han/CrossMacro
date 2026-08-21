namespace CrossMacro.Mcp.Contracts;

/// <summary>
/// The finite automation workloads coordinated by the local MCP server.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<McpAutomationOperationKind>))]
public enum McpAutomationOperationKind
{
    [JsonStringEnumMemberName("play")]
    Play = 0,

    [JsonStringEnumMemberName("run")]
    Run = 1,

    [JsonStringEnumMemberName("record")]
    Record = 2,
}
