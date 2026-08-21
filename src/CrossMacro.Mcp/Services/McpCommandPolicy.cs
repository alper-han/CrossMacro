using System.Collections.Frozen;
using CrossMacro.Mcp.Contracts;

namespace CrossMacro.Mcp.Services;

/// <summary>
/// Blocks recursive MCP hosting, GUI lifecycle, setup, and privilege paths before
/// the normal CLI parser can see them.
/// </summary>
public sealed class McpCommandPolicy : IMcpCommandPolicy
{
    private static readonly FrozenSet<string> AllowedCommands =
        McpCliCommandCatalog.SupportedCommands.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    private static readonly FrozenSet<string> BlockedCommands = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "mcp",
        "headless",
        "setup",
        "quick-setup",
        "gui",
        "--headless",
        "sudo",
        "pkexec",
        "run0",
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    private static readonly FrozenSet<string> BlockedOptions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "--start-minimized",
        "--drm",
        "--fbdev",
        "--tty",
        "--display",
        "--x11",
        "--wayland",
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    public McpToolOutcome Validate(string command, IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(arguments);

        var normalizedCommand = command.Trim();
        if (string.IsNullOrWhiteSpace(normalizedCommand))
        {
            return McpToolOutcomeMapper.InvalidArguments("Command token is required.");
        }

        if (BlockedCommands.Contains(normalizedCommand))
        {
            return McpToolOutcomeMapper.InvalidArguments("This command is not available through command.execute.");
        }

        if (!AllowedCommands.Contains(normalizedCommand))
        {
            return McpToolOutcomeMapper.InvalidArguments("This command is not available through command.execute.");
        }

        if (normalizedCommand.StartsWith('-')
            || normalizedCommand.Contains(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
            || normalizedCommand.Contains(Path.AltDirectorySeparatorChar.ToString(), StringComparison.Ordinal)
            || normalizedCommand.Any(char.IsWhiteSpace))
        {
            return McpToolOutcomeMapper.InvalidArguments("Command must be a single CrossMacro command token.");
        }

        if (arguments.Count > 128)
        {
            return McpToolOutcomeMapper.InvalidArguments("Command argument count exceeds the maximum allowed value.");
        }

        var totalCharacters = normalizedCommand.Length;
        foreach (var argument in arguments)
        {
            if (argument is null || argument.Length > 16_384)
            {
                return McpToolOutcomeMapper.InvalidArguments("Command arguments must be non-null and at most 16384 characters.");
            }

            totalCharacters = checked(totalCharacters + argument.Length);
            if (totalCharacters > 262_144)
            {
                return McpToolOutcomeMapper.InvalidArguments("Command payload exceeds the maximum allowed size.");
            }

            if (argument.StartsWith('-') && BlockedOptions.Contains(argument))
            {
                return McpToolOutcomeMapper.InvalidArguments("This command option is not available through command.execute.");
            }
        }

        return McpToolOutcomeMapper.Success(string.Empty);
    }
}
