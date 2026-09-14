namespace CrossMacro.Mcp.Contracts.ScreenReading;

/// <summary>
/// Separates platform-declared PNG clipboard read and write capability.
/// </summary>
public sealed record McpImageClipboardCapability(bool ReadSupported, bool WriteSupported);
