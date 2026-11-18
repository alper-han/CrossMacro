namespace CrossMacro.Mcp.Contracts;

/// <summary>Maps the expected PNG decode, file and clipboard exceptions to MCP outcomes.</summary>
internal static class McpImageFailureMapper
{
    public static bool TryMap(Exception exception, McpImageFailureMessages messages, out McpToolOutcome outcome)
    {
        var mapped = exception switch
        {
            ImageClipboardUnavailableException when messages.Environment is not null =>
                McpToolOutcomeMapper.EnvironmentError(messages.Environment),
            InvalidDataException or NotSupportedException or ArgumentException =>
                McpToolOutcomeMapper.ValidationError(messages.Validation),
            IOException or UnauthorizedAccessException when messages.File is not null =>
                McpToolOutcomeMapper.FileError(messages.File),
            InvalidOperationException when messages.Runtime is not null =>
                McpToolOutcomeMapper.RuntimeError(messages.Runtime),
            _ => null,
        };

        outcome = mapped!;
        return mapped is not null;
    }
}
