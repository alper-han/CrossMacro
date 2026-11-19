namespace CrossMacro.Cli;

/// <summary>
/// The one definition of every command usage line. Parser errors, per-topic help and the
/// top-level help all read from here so the texts cannot drift apart.
/// </summary>
internal static class CliUsage
{
    private const string Common = "[--json] [--log-level <level>]";
    private const string DryRunCommon = "[--dry-run] " + Common;
    private const string ScheduleForm = "[--interval <duration>|--at <datetime>|--weekly <days> --time <HH:mm>] [--speed <value>] [--enabled <bool>] " + Common;
    private const string ShortcutOptions = "[--speed <value>] [--loop] [--repeat <n>] [--repeat-delay-ms <ms>] [--random-repeat-delay <min-ms> <max-ms>] [--run-while-held] [--press-release] [--release-macro <path>] [--normal] [--window-rule <field> <match-mode> <value>]";
    private const string TriggerOptions = "[--profile <profile>] [--macro <path>] [--fire-mode <mode>] [--cooldown-ms <ms>] [--debounce-ms <ms>] [--enabled <bool>] " + Common;
    private const string RunOptions = "[--file <steps-file>] [--asset <name> <png-path>] [--speed <value>] [--countdown <sec>] [--timeout <sec>] [--dry-run] " + Common;
    private const string TextExpansionProfile = "[--profile <name-or-id>] " + Common;
    private const string ImageSearchOptions = "[--similarity <0..1>] [--matchmode <auto|first|best>] " + Common;

    public const string Gui = "crossmacro [--start-minimized]";

    public const string MacroValidate = "crossmacro macro validate <macro-file> " + Common;
    public const string MacroInfo = "crossmacro macro info <macro-file> " + Common;
    public const string Play = "crossmacro play <macro-file> [--speed <value>] [--motion-mode precision|strict-speed] [--motion-rate <reports/s>] [--precision-motion-rate <reports/s>] [--motion-error-px <px>] [--loop] [--repeat <n>] [--repeat-delay-ms <ms>] [--countdown <sec>] [--timeout <sec>] [--dry-run] " + Common;
    public const string Doctor = "crossmacro doctor [--verbose] " + Common;
    public const string Setup = "crossmacro setup " + Common;

    public const string SettingsGet = "crossmacro settings get [<key>|--all] " + Common;
    public const string SettingsSet = "crossmacro settings set <key> <value> " + Common;
    public const string SettingsListKeys = "crossmacro settings list-keys " + Common;
    public const string SettingsReset = "crossmacro settings reset <key> " + Common;

    public const string ProfileList = "crossmacro profile list " + Common;
    public const string ProfileCurrent = "crossmacro profile current " + Common;
    public const string ProfileCreate = "crossmacro profile create <name> " + Common;
    public const string ProfileSwitch = "crossmacro profile switch <name-or-id> " + Common;
    public const string ProfileRename = "crossmacro profile rename <name-or-id> <new-name> " + Common;
    public const string ProfileDelete = "crossmacro profile delete <name-or-id> --force " + Common;
    public const string ProfileGeneric = "crossmacro profile [subcommand] " + Common;

    public const string TextExpansionList = "crossmacro text-expansion list " + TextExpansionProfile;
    public const string TextExpansionAdd = "crossmacro text-expansion add <trigger> <replacement> [--method CtrlV|CtrlShiftV|ShiftInsert] [--insertion-mode Paste|DirectTyping] [--direct-typing-method FastBatch|CompatibleKeyByKey] " + TextExpansionProfile;
    public const string TextExpansionTrigger = "crossmacro text-expansion remove|enable|disable|test <trigger> " + TextExpansionProfile;

    public const string ScheduleList = "crossmacro schedule list " + Common;
    public const string ScheduleRun = "crossmacro schedule run <task-id> " + Common;
    public const string ScheduleAdd = "crossmacro schedule add --name <name> --macro <path> " + ScheduleForm;
    public const string ScheduleEdit = "crossmacro schedule edit <task-id> [--name <name>] [--macro <path>] " + ScheduleForm;
    public const string ScheduleTask = "crossmacro schedule remove|enable|disable|next <task-id> " + Common;

