using CrossMacro.Cli.Parsing;

namespace CrossMacro.Cli;

/// <summary>
/// Owns the CLI's top-level command and startup-token catalogue.
/// Keeping this data outside the parser makes command registration and alias
/// changes reviewable without mixing them with parse control flow.
/// </summary>
internal static class CliCommandCatalog
{
    internal delegate CliParseResult ParseCommandDelegate(string[] args);

    internal sealed record RootCommandDescriptor(
        string CommandToken,
        ParseCommandDelegate ParseCommand,
        params string[] Aliases);

    internal static readonly IReadOnlySet<string> StandaloneCliOptionTokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "--json",
        "--log-level",
        "--speed",
        "--loop",
        "--repeat",
        "--repeat-delay-ms",
        "--motion-mode",
        "--motion-rate",
        "--precision-motion-rate",
        "--motion-error-px",
        "--countdown",
        "--timeout",
        "--dry-run",
        "--verbose",
        "--output",
        "-o",
        "--mouse",
        "--keyboard",
        "--mode",
        "--skip-initial-zero",
        "--duration",
        "--step",
        "--file",
        "--asset",
        "--active",
        "--address",
        "--title",
        "--class",
        "--field",
        "--value",
        "--action",
        "--timeout-ms",
        "--relative",
        "--tolerance",
        "--similarity",
        "--button",
        "--region",
        "--clipboard",
        "--all",
        "--force",
        "--profile",
        "--method",
        "--insertion-mode",
        "--direct-typing-method",
        "--name",
        "--macro",
        "--interval",
        "--at",
        "--weekly",
        "--time",
        "--enabled",
        "--hotkey",
        "--random-repeat-delay",
        "--run-while-held",
        "--press-release",
        "--release-macro",
        "--normal",
        "--window-rule",
        "--clear-window-rules",
        "--match-mode",
        "--matchmode",
        "--cooldown-ms",
        "--debounce-ms",
        "--fire-mode",
        "--restricted",
    };

    internal static readonly IReadOnlyList<RootCommandDescriptor> RootCommands =
        CliCommandContractCatalog.RootCommands
            .Select(static contract => new RootCommandDescriptor(
                contract.CommandToken,
                GetParser(contract.CommandToken),
                contract.Aliases.ToArray()))
            .ToArray();

    internal static readonly IReadOnlyDictionary<string, RootCommandDescriptor> RootCommandLookup = BuildRootCommandLookup();

    internal static readonly IReadOnlyList<string> TopLevelUsageSections =
    [
        .. Indent(CliUsage.Gui, CliUsage.MacroValidate, CliUsage.MacroInfo, CliUsage.Play, CliUsage.Doctor, CliUsage.Setup),
        string.Empty,
        .. Indent(CliUsage.SettingsLines),
        .. Indent(CliUsage.ProfileSummary, CliUsage.TextExpansionSummary, CliUsage.ScheduleSummary, CliUsage.ShortcutSummary, CliUsage.TriggerSummary, CliUsage.Record),
        string.Empty,
        .. Indent(CliUsage.RunLines),
        .. Indent(CliUsage.InputLines),
        string.Empty,
        .. Indent(CliUsage.ClipboardLines),
        .. Indent(CliUsage.WindowSummary, CliUsage.ScreenSummary, CliUsage.ScreenshotSummary),
        string.Empty,
        .. Indent(CliUsage.Headless, CliUsage.HeadlessFlag, CliUsage.Mcp),
    ];

    internal static readonly string TopLevelUsageText = BuildTopLevelUsageText();

    internal static readonly IReadOnlySet<string> KnownGuiStartupOptionTokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "--start-minimized",
    };

    internal static readonly IReadOnlyList<string> KnownGuiStartupOptionPrefixes =
    [
        "--drm",
        "--fbdev",
        "--tty",
        "--display",
        "--x11",
        "--wayland",
    ];

    private static IEnumerable<string> Indent(params string[] lines) => lines.Select(static line => "  " + line);

    private static string BuildTopLevelUsageText()
    {
        return
            "Usage:\n" +
            string.Join('\n', TopLevelUsageSections) +
            "\n\nDetailed Help:\n" +
            "  crossmacro <command> --help\n" +
            "  Example: crossmacro settings --help\n\n" +
            "Options:\n" +
            "  -h, --help       Show help\n" +
            "  -v, --version    Show version\n" +
            "  --start-minimized  Start GUI minimized and hide to tray when available\n" +
            "  --json           Print result in JSON format\n" +
            "  --log-level      Override logger level (Verbose, Debug, Information, Warning, Error, Fatal)\n";
    }

    private static Dictionary<string, RootCommandDescriptor> BuildRootCommandLookup()
    {
        var lookup = new Dictionary<string, RootCommandDescriptor>(StringComparer.OrdinalIgnoreCase);

        foreach (var command in RootCommands)
        {
            lookup[command.CommandToken] = command;

            foreach (var alias in command.Aliases)
            {
                lookup[alias] = command;
            }
        }

        return lookup;
    }

    private static ParseCommandDelegate GetParser(string commandToken) => commandToken switch
    {
        "macro" => MacroCommandParser.Parse,
        "play" => PlayCommandParser.Parse,
        "doctor" => DoctorCommandParser.Parse,
        "setup" => QuickSetupCommandParser.Parse,
        "settings" => SettingsCommandParser.Parse,
        "profile" => ProfileCommandParser.Parse,
        "text-expansion" => TextExpansionCommandParser.Parse,
        "schedule" => ScheduleCommandParser.Parse,
        "shortcut" => ShortcutCommandParser.Parse,
        "trigger" => TriggerCommandParser.Parse,
        "record" => RecordCommandParser.Parse,
        "run" => RunCommandParser.Parse,
        "move" or "click" or "down" or "up" or "scroll" or "key" or "tap" or "type" or "delay" => InputCommandParser.Parse,
        "clipboard" => ClipboardCommandParser.Parse,
        "window" => WindowCommandParser.Parse,
        "screen" => ScreenCommandParser.Parse,
        "screenshot" => ScreenshotCommandParser.Parse,
        "headless" => HeadlessCommandParser.Parse,
        "mcp" => McpCommandParser.Parse,
        _ => throw new InvalidOperationException($"No parser registered for CLI command contract: {commandToken}"),
    };
}
