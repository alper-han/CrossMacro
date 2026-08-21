namespace CrossMacro.Mcp.Contracts;

/// <summary>
/// Separates platform-declared PNG clipboard read and write capability.
/// </summary>
public sealed record McpImageClipboardCapability(bool ReadSupported, bool WriteSupported);