    public const string ShortcutList = "crossmacro shortcut list " + Common;
    public const string ShortcutRun = "crossmacro shortcut run <task-id> " + Common;
    public const string ShortcutAdd = "crossmacro shortcut add --name <name> --macro <path> --hotkey <keys> [--hotkey <keys> ...] " + ShortcutOptions + " [--enabled <bool>] " + Common;
    public const string ShortcutEdit = "crossmacro shortcut edit <task-id> [--name <name>] [--macro <path>] [--hotkey <keys> ...] " + ShortcutOptions + " [--clear-window-rules] [--enabled <bool>] " + Common;
    public const string ShortcutTask = "crossmacro shortcut remove|enable|disable <task-id> " + Common;
    public const string ShortcutBind = "crossmacro shortcut bind <task-id> --hotkey <keys> [--hotkey <keys> ...] " + Common;

    public const string TriggerList = "crossmacro trigger list " + Common;
    public const string TriggerAdd = "crossmacro trigger add --name <name> --field <field> --match-mode <mode> --value <value> --action <action> " + TriggerOptions;
    public const string TriggerEdit = "crossmacro trigger edit <task-id> [--name <name>] [--field <field>] [--match-mode <mode>] [--value <value>] [--action <action>] " + TriggerOptions;
    public const string TriggerTask = "crossmacro trigger remove|enable|disable <task-id> " + Common;

    public const string Record = "crossmacro record (--output|-o) <macro-file> [--mouse <true|false>] [--keyboard <true|false>] [--mode <auto|absolute|relative>] [--skip-initial-zero] [--duration <sec>] " + Common;
    public const string RunSteps = "crossmacro run --step <step> [--step <step> ...] " + RunOptions;
    public const string RunCommands = "crossmacro run <step-command> [<step-command> ...] " + RunOptions;

    public const string MoveInput = "crossmacro move abs|rel|rel-logical|rel-raw <x> <y> " + DryRunCommon;
    public const string ButtonInput = "crossmacro click|down|up [current] <button> " + DryRunCommon;
    public const string ScrollInput = "crossmacro scroll <up|down|left|right> [count] " + DryRunCommon;
    public const string KeyInput = "crossmacro key down|up <key> " + DryRunCommon;
    public const string TapInput = "crossmacro tap <combo> " + DryRunCommon;
    public const string TypeInput = "crossmacro type <text> " + DryRunCommon;
    public const string DelayInput = "crossmacro delay <duration> | delay random <min> <max> " + DryRunCommon;

    public const string ClipboardGet = "crossmacro clipboard get " + Common;
    public const string ClipboardSetText = "crossmacro clipboard set <text> " + Common;
    public const string ClipboardSetFile = "crossmacro clipboard set --file <path> " + Common;
    public const string ClipboardClear = "crossmacro clipboard clear " + Common;

    public const string WindowActive = "crossmacro window active " + Common;
    public const string WindowList = "crossmacro window list " + Common;
    public const string WindowSearch = "crossmacro window search (--title <text>|--class <text>) " + Common;
    public const string WindowWait = "crossmacro window wait (--title <text>|--class <text>) [--timeout-ms <n>] " + Common;
    public const string WindowFocus = "crossmacro window focus (--address <id>|--title <text>|--class <text>) " + Common;
    public const string WindowClose = "crossmacro window close (--address <id>|--title <text>) " + Common;
    public const string WindowMove = "crossmacro window move --active <x> <y> " + Common;
    public const string WindowResize = "crossmacro window resize --active <width> <height> " + Common;
    public const string WindowPlacement = "crossmacro window center|maximize|fullscreen|float --active " + Common;
    public const string WindowWorkspace = "crossmacro window workspace get|switch|move-active|move-window ... " + Common;
    public const string WindowSummary = "crossmacro window active|list|search|wait|focus|close|move|resize|center|maximize|fullscreen|float|workspace ... " + Common;

