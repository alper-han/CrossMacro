namespace CrossMacro.Mcp.Contracts;

/// <summary>
/// The user-facing message for each failure class an image tool can report. A null message means
/// that failure class is not handled by the tool and the exception propagates.
/// </summary>
internal sealed record McpImageFailureMessages(
    string Validation,
    string? File = null,
    string? Environment = null,
    string? Runtime = null);
