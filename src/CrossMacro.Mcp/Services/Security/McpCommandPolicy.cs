using System.Collections.Frozen;
namespace CrossMacro.Mcp.Services.Security;

/// <summary>
/// Blocks recursive MCP hosting, GUI lifecycle, setup, and privilege paths before
/// the normal CLI parser can see them.
/// </summary>
public static class McpCommandPolicy
{
    private const int MaximumArgumentCount = 128;
    private const int MaximumArgumentCharacters = 16_384;
    private const int MaximumPayloadCharacters = 262_144;

    private static readonly FrozenSet<string> AllowedCommands =
        McpCliCommandCatalog.SupportedCommands.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

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

    public static McpToolOutcome Validate(string command, IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(arguments);

        var normalizedCommand = command.Trim();
        if (string.IsNullOrWhiteSpace(normalizedCommand))
        {
            return McpToolOutcomeMapper.InvalidArguments("Command token is required.");
        }

        if (!AllowedCommands.Contains(normalizedCommand))
        {
            return McpToolOutcomeMapper.InvalidArguments("This command is not available through command.execute.");
        }

        if (arguments.Count > MaximumArgumentCount)
        {
            return McpToolOutcomeMapper.InvalidArguments("Command argument count exceeds the maximum allowed value.");
        }

        var totalCharacters = normalizedCommand.Length;
        foreach (var argument in arguments)
        {
            if (argument is null || argument.Length > MaximumArgumentCharacters)
            {
                return McpToolOutcomeMapper.InvalidArguments($"Command arguments must be non-null and at most {MaximumArgumentCharacters.ToString(CultureInfo.InvariantCulture)} characters.");
            }

            totalCharacters = checked(totalCharacters + argument.Length);
            if (totalCharacters > MaximumPayloadCharacters)
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