    public const string ScreenPixel = "crossmacro screen pixel <x> <y> [--relative] " + Common;
    public const string ScreenWaitColor = "crossmacro screen wait-color <x> <y> <RRGGBB> [--timeout-ms <n>] " + Common;
    public const string ScreenSearchColor = "crossmacro screen search-color <x1> <y1> <x2> <y2> <RRGGBB> [--timeout-ms <n>] [--tolerance <0..255>] " + Common;
    public const string ScreenSearchImage = "crossmacro screen search-image <image-path> [--region <x> <y> <width> <height>] " + ImageSearchOptions;
    public const string ScreenWaitImage = "crossmacro screen wait-image <image-path> [--timeout-ms <n>] [--region <x> <y> <width> <height>] " + ImageSearchOptions;
    public const string ScreenImageClick = "crossmacro screen image-click <image-path> [--timeout-ms <n>] [--button <left|right|middle>] [--region <x> <y> <width> <height>] " + ImageSearchOptions;
    public const string ScreenSummary = "crossmacro screen pixel|wait-color|search-color|search-image|wait-image|image-click ... " + Common;

    public const string ScreenshotOutput = "crossmacro screenshot --output <path> " + Common;
    public const string ScreenshotClipboard = "crossmacro screenshot --clipboard " + Common;
    public const string ScreenshotRegion = "crossmacro screenshot --output <path> --region <x> <y> <width> <height> " + Common;
    public const string ScreenshotSummary = "crossmacro screenshot ((--output|-o) <path>|--clipboard) [--region <x> <y> <width> <height>] " + Common;

    public const string Headless = "crossmacro headless " + Common;
    public const string HeadlessFlag = "crossmacro --headless " + Common;
    public const string Mcp = "crossmacro mcp [--restricted] [--log-level <level>]";

    public const string ProfileSummary = "crossmacro profile list|current|create|switch|rename|delete ... " + Common;
    public const string TextExpansionSummary = "crossmacro text-expansion list|add|remove|enable|disable|test ... " + Common;
    public const string ScheduleSummary = "crossmacro schedule list|run|add|edit|remove|enable|disable|next ... " + Common;
    public const string ShortcutSummary = "crossmacro shortcut list|run|add|edit|remove|enable|disable|bind ... " + Common;
    public const string TriggerSummary = "crossmacro trigger list|add|edit|remove|enable|disable ... " + Common;

    public static readonly string[] InputLines =
    [
        MoveInput,
        ButtonInput,
        ScrollInput,
        KeyInput,
        TapInput,
        TypeInput,
        DelayInput,
    ];

    public static readonly string[] ScreenLines =
    [
        ScreenPixel,
        ScreenWaitColor,
        ScreenSearchColor,
        ScreenSearchImage,
        ScreenWaitImage,
        ScreenImageClick,
    ];

    public static readonly string[] ClipboardLines =
    [
        ClipboardGet,
        ClipboardSetText,
        ClipboardSetFile,
        ClipboardClear,
    ];

    public static readonly string[] WindowLines =
    [
        WindowActive,
        WindowList,
        WindowSearch,
        WindowWait,
        WindowFocus,
        WindowClose,
        WindowMove,
        WindowResize,
        WindowPlacement,
        WindowWorkspace,
    ];

    public static readonly string[] ProfileLines =
    [
        ProfileList,
        ProfileCurrent,
        ProfileCreate,
        ProfileSwitch,
        ProfileRename,
        ProfileDelete,
    ];

    public static readonly string[] TextExpansionLines =
    [
        TextExpansionList,
        TextExpansionAdd,
        TextExpansionTrigger,
    ];

    public static readonly string[] ScheduleLines =
    [
        ScheduleList,
        ScheduleRun,
        ScheduleAdd,
        ScheduleEdit,
        ScheduleTask,
    ];

    public static readonly string[] ShortcutLines =
    [
        ShortcutList,
        ShortcutRun,
        ShortcutAdd,
        ShortcutEdit,
        ShortcutTask,
        ShortcutBind,
    ];

    public static readonly string[] TriggerLines =
    [
        TriggerList,
        TriggerAdd,
        TriggerEdit,
        TriggerTask,
    ];

    public static readonly string[] SettingsLines =
    [
        SettingsGet,
        SettingsSet,
        SettingsListKeys,
        SettingsReset,
    ];

    public static readonly string[] RunLines = [RunSteps, RunCommands];

    public static readonly string[] ScreenshotLines = [ScreenshotOutput, ScreenshotClipboard, ScreenshotRegion];

    /// <summary>The usage block of one help topic: a "Usage:" header followed by the indented lines.</summary>
    public static string Block(params string[] lines) =>
        "Usage:\n" + string.Concat(lines.Select(static line => "  " + line + "\n"));
}
