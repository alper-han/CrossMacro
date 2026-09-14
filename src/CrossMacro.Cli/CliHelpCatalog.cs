namespace CrossMacro.Cli;

/// <summary>Combines command-specific guidance with the canonical option vocabulary.</summary>
internal static class CliHelpCatalog
{
    public static string GetUsage(string? topic)
    {
        if (string.IsNullOrWhiteSpace(topic))
        {
            return CliCommandCatalog.TopLevelUsageText;
        }

        var guidance = GetTopicUsage(topic);
        var root = topic.Split('.')[0];
        var command = CliCommandContractCatalog.RootCommands.FirstOrDefault(candidate =>
            string.Equals(candidate.CommandToken, root, StringComparison.OrdinalIgnoreCase)
            || candidate.Aliases.Contains(root, StringComparer.OrdinalIgnoreCase));
        if (command is null)
        {
            return guidance;
        }

        var undocumented = command.Options.Where(option => !guidance.Contains(option.Token, StringComparison.Ordinal)).ToArray();
        if (undocumented.Length is 0)
        {
            return guidance;
        }

        var options = undocumented.Select(option =>
        {
            var values = option.AllowedValues.Count > 0 ? string.Join('|', option.AllowedValues) : "value";
            var operand = option.RequiresValue ? " <" + values + ">" : string.Empty;
            var defaultValue = option.DefaultValue is not null ? " (default: " + option.DefaultValue + ")" : string.Empty;
            return "  " + option.Token + operand + defaultValue;
        });
        return guidance + "\nAdditional supported options:\n" + string.Join('\n', options) + "\n";
    }

