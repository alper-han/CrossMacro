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
        "--timeout-ms",
        "--relative",
        "--tolerance",
        "--poll",
        "--poll-ms",
        "--similarity",
        "--downsample",
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
        "--match-mode",
        "--cooldown-ms",
        "--debounce-ms",
        "--fire-mode",
    };

    internal static readonly IReadOnlyList<RootCommandDescriptor> RootCommands =
    [
        new("macro", MacroCommandParser.Parse),
        new("play", PlayCommandParser.Parse),
        new("doctor", DoctorCommandParser.Parse),
        new("settings", SettingsCommandParser.Parse),
        new("profile", ProfileCommandParser.Parse),
        new("text-expansion", TextExpansionCommandParser.Parse, "text"),
        new("schedule", ScheduleCommandParser.Parse),
        new("shortcut", ShortcutCommandParser.Parse),
        new("trigger", TriggerCommandParser.Parse),
        new("record", RecordCommandParser.Parse),
        new("run", RunCommandParser.Parse),
        new("move", InputCommandParser.Parse),
        new("click", InputCommandParser.Parse),
        new("down", InputCommandParser.Parse),
        new("up", InputCommandParser.Parse),
        new("scroll", InputCommandParser.Parse),
        new("key", InputCommandParser.Parse),
        new("tap", InputCommandParser.Parse),
        new("type", InputCommandParser.Parse),
        new("delay", InputCommandParser.Parse),
        new("clipboard", ClipboardCommandParser.Parse),
        new("window", WindowCommandParser.Parse),
        new("screen", ScreenCommandParser.Parse),
        new("screenshot", ScreenshotCommandParser.Parse),
        new("headless", HeadlessCommandParser.Parse, "--headless"),
    ];

    internal static readonly IReadOnlyDictionary<string, RootCommandDescriptor> RootCommandLookup = BuildRootCommandLookup();

    internal static readonly IReadOnlyList<string> TopLevelUsageSections =
    [
        "  crossmacro [--start-minimized]",
        "  crossmacro macro validate <macro-file> [--json] [--log-level <level>]",
        "  crossmacro macro info <macro-file> [--json] [--log-level <level>]",
        "  crossmacro play <macro-file> [--speed <value>] [--loop] [--repeat <n>] [--repeat-delay-ms <ms>] [--countdown <sec>] [--timeout <sec>] [--dry-run] [--json] [--log-level <level>]",
        "  crossmacro doctor [--verbose] [--json] [--log-level <level>]",
        string.Empty,
        "  crossmacro settings get [<key>] [--json] [--log-level <level>]",
        "  crossmacro settings get --all [--json] [--log-level <level>]",
        "  crossmacro settings set <key> <value> [--json] [--log-level <level>]",
        "  crossmacro settings list-keys [--json] [--log-level <level>]",
        "  crossmacro settings reset <key> [--json] [--log-level <level>]",
        "  crossmacro profile list|current|create|switch|rename|delete ... [--json] [--log-level <level>]",
        "  crossmacro text-expansion list|add|remove|enable|disable|test ... [--json] [--log-level <level>]",
        "  crossmacro schedule list|run|add|edit|remove|enable|disable|next ... [--json] [--log-level <level>]",
        "  crossmacro shortcut list|run|add|edit|remove|enable|disable|bind ... [--json] [--log-level <level>]",
        "  crossmacro trigger list|add|edit|remove|enable|disable ... [--json] [--log-level <level>]",
        "  crossmacro record (--output|-o) <macro-file> [--mouse <true|false>] [--keyboard <true|false>] [--mode <auto|absolute|relative>] [--skip-initial-zero] [--duration <sec>] [--json] [--log-level <level>]",
        string.Empty,
        "  crossmacro run --step <step> [--step <step> ...] [--file <steps-file>] [--asset <name> <png-path>] [--speed <value>] [--countdown <sec>] [--timeout <sec>] [--dry-run] [--json] [--log-level <level>]",
        "  crossmacro run <step-command> [<step-command> ...] [--file <steps-file>] [--asset <name> <png-path>] [--speed <value>] [--countdown <sec>] [--timeout <sec>] [--dry-run] [--json] [--log-level <level>]",
        "  crossmacro move abs|rel|rel-logical|rel-raw <x> <y> [--dry-run] [--json] [--log-level <level>]",
        "  crossmacro click|down|up [current] <button> [--dry-run] [--json] [--log-level <level>]",
        "  crossmacro scroll <up|down|left|right> [count] [--dry-run] [--json] [--log-level <level>]",
        "  crossmacro key down|up <key> [--dry-run] [--json] [--log-level <level>]",
        "  crossmacro tap <combo> [--dry-run] [--json] [--log-level <level>]",
        "  crossmacro type <text> [--dry-run] [--json] [--log-level <level>]",
        "  crossmacro delay <ms>|delay random <min> <max> [--dry-run] [--json] [--log-level <level>]",
        string.Empty,
        "  crossmacro clipboard get [--json] [--log-level <level>]",
        "  crossmacro clipboard set <text> [--json] [--log-level <level>]",
        "  crossmacro clipboard set --file <path> [--json] [--log-level <level>]",
        "  crossmacro clipboard clear [--json] [--log-level <level>]",
        "  crossmacro window active|list|search|wait|focus|close|move|resize|center|maximize|fullscreen|float|workspace ... [--json] [--log-level <level>]",
        "  crossmacro screen pixel|wait-color|search-color|search-image|wait-image|image-click ... [--json] [--log-level <level>]",
        "  crossmacro screenshot ((--output|-o) <path>|--clipboard) [--region <x> <y> <width> <height>] [--json] [--log-level <level>]",
        string.Empty,
        "  crossmacro headless [--json] [--log-level <level>]",
        "  crossmacro --headless [--json] [--log-level <level>]",
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
}
