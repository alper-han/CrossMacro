using CrossMacro.Cli;

namespace CrossMacro.Mcp.Contracts;

/// <summary>
/// Canonical MCP policy metadata for every CLI option in the public CLI contract.
/// </summary>
public static class McpCommandCapabilityMetadataCatalog
{
    private static readonly TimeSpan DefaultDuration = TimeSpan.FromMinutes(1);

    public static IReadOnlyList<McpCommandCapabilityMetadata> All { get; } =
        Array.AsReadOnly(
            CliCommandContractCatalog.RootCommands
                .SelectMany(command => command.Options.Select(option => Create(command.CommandToken, option)))
                .ToArray());

    public static McpCommandCapabilityMetadata Get(string commandToken, string optionToken) =>
        All.First(metadata => string.Equals(metadata.CommandToken, commandToken, StringComparison.OrdinalIgnoreCase)
            && string.Equals(metadata.OptionToken, optionToken, StringComparison.OrdinalIgnoreCase));

    private static McpCommandCapabilityMetadata Create(string commandToken, CliOptionContract option)
    {
        var isPath = option.ValueKind is CliOptionValueKind.Path;
        var isReadOnly = commandToken is "macro" or "doctor" or "screen" or "window" && option.Token is not "--active";
        var capability = commandToken switch
        {
            "macro" => McpCapability.MacroRead,
            "play" or "record" or "run" or "move" or "click" or "down" or "up" or "scroll" or "key" or "tap" or "type" or "delay" => McpCapability.InputAutomation,
            "clipboard" => option.Token is "--file" ? McpCapability.FileRead : McpCapability.ClipboardWrite,
            "window" => McpCapability.WindowControl,
            "screen" or "screenshot" => McpCapability.ScreenRead,
            _ => McpCapability.CommandExecute,
        };

        McpPathKind? pathKind = commandToken switch
        {
            "macro" or "play" when isPath => McpPathKind.MacroRead,
            "record" when isPath => McpPathKind.MacroWrite,
            "run" when isPath => McpPathKind.FileRead,
            "screenshot" when isPath => McpPathKind.ImageWrite,
            "schedule" or "shortcut" or "trigger" when isPath => McpPathKind.MacroRead,
            "clipboard" when isPath => McpPathKind.FileRead,
            _ => null,
        };

        return new McpCommandCapabilityMetadata(
            commandToken,
            option.Token,
            capability,
            isReadOnly ? McpToolAccess.ReadOnly : McpToolAccess.Effectful,
            pathKind,
            !isReadOnly,
            CliRuntimeProfile.OneShot,
            commandToken is "clipboard" or "window" or "screen" or "screenshot" or "move" or "click" or "down" or "up" or "scroll" or "key" or "tap" or "type"
                ? McpCommandPlatform.PlatformDependent
                : McpCommandPlatform.Any,
            DefaultDuration);
    }
}