    private static readonly IReadOnlyDictionary<string, Func<string>> TopicUsage = new Dictionary<string, Func<string>>(StringComparer.OrdinalIgnoreCase)
    {
        ["macro"] = static () =>
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
        },
        ["macro.validate"] = static () =>
        {
            return
                "Usage:\n" +
                "  crossmacro macro validate <macro-file> [--json] [--log-level <level>]\n\n" +
                "Description:\n" +
                "  Loads the macro file and runs validation checks without playback.\n\n" +
                "Examples:\n" +
                "  crossmacro macro validate ./demo.macro\n" +
                "  crossmacro macro validate ./demo.macro --json\n";
        },
        ["macro.info"] = static () =>
        {
            return
                "Usage:\n" +
                "  crossmacro macro info <macro-file> [--json] [--log-level <level>]\n\n" +
                "Description:\n" +
                "  Loads the macro file and prints metadata (event count, duration, breakdown).\n\n" +
                "Examples:\n" +
                "  crossmacro macro info ./demo.macro\n" +
                "  crossmacro macro info ./demo.macro --json\n";
        },
        ["play"] = static () =>
        {
            return
                "Usage:\n" +
                "  crossmacro play <macro-file> [--speed <value>] [--motion-mode precision|strict-speed] [--motion-rate <reports/s>] [--precision-motion-rate <reports/s>] [--loop] [--repeat <n>] [--repeat-delay-ms <ms>] [--countdown <sec>] [--timeout <sec>] [--dry-run] [--json] [--log-level <level>]\n\n" +
                "Options:\n" +
                "  --speed <value>         Playback speed (0.1..10.0)\n" +
                "  --motion-mode <mode>    Motion fidelity: precision (default) or strict-speed\n" +
                "  --motion-rate <rate>    Strict-speed cap in reports/second (60..10000)\n" +
                "  --precision-motion-rate <rate>  Precision quality cap in reports/second (60..10000)\n" +
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
        },
        ["doctor"] = static () =>
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
        },
        ["setup"] = static () =>
        {
            return
                "Usage:\n" +
                "  crossmacro setup [--json] [--log-level <level>]\n\n" +
                "Description:\n" +
                "  Grants temporary direct input access for the current Flatpak or AppImage Wayland session.\n" +
                "  The command uses the same host authorization flow as the GUI Quick Setup.\n\n" +
                "Examples:\n" +
                "  crossmacro setup\n" +
                "  crossmacro setup --json\n";
        },
        ["settings"] = static () =>
        {
            var keys = string.Join('\n', SettingsCliService.SupportedKeys.Select(k => $"  - {k}"));

            return
                "Usage:\n" +
                "  crossmacro settings get [<key>|--all] [--json] [--log-level <level>]\n" +
                "  crossmacro settings set <key> <value> [--json] [--log-level <level>]\n" +
                "  crossmacro settings list-keys [--json] [--log-level <level>]\n" +
                "  crossmacro settings reset <key> [--json] [--log-level <level>]\n\n" +
                "Subcommands:\n" +
                "  get        Read one key or all supported keys.\n" +
                "  set        Update a single key.\n" +
                "  list-keys  List supported public keys.\n" +
                "  reset      Reset one key to its default value.\n\n" +
                "Supported Keys:\n" +
                $"{keys}\n\n" +
                "Try:\n" +
                "  crossmacro settings get --help\n" +
                "  crossmacro settings set --help\n";
        },
        ["settings.get"] = static () =>
        {
            var keys = string.Join('\n', SettingsCliService.SupportedKeys.Select(k => $"  - {k}"));

            return
                "Usage:\n" +
                "  crossmacro settings get [<key>|--all] [--json] [--log-level <level>]\n\n" +
                "Description:\n" +
                "  Without <key>, prints all supported key/value pairs.\n" +
                "  With --all, explicitly prints all supported key/value pairs.\n" +
                "  With <key>, prints only that key.\n\n" +
                "Supported Keys:\n" +
                $"{keys}\n\n" +
                "Examples:\n" +
                "  crossmacro settings get\n" +
                "  crossmacro settings get --all --json\n" +
                "  crossmacro settings get playback.speed\n" +
                "  crossmacro settings get logging.level --json\n";
        },
        ["settings.set"] = static () =>
        {
            var keys = string.Join('\n', SettingsCliService.SupportedKeys.Select(k => $"  - {k}"));

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
                "  recording.logicalRelative  bool\n" +
                "  recording.skipInitialZeroZero bool\n" +
                "  textExpansion.enabled      bool\n\n" +
                "  ui.theme                  string\n" +
                "  ui.language               string\n" +
                "  ui.trayIcon               bool\n" +
                "  ui.startMinimized         bool\n" +
                "  updates.checkForUpdates   bool\n\n" +
                "Examples:\n" +
                "  crossmacro settings set playback.speed 1.25\n" +
                "  crossmacro settings set playback.loop true\n" +
                "  crossmacro settings set logging.level Warning\n";
        },
        ["settings.list-keys"] = static () =>
        {
            return "Usage:\n  crossmacro settings list-keys [--json] [--log-level <level>]\n";
        },
        ["settings.reset"] = static () =>
        {
            return "Usage:\n  crossmacro settings reset <key> [--json] [--log-level <level>]\n";
        },
        ["profile"] = static () =>
        {
            return
                "Usage:\n" +
                "  crossmacro profile list [--json] [--log-level <level>]\n" +
                "  crossmacro profile current [--json] [--log-level <level>]\n" +
                "  crossmacro profile create <name> [--json] [--log-level <level>]\n" +
                "  crossmacro profile switch <name-or-id> [--json] [--log-level <level>]\n" +
                "  crossmacro profile rename <name-or-id> <new-name> [--json] [--log-level <level>]\n" +
                "  crossmacro profile delete <name-or-id> --force [--json] [--log-level <level>]\n\n" +
                "Profile export/import is intentionally deferred until archive restore semantics are specified.\n";
        },
        ["text-expansion"] = static () =>
        {
            return
                "Usage:\n" +
                "  crossmacro text-expansion list [--profile <name-or-id>] [--json] [--log-level <level>]\n" +
                "  crossmacro text-expansion add <trigger> <replacement> [--method CtrlV|CtrlShiftV|ShiftInsert] [--insertion-mode Paste|DirectTyping] [--direct-typing-method FastBatch|CompatibleKeyByKey] [--profile <name-or-id>] [--json] [--log-level <level>]\n" +
                "  crossmacro text-expansion remove|enable|disable|test <trigger> [--profile <name-or-id>] [--json] [--log-level <level>]\n\n" +
                "The --profile option edits that profile's storage without switching the active profile. test only resolves an expansion; it does not type or paste.\n";
        },
        ["text"] = static () =>
        {
            return
                "Usage:\n" +
                "  crossmacro text-expansion list [--profile <name-or-id>] [--json] [--log-level <level>]\n" +
                "  crossmacro text-expansion add <trigger> <replacement> [--method CtrlV|CtrlShiftV|ShiftInsert] [--insertion-mode Paste|DirectTyping] [--direct-typing-method FastBatch|CompatibleKeyByKey] [--profile <name-or-id>] [--json] [--log-level <level>]\n" +
                "  crossmacro text-expansion remove|enable|disable|test <trigger> [--profile <name-or-id>] [--json] [--log-level <level>]\n\n" +
                "The --profile option edits that profile's storage without switching the active profile. test only resolves an expansion; it does not type or paste.\n";
        },
        ["schedule"] = static () =>
        {
            return
                "Usage:\n" +
                "  crossmacro schedule list [--json] [--log-level <level>]\n" +
                "  crossmacro schedule run <task-id> [--json] [--log-level <level>]\n" +
                "  crossmacro schedule add --name <name> --macro <path> [--interval <duration>|--at <datetime>|--weekly <days> --time <HH:mm>] [--speed <value>] [--enabled <bool>] [--json] [--log-level <level>]\n" +
                "  crossmacro schedule edit <task-id> [--name <name>] [--macro <path>] [--interval <duration>|--at <datetime>|--weekly <days> --time <HH:mm>] [--speed <value>] [--enabled <bool>] [--json] [--log-level <level>]\n" +
                "  crossmacro schedule remove|enable|disable|next <task-id> [--json] [--log-level <level>]\n\n" +
                "Subcommands:\n" +
                "  list     List known schedule tasks.\n" +
                "  run      Trigger a schedule task by task id.\n" +
                "  add      Create an interval, one-time, or weekly schedule task.\n" +
                "  edit     Update schedule task fields.\n" +
                "  remove   Delete a schedule task.\n" +
                "  enable   Enable a schedule task.\n" +
                "  disable  Disable a schedule task.\n" +
                "  next     Show the next run time for a schedule task.\n";
        },
        ["schedule.list"] = static () =>
        {
            return
                "Usage:\n" +
                "  crossmacro schedule list [--json] [--log-level <level>]\n\n" +
                "Examples:\n" +
                "  crossmacro schedule list\n" +
                "  crossmacro schedule list --json\n";
        },
        ["schedule.run"] = static () =>
        {
            return
                "Usage:\n" +
                "  crossmacro schedule run <task-id> [--json] [--log-level <level>]\n\n" +
                "Description:\n" +
                "  Executes one schedule task immediately using its id from schedule list output.\n\n" +
                "Examples:\n" +
                "  crossmacro schedule run 11111111-1111-1111-1111-111111111111\n" +
                "  crossmacro schedule run 11111111-1111-1111-1111-111111111111 --json\n";
        },
        ["shortcut"] = static () =>
        {
            return
                "Usage:\n" +
                "  crossmacro shortcut list [--json] [--log-level <level>]\n" +
                "  crossmacro shortcut run <task-id> [--json] [--log-level <level>]\n" +
                "  crossmacro shortcut add --name <name> --macro <path> --hotkey <keys> [--speed <value>] [--loop] [--repeat <n>] [--repeat-delay-ms <ms>] [--random-repeat-delay <min-ms> <max-ms>] [--run-while-held] [--enabled <bool>] [--json] [--log-level <level>]\n" +
                "  crossmacro shortcut edit <task-id> [--name <name>] [--macro <path>] [--hotkey <keys>] [--speed <value>] [--loop] [--repeat <n>] [--repeat-delay-ms <ms>] [--random-repeat-delay <min-ms> <max-ms>] [--run-while-held] [--enabled <bool>] [--json] [--log-level <level>]\n" +
                "  crossmacro shortcut remove|enable|disable <task-id> [--json] [--log-level <level>]\n" +
                "  crossmacro shortcut bind <task-id> <hotkey> [--json] [--log-level <level>]\n\n" +
                "Subcommands:\n" +
                "  list     List known shortcut tasks.\n" +
                "  run      Trigger a shortcut task by task id.\n" +
                "  add      Create a shortcut-bound macro task.\n" +
                "  edit     Update shortcut task fields.\n" +
                "  remove   Delete a shortcut task.\n" +
                "  enable   Enable a shortcut task.\n" +
                "  disable  Disable a shortcut task.\n" +
                "  bind     Replace a shortcut task's hotkey.\n";
        },
        ["shortcut.list"] = static () =>
        {
            return
                "Usage:\n" +
                "  crossmacro shortcut list [--json] [--log-level <level>]\n\n" +
                "Examples:\n" +
                "  crossmacro shortcut list\n" +
                "  crossmacro shortcut list --json\n";
        },
        ["shortcut.run"] = static () =>
        {
            return
                "Usage:\n" +
                "  crossmacro shortcut run <task-id> [--json] [--log-level <level>]\n\n" +
                "Description:\n" +
                "  Executes one shortcut task immediately using its id from shortcut list output.\n\n" +
                "Examples:\n" +
                "  crossmacro shortcut run 22222222-2222-2222-2222-222222222222\n" +
                "  crossmacro shortcut run 22222222-2222-2222-2222-222222222222 --json\n";
        },
        ["trigger"] = static () =>
        {
            return
                "Usage:\n" +
                "  crossmacro trigger list [--json] [--log-level <level>]\n" +
                "  crossmacro trigger add --name <name> --field <field> --match-mode <mode> --value <value> --action <action> [--profile <profile>] [--macro <path>] [--fire-mode <mode>] [--cooldown-ms <ms>] [--debounce-ms <ms>] [--enabled <bool>] [--json] [--log-level <level>]\n" +
                "  crossmacro trigger edit <task-id> [--name <name>] [--field <field>] [--match-mode <mode>] [--value <value>] [--action <action>] [--profile <profile>] [--macro <path>] [--fire-mode <mode>] [--cooldown-ms <ms>] [--debounce-ms <ms>] [--enabled <bool>] [--json] [--log-level <level>]\n" +
                "  crossmacro trigger remove|enable|disable <task-id> [--json] [--log-level <level>]\n\n" +
                "Subcommands:\n" +
                "  list     List known trigger tasks.\n" +
                "  add      Create a window-match trigger task.\n" +
                "  edit     Update trigger task fields.\n" +
                "  remove   Delete a trigger task.\n" +
                "  enable   Enable a trigger task.\n" +
                "  disable  Disable a trigger task.\n";
        },
        ["record"] = static () =>
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
        },
        ["run"] = static () =>
        {
            return
                "Usage:\n" +
                "  crossmacro run --step <step> [--step <step> ...] [--file <steps-file>] [--asset <name> <png-path>] [--speed <value>] [--countdown <sec>] [--timeout <sec>] [--dry-run] [--json] [--log-level <level>]\n" +
                "  crossmacro run <step-command> [<step-command> ...] [--file <steps-file>] [--asset <name> <png-path>] [--speed <value>] [--countdown <sec>] [--timeout <sec>] [--dry-run] [--json] [--log-level <level>]\n\n" +
                "Run Steps:\n" +
                "  move abs <integer|$variable> <integer|$variable>\n" +
                "  move rel <integer|$variable> <integer|$variable>\n" +
                "  move rel-logical <integer|$variable> <integer|$variable>\n" +
                "  move rel-raw <integer|$variable> <integer|$variable>\n" +
                "  down <button> | up <button> | click <button>\n" +
                "  down current <button> | up current <button> | click current <button>\n" +
                "  scroll <up|down|left|right> [count]\n" +
                "  key down <key> | key up <key>\n" +
                "  tap <combo>\n" +
                "  delay <duration>  (for example: 20, 2.375ms, 250us)\n" +
                "  delay random <min> <max> | delay random <min>..<max>\n" +
                "  shell \"<command>\" [retries] [backoff_ms] [timeout_ms]\n" +
                "  shell capture \"<command>\" exit_var stdout_var stderr_var [retries] [backoff_ms] [timeout_ms]\n" +
                "  shell input \"<stdin text>\" \"<command>\" [retries] [backoff_ms] [timeout_ms]\n" +
                "  shell capture-input \"<stdin text>\" \"<command>\" exit_var stdout_var stderr_var [retries] [backoff_ms] [timeout_ms]\n" +
                "  set <name> <value> | set <name>=<value>\n" +
                "  inc <name> [amount] | dec <name> [amount] | mul <name> [amount] | div <name> [amount]\n" +
                "  repeat <count> { ... }\n" +
                "  if <left> <op> <right> { ... } else { ... }\n" +
                "  while <left> <op> <right> { ... }\n" +
                "  for <var> from <start> to <end> [step <n>] { ... }\n" +
                "  break | continue\n" +
                "  type <text>\n" +
                "  pixelcolor <x> <y> [var]\n" +
                "  pixelcolor rel <dx> <dy> [var]\n" +
                "  waitcolor <x> <y> <RRGGBB|$var> [timeout_ms] [result_var]\n" +
                "  pixelsearch <x1> <y1> <x2> <y2> <RRGGBB|$var> [found_var var_x var_y|var_x var_y] [timeout <ms>] [tolerance <0..255>]\n\n" +
                "  imagesearch [<x1> <y1> <x2> <y2>] <ImageName> [found_var x_var y_var] [similarity <0..1>] [matchmode auto|first|best]\n" +
                "  imageclick [<x1> <y1> <x2> <y2>] <ImageName> [found_var x_var y_var] [button left|right|middle] [timeout <ms>] [similarity <0..1>] [matchmode auto|first|best]\n" +
                "  waitimage [<x1> <y1> <x2> <y2>] <ImageName> [found_var x_var y_var] [timeout <ms>] [similarity <0..1>] [matchmode auto|first|best]\n" +
                "  window ... | clipboard ... | screenshot ...\n\n" +
                "  --asset <name> <png-path>  Load a named PNG asset for image script steps; repeatable.\n\n" +
                "Shell capture modes store exit/stdout/stderr variables; use _ to ignore a value. Capture modes do not fail on non-zero exits.\n" +
                "Shell output captured into variables is capped at 65536 characters per stream.\n" +
                "Shell steps execute arbitrary commands; only run trusted macros. Flatpak builds run them in a restricted nested sandbox without host permissions. Use $$NAME to pass $NAME to the shell.\n\n" +
                "Examples:\n" +
                "  crossmacro run --step \"move abs 500 300\" --step \"click left\" --dry-run\n" +
                "  crossmacro run move rel 100 0 delay 40 click left\n" +
                "  crossmacro run --step \"set x=640\" --step \"set y=360\" --step \"move abs $x $y\"\n" +
                "  crossmacro run --step \"repeat 3 {\" --step \"click left\" --step \"delay random 40 90\" --step \"}\"\n" +
                "  crossmacro run --step \"set i=0\" --step \"while $i < 3 {\" --step \"click left\" --step \"inc i\" --step \"}\"\n" +
                "  crossmacro run --step \"delay random 40..90\" --step \"click left\"\n" +
                "  crossmacro run --step 'shell \"notify-send done\" 1 250 5000'\n" +
                "  crossmacro run --step 'shell capture \"printf ok\" code out err'\n" +
                "  crossmacro run --step 'shell capture-input \"hello\" \"cat\" code out err'\n" +
                "  crossmacro run --step \"pixelcolor 500 300 sampled\"\n" +
                "  crossmacro run --step \"waitcolor 500 300 00FF00 5000\"\n" +
                "  crossmacro run --step 'pixelcolor 500 300 sampled' --step 'waitcolor 500 300 $sampled 5000'\n" +
                "  crossmacro run --step \"pixelsearch 0 0 1920 1080 FF0000 found_x found_y timeout 5000 tolerance 26\"\n" +
                "  crossmacro run --asset button ./button.png --step \"waitimage button found found_x found_y timeout 5000\"\n" +
                "  crossmacro run --file ./steps.txt --json\n";
        },
        ["input"] = static () =>
        {
            return
                "Usage:\n" +
                "  crossmacro move abs|rel|rel-logical|rel-raw <x> <y> [--dry-run] [--json] [--log-level <level>]\n" +
                "  crossmacro click|down|up [current] <button> [--dry-run] [--json] [--log-level <level>]\n" +
                "  crossmacro scroll <up|down|left|right> [count] [--dry-run] [--json] [--log-level <level>]\n" +
                "  crossmacro key down|up <key> [--dry-run] [--json] [--log-level <level>]\n" +
                "  crossmacro tap <combo> [--dry-run] [--json] [--log-level <level>]\n" +
                "  crossmacro type <text> [--dry-run] [--json] [--log-level <level>]\n" +
                "  crossmacro delay <duration> | delay random <min> <max> [--dry-run] [--json] [--log-level <level>]\n\n" +
                "Description:\n" +
                "  Executes one input primitive using the same script compiler and coordinate handling as run.\n" +
                "  Use run for variables, conditions, loops, screen reads, window operations, or multiple steps.\n\n" +
                "Examples:\n" +
                "  crossmacro move abs 500 300\n" +
                "  crossmacro click left\n" +
                "  crossmacro key down Ctrl\n" +
                "  crossmacro tap Ctrl+C\n" +
                "  crossmacro type \"hello world\"\n" +
                "  crossmacro scroll down 3\n" +
                "  crossmacro move abs 500 300 --dry-run --json\n";
        },
        ["headless"] = static () =>
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
        },
        ["mcp"] = static () =>
        {
            return
                "Usage:\n" +
                "  crossmacro mcp [--restricted] [--log-level <level>]\n\n" +
                "Description:\n" +
                "  Starts the local stdio Model Context Protocol server.\n" +
                "  Standard output is reserved for MCP JSON-RPC messages; diagnostics use standard error.\n\n" +
                "Notes:\n" +
                "  MCP is persistent and does not acquire the GUI/headless runtime lock; multiple MCP sessions can run alongside GUI or headless.\n" +
                "  The session ends when the MCP client closes standard input or sends cancellation.\n";
        },
        ["clipboard"] = static () =>
        {
            return
                "Usage:\n" +
                "  crossmacro clipboard get [--json] [--log-level <level>]\n" +
                "  crossmacro clipboard set <text> [--json] [--log-level <level>]\n" +
                "  crossmacro clipboard set --file <path> [--json] [--log-level <level>]\n" +
                "  crossmacro clipboard clear [--json] [--log-level <level>]\n\n" +
                "Subcommands:\n" +
                "  get   Print current clipboard text.\n" +
                "  set   Replace clipboard text from an argument or file.\n" +
                "  clear Clear clipboard text.\n";
        },
        ["clipboard.get"] = static () =>
        {
            return "Usage:\n  crossmacro clipboard get [--json] [--log-level <level>]\n";
        },
        ["clipboard.set"] = static () =>
        {
            return
                "Usage:\n" +
                "  crossmacro clipboard set <text> [--json] [--log-level <level>]\n" +
                "  crossmacro clipboard set --file <path> [--json] [--log-level <level>]\n";
        },
        ["clipboard.clear"] = static () =>
        {
            return "Usage:\n  crossmacro clipboard clear [--json] [--log-level <level>]\n";
        },
        ["window"] = static () =>
        {
            return
                "Usage:\n" +
                "  crossmacro window active [--json] [--log-level <level>]\n" +
                "  crossmacro window list [--json] [--log-level <level>]\n" +
                "  crossmacro window search (--title <text>|--class <text>) [--json] [--log-level <level>]\n" +
                "  crossmacro window wait (--title <text>|--class <text>) [--timeout-ms <n>] [--json] [--log-level <level>]\n" +
                "  crossmacro window focus (--address <id>|--title <text>|--class <text>) [--json] [--log-level <level>]\n" +
                "  crossmacro window close (--address <id>|--title <text>) [--json] [--log-level <level>]\n" +
                "  crossmacro window move --active <x> <y> [--json] [--log-level <level>]\n" +
                "  crossmacro window resize --active <width> <height> [--json] [--log-level <level>]\n" +
                "  crossmacro window center|maximize|fullscreen|float --active [--json] [--log-level <level>]\n" +
                "  crossmacro window workspace get|switch|move-active|move-window ... [--json] [--log-level <level>]\n\n" +
                "Matches for --title and --class use case-insensitive substring matching.\n";
        },
        ["screen"] = static () =>
        {
            return
                "Usage:\n" +
                "  crossmacro screen pixel <x> <y> [--relative] [--json] [--log-level <level>]\n" +
                "  crossmacro screen wait-color <x> <y> <RRGGBB> [--timeout-ms <n>] [--json] [--log-level <level>]\n" +
                "  crossmacro screen search-color <x1> <y1> <x2> <y2> <RRGGBB> [--timeout-ms <n>] [--tolerance <0..255>] [--json] [--log-level <level>]\n" +
                "  crossmacro screen search-image <image-path> [--region <x> <y> <width> <height>] [--similarity <0..1>] [--matchmode <auto|first|best>] [--json] [--log-level <level>]\n" +
                "  crossmacro screen wait-image <image-path> [--timeout-ms <n>] [--region <x> <y> <width> <height>] [--similarity <0..1>] [--matchmode <auto|first|best>] [--json] [--log-level <level>]\n" +
                "  crossmacro screen image-click <image-path> [--timeout-ms <n>] [--button <left|right|middle>] [--region <x> <y> <width> <height>] [--similarity <0..1>] [--matchmode <auto|first|best>] [--json] [--log-level <level>]\n\n" +
                "Colors are 6-character RGB hex values. search-color bounds are end-exclusive. Wait commands use a five-second timeout unless overridden; image commands read 8-bit PNG templates.\n";
        },
        ["screenshot"] = static () =>
        {
            return
                "Usage:\n" +
                "  crossmacro screenshot --output <path> [--json] [--log-level <level>]\n" +
                "  crossmacro screenshot --clipboard [--json] [--log-level <level>]\n" +
                "  crossmacro screenshot -o <path> --clipboard --region <x> <y> <width> <height> [--json] [--log-level <level>]\n\n" +
                "Captures a PNG image using the active screen frame provider.\n";
        },
    };

    private static string GetTopicUsage(string topic)
    {
        if (IsInputCommandTopic(topic)) { return TopicUsage["input"](); }
        if (TopicUsage.TryGetValue(topic, out var usage)) { return usage(); }
        if (topic.StartsWith("profile.", StringComparison.OrdinalIgnoreCase)) { return TopicUsage["profile"](); }
        if (topic.StartsWith("text-expansion.", StringComparison.OrdinalIgnoreCase)) { return TopicUsage["text-expansion"](); }
        if (topic.StartsWith("schedule.", StringComparison.OrdinalIgnoreCase)) { return TopicUsage["schedule"](); }
        if (topic.StartsWith("shortcut.", StringComparison.OrdinalIgnoreCase)) { return TopicUsage["shortcut"](); }
        if (topic.StartsWith("trigger.", StringComparison.OrdinalIgnoreCase)) { return TopicUsage["trigger"](); }
        if (topic.StartsWith("window.", StringComparison.OrdinalIgnoreCase)) { return TopicUsage["window"](); }
        if (topic.StartsWith("screen.", StringComparison.OrdinalIgnoreCase)) { return TopicUsage["screen"](); }
        return "Usage:\n  crossmacro --help\n";
    }

    private static bool IsInputCommandTopic(string topic)
    {
        return string.Equals(topic, "move", StringComparison.OrdinalIgnoreCase)
            || string.Equals(topic, "click", StringComparison.OrdinalIgnoreCase)
            || string.Equals(topic, "down", StringComparison.OrdinalIgnoreCase)
            || string.Equals(topic, "up", StringComparison.OrdinalIgnoreCase)
            || string.Equals(topic, "scroll", StringComparison.OrdinalIgnoreCase)
            || string.Equals(topic, "key", StringComparison.OrdinalIgnoreCase)
            || string.Equals(topic, "tap", StringComparison.OrdinalIgnoreCase)
            || string.Equals(topic, "type", StringComparison.OrdinalIgnoreCase)
            || string.Equals(topic, "delay", StringComparison.OrdinalIgnoreCase);
    }

}
