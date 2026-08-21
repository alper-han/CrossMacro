namespace CrossMacro.Mcp.Contracts;

public sealed record McpStatusResult(
    string Runtime,
    string ProductVersion,
    string OperatingSystem,
    string? SessionType,
    bool IsFlatpak,
    McpActiveProfile ActiveProfile,
    McpCapabilitySummary Capabilities,
    McpImageClipboardCapability ImageClipboard,
    McpAutomationOperation? ActiveOperation,
    string Policy,
    bool IsRestricted,
    IReadOnlyList<string> EnabledCapabilities,
    IReadOnlyList<string> AvailableTools);
