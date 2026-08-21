namespace CrossMacro.Mcp.Contracts;

/// <summary>
/// The stable lifecycle state of a coordinated automation operation.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<McpAutomationOperationState>))]
public enum McpAutomationOperationState
{
    [JsonStringEnumMemberName("running")]
    Running = 0,

    [JsonStringEnumMemberName("succeeded")]
    Succeeded = 1,

    [JsonStringEnumMemberName("failed")]
    Failed = 2,

    [JsonStringEnumMemberName("cancelled")]
    Cancelled = 3,
}
