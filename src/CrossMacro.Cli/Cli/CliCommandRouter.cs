using System;
using System.Collections.Generic;
using System.Linq;
using CrossMacro.Cli.Services;

namespace CrossMacro.Cli;

public sealed class CliCommandRouter
{
    private static readonly HashSet<string> StandaloneCliOptionTokens = new(StringComparer.OrdinalIgnoreCase)
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
        "--file"
    };

    private static readonly HashSet<string> KnownGuiStartupOptionTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "--start-minimized"
    };

    private static readonly string[] KnownGuiStartupOptionPrefixes =
    [
        "--drm",
        "--fbdev",
        "--tty",
        "--display",
        "--x11",
        "--wayland"
    ];

    public CliParseResult Parse(string[] args)
    {
        if (args == null || args.Length == 0)
        {
            return CliParseResult.Gui();
        }

        var first = args[0];

        if (IsHelpToken(first))
        {
            return CliParseResult.Help();
        }

        if (IsVersionToken(first))
        {
            return CliParseResult.Version();
        }

        if (!IsCliCommandToken(first))
        {
            if (IsStandaloneCliOptionToken(first))
            {
                return CliParseResult.Error($"Option '{first}' requires a command. See 'crossmacro --help'.");
            }

            if (ShouldTreatAsGuiStartup(first))
            {
                return CliParseResult.Gui();
            }

            if (LooksLikeOptionToken(first))
            {
                return CliParseResult.Error($"Unknown option: {first}");
            }

            return CliParseResult.Error($"Unknown command: {first}");
        }

        if (string.Equals(first, "macro", StringComparison.OrdinalIgnoreCase))
        {
            return MacroCommandParser.Parse(args);
        }

        if (string.Equals(first, "play", StringComparison.OrdinalIgnoreCase))
        {
            return PlayCommandParser.Parse(args);
        }

        if (string.Equals(first, "doctor", StringComparison.OrdinalIgnoreCase))
        {
            return DoctorCommandParser.Parse(args);
        }

        if (string.Equals(first, "settings", StringComparison.OrdinalIgnoreCase))
        {
            return SettingsCommandParser.Parse(args);
        }

        if (string.Equals(first, "schedule", StringComparison.OrdinalIgnoreCase))
        {
            return ScheduleCommandParser.Parse(args);
        }

        if (string.Equals(first, "shortcut", StringComparison.OrdinalIgnoreCase))
        {
            return ShortcutCommandParser.Parse(args);
        }

        if (string.Equals(first, "record", StringComparison.OrdinalIgnoreCase))
        {
            return RecordCommandParser.Parse(args);
        }

        if (string.Equals(first, "run", StringComparison.OrdinalIgnoreCase))
        {
            return RunCommandParser.Parse(args);
        }

        if (string.Equals(first, "headless", StringComparison.OrdinalIgnoreCase)
            || string.Equals(first, "--headless", StringComparison.OrdinalIgnoreCase))
        {
            return HeadlessCommandParser.Parse(args);
        }

        return CliParseResult.Error($"Unknown command: {first}");
    }

    public string GetUsage(string? topic = null)
    {
        if (!string.IsNullOrWhiteSpace(topic))
        {
            return GetTopicUsage(topic);
        }

        return
            "Usage:\n" +
            "  crossmacro [--start-minimized]\n" +
            "  crossmacro macro validate <macro-file> [--json] [--log-level <level>]\n" +
            "  crossmacro macro info <macro-file> [--json] [--log-level <level>]\n" +
            "  crossmacro play <macro-file> [--speed <value>] [--loop] [--repeat <n>] [--repeat-delay-ms <ms>] [--countdown <sec>] [--timeout <sec>] [--dry-run] [--json] [--log-level <level>]\n" +
            "  crossmacro doctor [--verbose] [--json] [--log-level <level>]\n\n" +
            "  crossmacro settings get [<key>] [--json] [--log-level <level>]\n" +
            "  crossmacro settings set <key> <value> [--json] [--log-level <level>]\n" +
            "  crossmacro schedule list [--json] [--log-level <level>]\n" +
            "  crossmacro schedule run <task-id> [--json] [--log-level <level>]\n" +
            "  crossmacro shortcut list [--json] [--log-level <level>]\n" +
            "  crossmacro shortcut run <task-id> [--json] [--log-level <level>]\n" +
            "  crossmacro record (--output|-o) <macro-file> [--mouse <true|false>] [--keyboard <true|false>] [--mode <auto|absolute|relative>] [--skip-initial-zero] [--duration <sec>] [--json] [--log-level <level>]\n\n" +
            "  crossmacro run --step <step> [--step <step> ...] [--file <steps-file>] [--speed <value>] [--countdown <sec>] [--timeout <sec>] [--dry-run] [--json] [--log-level <level>]\n" +
            "  crossmacro run <step-command> [<step-command> ...] [--file <steps-file>] [--speed <value>] [--countdown <sec>] [--timeout <sec>] [--dry-run] [--json] [--log-level <level>]\n\n" +
            "  crossmacro headless [--json] [--log-level <level>]\n" +
            "  crossmacro --headless [--json] [--log-level <level>]\n\n" +
            "Detailed Help:\n" +
            "  crossmacro <command> --help\n" +
            "  Example: crossmacro settings --help\n\n" +
            "Options:\n" +
            "  -h, --help       Show help\n" +
            "  -v, --version    Show version\n" +
            "  --start-minimized  Start GUI minimized and hide to tray when available\n" +
            "  --json           Print result in JSON format\n" +
            "  --log-level      Override logger level (Verbose, Debug, Information, Warning, Error, Fatal)\n";
    }

    private static string GetTopicUsage(string topic)
    {
        if (string.Equals(topic, "macro", StringComparison.OrdinalIgnoreCase))
        {
            return
                "Usage:\n" +
                "  crossmacro macro validate <macro-file> [--json] [--log-level <level>]\n" +
                "  crossmacro macro info <macro-file> [--json] [--log-level <level>]\n\n" +
                "Subcommands:\n" +
                "  validate  Validate macro syntax and playback compatibility.\n" +
                "  info      Show macro metadata and event breakdown.\n\n" +
                "Try:\n" +
                "  crossmacro macro validate --help\n" +
                "  crossmacro macro info --help\n";
        }

        if (string.Equals(topic, "macro.validate", StringComparison.OrdinalIgnoreCase))
        {
            return
                "Usage:\n" +
                "  crossmacro macro validate <macro-file> [--json] [--log-level <level>]\n\n" +
                "Description:\n" +
                "  Loads the macro file and runs validation checks without playback.\n\n" +
                "Examples:\n" +
                "  crossmacro macro validate ./demo.macro\n" +
                "  crossmacro macro validate ./demo.macro --json\n";
        }

        if (string.Equals(topic, "macro.info", StringComparison.OrdinalIgnoreCase))
        {
            return
                "Usage:\n" +
                "  crossmacro macro info <macro-file> [--json] [--log-level <level>]\n\n" +
                "Description:\n" +
                "  Loads the macro file and prints metadata (event count, duration, breakdown).\n\n" +
                "Examples:\n" +
                "  crossmacro macro info ./demo.macro\n" +
                "  crossmacro macro info ./demo.macro --json\n";
        }

        if (string.Equals(topic, "play", StringComparison.OrdinalIgnoreCase))
        {
            return
                "Usage:\n" +
                "  crossmacro play <macro-file> [--speed <value>] [--loop] [--repeat <n>] [--repeat-delay-ms <ms>] [--countdown <sec>] [--timeout <sec>] [--dry-run] [--json] [--log-level <level>]\n\n" +
                "Options:\n" +
                "  --speed <value>         Playback speed (0.1..10.0)\n" +
                "  --loop                  Enable loop mode (infinite if --repeat is omitted)\n" +
                "  --repeat <n>            Repeat count (> 0 implies loop mode; 0 requires --loop)\n" +
                "  --repeat-delay-ms <ms>  Delay between repeats in milliseconds (>= 0)\n" +
                "  --countdown <sec>       Countdown before start (>= 0)\n" +
                "  --timeout <sec>         Command timeout (>= 0)\n" +
                "  --dry-run               Validate only; do not send input events\n\n" +
                "Examples:\n" +
                "  crossmacro play ./demo.macro\n" +
                "  crossmacro play ./demo.macro --repeat 3 --speed 1.25\n" +
                "  crossmacro play ./demo.macro --dry-run --json\n";
        }

        if (string.Equals(topic, "doctor", StringComparison.OrdinalIgnoreCase))
        {
            return
                "Usage:\n" +
                "  crossmacro doctor [--verbose] [--json] [--log-level <level>]\n\n" +
                "Description:\n" +
                "  Runs environment and backend readiness checks (session, daemon, uinput, providers).\n\n" +
                "Options:\n" +
                "  --verbose  Include diagnostic details in output data.\n\n" +
                "Examples:\n" +
                "  crossmacro doctor\n" +
                "  crossmacro doctor --verbose --json\n";
        }

        if (string.Equals(topic, "settings", StringComparison.OrdinalIgnoreCase))
        {
            var keys = string.Join("\n", SettingsCliService.GetSupportedKeys().Select(k => $"  - {k}"));

            return
                "Usage:\n" +
                "  crossmacro settings get [<key>] [--json] [--log-level <level>]\n" +
                "  crossmacro settings set <key> <value> [--json] [--log-level <level>]\n\n" +
                "Subcommands:\n" +
                "  get   Read one key or all supported keys.\n" +
                "  set   Update a single key.\n\n" +
                "Supported Keys:\n" +
                $"{keys}\n\n" +
                "Try:\n" +
                "  crossmacro settings get --help\n" +
                "  crossmacro settings set --help\n";
        }

        if (string.Equals(topic, "settings.get", StringComparison.OrdinalIgnoreCase))
        {
            var keys = string.Join("\n", SettingsCliService.GetSupportedKeys().Select(k => $"  - {k}"));

            return
                "Usage:\n" +
                "  crossmacro settings get [<key>] [--json] [--log-level <level>]\n\n" +
                "Description:\n" +
                "  Without <key>, prints all supported key/value pairs.\n" +
                "  With <key>, prints only that key.\n\n" +
                "Supported Keys:\n" +
                $"{keys}\n\n" +
                "Examples:\n" +
                "  crossmacro settings get\n" +
                "  crossmacro settings get playback.speed\n" +
                "  crossmacro settings get logging.level --json\n";
        }

        if (string.Equals(topic, "settings.set", StringComparison.OrdinalIgnoreCase))
        {
            var keys = string.Join("\n", SettingsCliService.GetSupportedKeys().Select(k => $"  - {k}"));

            return
                "Usage:\n" +
                "  crossmacro settings set <key> <value> [--json] [--log-level <level>]\n\n" +
                "Supported Keys:\n" +
                $"{keys}\n\n" +
                "Value Notes:\n" +
                "  playback.speed             double\n" +
                "  playback.loop              bool (true/false/1/0/yes/no/on/off)\n" +
                "  playback.loopCount         integer >= 0\n" +
                "  playback.loopDelayMs       integer >= 0\n" +
                "  playback.countdownSeconds  integer >= 0\n" +
                "  logging.level              Debug|Information|Warning|Error\n" +
                "  recording.mouse            bool\n" +
                "  recording.keyboard         bool\n" +
                "  recording.forceRelative    bool\n" +
                "  recording.skipInitialZeroZero bool\n" +
                "  textExpansion.enabled      bool\n\n" +
                "Examples:\n" +
                "  crossmacro settings set playback.speed 1.25\n" +
                "  crossmacro settings set playback.loop true\n" +
                "  crossmacro settings set logging.level Warning\n";
        }

        if (string.Equals(topic, "schedule", StringComparison.OrdinalIgnoreCase))
        {
            return
                "Usage:\n" +
                "  crossmacro schedule list [--json] [--log-level <level>]\n" +
                "  crossmacro schedule run <task-id> [--json] [--log-level <level>]\n\n" +
                "Subcommands:\n" +
                "  list   List known schedule tasks.\n" +
                "  run    Trigger a schedule task by task id.\n";
        }

        if (string.Equals(topic, "schedule.list", StringComparison.OrdinalIgnoreCase))
        {
            return
                "Usage:\n" +
                "  crossmacro schedule list [--json] [--log-level <level>]\n\n" +
                "Examples:\n" +
                "  crossmacro schedule list\n" +
                "  crossmacro schedule list --json\n";
        }

        if (string.Equals(topic, "schedule.run", StringComparison.OrdinalIgnoreCase))
        {
            return
                "Usage:\n" +
                "  crossmacro schedule run <task-id> [--json] [--log-level <level>]\n\n" +
                "Description:\n" +
                "  Executes one schedule task immediately using its id from schedule list output.\n\n" +
                "Examples:\n" +
                "  crossmacro schedule run 11111111-1111-1111-1111-111111111111\n" +
                "  crossmacro schedule run 11111111-1111-1111-1111-111111111111 --json\n";
        }

        if (string.Equals(topic, "shortcut", StringComparison.OrdinalIgnoreCase))
        {
            return
                "Usage:\n" +
                "  crossmacro shortcut list [--json] [--log-level <level>]\n" +
                "  crossmacro shortcut run <task-id> [--json] [--log-level <level>]\n\n" +
                "Subcommands:\n" +
                "  list   List known shortcut tasks.\n" +
                "  run    Trigger a shortcut task by task id.\n";
        }

        if (string.Equals(topic, "shortcut.list", StringComparison.OrdinalIgnoreCase))
        {
            return
                "Usage:\n" +
                "  crossmacro shortcut list [--json] [--log-level <level>]\n\n" +
                "Examples:\n" +
                "  crossmacro shortcut list\n" +
                "  crossmacro shortcut list --json\n";
        }

        if (string.Equals(topic, "shortcut.run", StringComparison.OrdinalIgnoreCase))
        {
            return
                "Usage:\n" +
                "  crossmacro shortcut run <task-id> [--json] [--log-level <level>]\n\n" +
                "Description:\n" +
                "  Executes one shortcut task immediately using its id from shortcut list output.\n\n" +
                "Examples:\n" +
                "  crossmacro shortcut run 22222222-2222-2222-2222-222222222222\n" +
                "  crossmacro shortcut run 22222222-2222-2222-2222-222222222222 --json\n";
        }

        if (string.Equals(topic, "record", StringComparison.OrdinalIgnoreCase))
        {
            return
                "Usage:\n" +
                "  crossmacro record (--output|-o) <macro-file> [--mouse <true|false>] [--keyboard <true|false>] [--mode <auto|absolute|relative>] [--skip-initial-zero] [--duration <sec>] [--json] [--log-level <level>]\n\n" +
                "Options:\n" +
                "  --output, -o <macro-file>  Output file path (required)\n" +
                "  --mouse <bool>             Capture mouse events\n" +
                "  --keyboard <bool>          Capture keyboard events\n" +
                "  --mode <auto|absolute|relative>\n" +
                "                             Coordinate recording mode\n" +
                "  --skip-initial-zero        Do not insert initial 0,0 move for relative mode\n" +
                "  --duration <sec>           Auto-stop duration in seconds (>= 0)\n\n" +
                "Examples:\n" +
                "  crossmacro record -o ./new.macro\n" +
                "  crossmacro record -o ./new.macro --mode relative --duration 10\n";
        }

        if (string.Equals(topic, "run", StringComparison.OrdinalIgnoreCase))
        {
            return
                "Usage:\n" +
                "  crossmacro run --step <step> [--step <step> ...] [--file <steps-file>] [--speed <value>] [--countdown <sec>] [--timeout <sec>] [--dry-run] [--json] [--log-level <level>]\n" +
                "  crossmacro run <step-command> [<step-command> ...] [--file <steps-file>] [--speed <value>] [--countdown <sec>] [--timeout <sec>] [--dry-run] [--json] [--log-level <level>]\n\n" +
                "Run Steps:\n" +
                "  move abs <x> <y>\n" +
                "  move rel <dx> <dy>\n" +
                "  down <button> | up <button> | click <button>\n" +
                "  down current <button> | up current <button> | click current <button>\n" +
                "  scroll <up|down|left|right> [count]\n" +
                "  key down <key> | key up <key>\n" +
                "  tap <combo>\n" +
                "  delay <ms>\n" +
                "  delay random <min> <max> | delay random <min>..<max>\n" +
                "  set <name> <value> | set <name>=<value>\n" +
                "  inc <name> [amount] | dec <name> [amount]\n" +
                "  repeat <count> { ... }\n" +
                "  if <left> <op> <right> { ... } else { ... }\n" +
                "  while <left> <op> <right> { ... }\n" +
                "  for <var> from <start> to <end> [step <n>] { ... }\n" +
                "  break | continue\n" +
                "  type <text>\n\n" +
                "Examples:\n" +
                "  crossmacro run --step \"move abs 500 300\" --step \"click left\" --dry-run\n" +
                "  crossmacro run move rel 100 0 delay 40 click left\n" +
                "  crossmacro run --step \"set x=640\" --step \"set y=360\" --step \"move abs $x $y\"\n" +
                "  crossmacro run --step \"repeat 3 {\" --step \"click left\" --step \"delay random 40 90\" --step \"}\"\n" +
                "  crossmacro run --step \"set i=0\" --step \"while $i < 3 {\" --step \"click left\" --step \"inc i\" --step \"}\"\n" +
                "  crossmacro run --step \"delay random 40..90\" --step \"click left\"\n" +
                "  crossmacro run --file ./steps.txt --json\n";
        }

        if (string.Equals(topic, "headless", StringComparison.OrdinalIgnoreCase))
        {
            return
                "Usage:\n" +
                "  crossmacro headless [--json] [--log-level <level>]\n" +
                "  crossmacro --headless [--json] [--log-level <level>]\n\n" +
                "Description:\n" +
                "  Starts background runtime services without opening GUI.\n" +
                "  Active services: global hotkeys, scheduler, shortcuts, text expansion.\n\n" +
                "Hotkey Behavior:\n" +
                "  Recording hotkey: start/stop recording in current headless session.\n" +
                "  Playback hotkey: play/stop the last macro recorded in current headless session.\n" +
                "  Pause hotkey: pause/resume active playback.\n\n" +
                "Notes:\n" +
                "  Playback hotkey requires a macro recorded in the same headless session.\n" +
                "  Stops on Ctrl+C (exit code 130).\n";
        }

        return "Usage:\n  crossmacro --help\n";
    }

    private static bool IsCliCommandToken(string firstToken)
    {
        if (string.IsNullOrWhiteSpace(firstToken))
        {
            return false;
        }

        return string.Equals(firstToken, "--headless", StringComparison.OrdinalIgnoreCase)
            || string.Equals(firstToken, "macro", StringComparison.OrdinalIgnoreCase)
            || string.Equals(firstToken, "play", StringComparison.OrdinalIgnoreCase)
            || string.Equals(firstToken, "doctor", StringComparison.OrdinalIgnoreCase)
            || string.Equals(firstToken, "schedule", StringComparison.OrdinalIgnoreCase)
            || string.Equals(firstToken, "shortcut", StringComparison.OrdinalIgnoreCase)
            || string.Equals(firstToken, "settings", StringComparison.OrdinalIgnoreCase)
            || string.Equals(firstToken, "record", StringComparison.OrdinalIgnoreCase)
            || string.Equals(firstToken, "run", StringComparison.OrdinalIgnoreCase)
            || string.Equals(firstToken, "headless", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ShouldTreatAsGuiStartup(string firstToken)
    {
        if (string.IsNullOrWhiteSpace(firstToken))
        {
            return true;
        }

        if (KnownGuiStartupOptionTokens.Contains(firstToken))
        {
            return true;
        }

        if (firstToken.StartsWith("-psn_", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        foreach (var prefix in KnownGuiStartupOptionPrefixes)
        {
            if (string.Equals(firstToken, prefix, StringComparison.OrdinalIgnoreCase)
                || firstToken.StartsWith($"{prefix}=", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool LooksLikeOptionToken(string token)
    {
        return token.StartsWith("-", StringComparison.Ordinal);
    }

    private static bool IsStandaloneCliOptionToken(string token)
    {
        return StandaloneCliOptionTokens.Contains(token);
    }

    private static bool IsHelpToken(string token)
    {
        return CliParseHelpers.IsHelpToken(token);
    }

    private static bool IsVersionToken(string token)
    {
        return CliParseHelpers.IsVersionToken(token);
    }
}
