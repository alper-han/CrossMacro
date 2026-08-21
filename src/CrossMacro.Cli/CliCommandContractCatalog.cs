namespace CrossMacro.Cli;

/// <summary>
/// Canonical, machine-readable CLI command vocabulary consumed by outer adapters.
/// </summary>
public static class CliCommandContractCatalog
{
    private static readonly CliOptionContract[] CommonOptions =
    [
        BooleanOption("--json"),
        EnumOption("--log-level", ["Verbose", "Debug", "Information", "Warning", "Error", "Fatal"]),
    ];

    public static IReadOnlyList<CliCommandContract> RootCommands { get; } =
        Array.AsReadOnly(
            new (string CommandToken, string[] Aliases, string[] Subcommands, CliOptionContract[] Options)[]
            {
                new("macro", [], ["validate", "info"], [.. CommonOptions]),
                new("play", [], [],
                [
                    .. CommonOptions,
                    NumberOption("--speed", "1"),
                    EnumOption("--motion-mode", ["precision", "strict-speed", "strict"], "precision"),
                    IntegerOption("--motion-rate"),
                    IntegerOption("--precision-motion-rate"),
                    NumberOption("--motion-error-px"),
                    BooleanOption("--loop"),
                    IntegerOption("--repeat", "1"),
                    IntegerOption("--repeat-delay-ms", "0"),
                    IntegerOption("--countdown", "0"),
                    IntegerOption("--timeout", "0"),
                    BooleanOption("--dry-run"),
                ]),
                new("doctor", [], [], [.. CommonOptions, BooleanOption("--verbose")]),
                new("setup", ["quick-setup"], [], [.. CommonOptions]),
                new("settings", [], ["get", "set", "list-keys", "reset"], [.. CommonOptions, BooleanOption("--all")]),
                new("profile", [], ["list", "current", "create", "switch", "rename", "delete"], [.. CommonOptions, BooleanOption("--force")]),
                new("text-expansion", ["text"], ["list", "add", "remove", "enable", "disable", "test"],
                [
                    .. CommonOptions,
                    StringOption("--profile"),
                    EnumOption("--method", ["CtrlV", "CtrlShiftV", "ShiftInsert"], "CtrlV"),
                    EnumOption("--insertion-mode", ["Paste", "DirectTyping"], "Paste"),
                    EnumOption("--direct-typing-method", ["FastBatch", "CompatibleKeyByKey"], "FastBatch"),
                ]),
                new("schedule", [], ["list", "run", "add", "edit", "remove", "enable", "disable", "next"],
                [
                    .. CommonOptions,
                    StringOption("--name"),
                    PathOption("--macro"),
                    StringOption("--interval"),
                    StringOption("--at"),
                    StringOption("--weekly"),
                    StringOption("--time"),
                    NumberOption("--speed"),
                    BooleanValueOption("--enabled"),
                ]),
                new("shortcut", [], ["list", "run", "add", "edit", "remove", "enable", "disable", "bind"],
                [
                    .. CommonOptions,
                    StringOption("--name"),
                    PathOption("--macro"),
                    StringOption("--hotkey"),
                    NumberOption("--speed"),
                    BooleanOption("--loop"),
                    IntegerOption("--repeat"),
                    IntegerOption("--repeat-delay-ms"),
                    CompositeOption("--random-repeat-delay"),
                    BooleanOption("--run-while-held"),
                    CompositeOption("--window-rule"),
                    BooleanOption("--clear-window-rules"),
                    BooleanValueOption("--enabled"),
                ]),
                new("trigger", [], ["list", "add", "edit", "remove", "enable", "disable"],
                [
                    .. CommonOptions,
                    StringOption("--name"),
                    EnumOption("--field", ["WindowClass", "WindowTitle", "Workspace", "ProcessName", "None"]),
                    EnumOption("--match-mode", ["Equals", "Contains", "Regex"]),
                    StringOption("--value"),
                    EnumOption("--action", ["SwitchProfile", "RunMacro"]),
                    StringOption("--profile"),
                    PathOption("--macro"),
                    EnumOption("--fire-mode", ["OnceOnChange", "EveryMatch", "OnEnter", "OnExit"]),
                    IntegerOption("--cooldown-ms"),
                    IntegerOption("--debounce-ms"),
                    BooleanValueOption("--enabled"),
                ]),
                new("record", [], [],
                [
                    .. CommonOptions,
                    PathOption("--output", requiresValue: true),
                    PathOption("-o", requiresValue: true),
                    BooleanValueOption("--mouse", "true"),
                    BooleanValueOption("--keyboard", "true"),
                    EnumOption("--mode", ["auto", "absolute", "relative", "abs", "rel"], "auto"),
                    BooleanOption("--skip-initial-zero"),
                    IntegerOption("--duration", "0"),
                ]),
                new("run", [], [],
                [
                    .. CommonOptions,
                    StringOption("--step"),
                    PathOption("--file"),
                    CompositeOption("--asset"),
                    NumberOption("--speed", "1"),
                    IntegerOption("--countdown", "0"),
                    IntegerOption("--timeout", "0"),
                    BooleanOption("--dry-run"),
                ]),
                new("move", [], [], [.. CommonOptions, BooleanOption("--dry-run")]),
                new("click", [], [], [.. CommonOptions, BooleanOption("--dry-run")]),
                new("down", [], [], [.. CommonOptions, BooleanOption("--dry-run")]),
                new("up", [], [], [.. CommonOptions, BooleanOption("--dry-run")]),
                new("scroll", [], [], [.. CommonOptions, BooleanOption("--dry-run")]),
                new("key", [], [], [.. CommonOptions, BooleanOption("--dry-run")]),
                new("tap", [], [], [.. CommonOptions, BooleanOption("--dry-run")]),
                new("type", [], [], [.. CommonOptions, BooleanOption("--dry-run")]),
                new("delay", [], [], [.. CommonOptions, BooleanOption("--dry-run")]),
                new("clipboard", [], ["get", "set", "clear"], [.. CommonOptions, PathOption("--file")]),
                new("window", [], ["active", "list", "search", "wait", "focus", "close", "move", "resize", "center", "maximize", "fullscreen", "float", "workspace"],
                [
                    .. CommonOptions,
                    CompositeOption("--active"),
                    StringOption("--address"),
                    StringOption("--title"),
                    StringOption("--class"),
                    IntegerOption("--timeout-ms"),
                ]),
                new("screen", [], ["pixel", "wait-color", "search-color", "search-image", "wait-image", "image-click"],
                [
                    .. CommonOptions,
                    BooleanOption("--relative"),
                    IntegerOption("--timeout-ms"),
                    IntegerOption("--tolerance", "0"),
                    CompositeOption("--region"),
                    NumberOption("--similarity", "0.95"),
                    EnumOption("--matchmode", ["auto", "first", "best"], "auto"),
                    EnumOption("--button", ["left", "right", "middle"], "left"),
                ]),
                new("screenshot", [], [],
                [.. CommonOptions, PathOption("--output"), PathOption("-o"), BooleanOption("--clipboard"), CompositeOption("--region")]),
                new("headless", ["--headless"], [], [.. CommonOptions]),
                new("mcp", [], [], [EnumOption("--log-level", ["Verbose", "Debug", "Information", "Warning", "Error", "Fatal"]), BooleanOption("--restricted")]),
            }
                .Select(static contract => new CliCommandContract(
                    contract.CommandToken,
                    Array.AsReadOnly(contract.Aliases.ToArray()),
                    Array.AsReadOnly(contract.Subcommands.ToArray()),
                    Array.AsReadOnly(contract.Options.ToArray())))
                .ToArray());

    private static CliOptionContract BooleanOption(string token, string defaultValue = "false") =>
        new(token, CliOptionValueKind.Boolean, requiresValue: false, defaultValue: defaultValue);

    private static CliOptionContract BooleanValueOption(string token, string? defaultValue = null) =>
        new(token, CliOptionValueKind.Boolean, requiresValue: true, defaultValue: defaultValue);

    private static CliOptionContract IntegerOption(string token, string? defaultValue = null) =>
        new(token, CliOptionValueKind.WholeNumber, requiresValue: true, defaultValue: defaultValue);

    private static CliOptionContract NumberOption(string token, string? defaultValue = null) =>
        new(token, CliOptionValueKind.DecimalNumber, requiresValue: true, defaultValue: defaultValue);

    private static CliOptionContract StringOption(string token) =>
        new(token, CliOptionValueKind.Text, requiresValue: true);

    private static CliOptionContract PathOption(string token, bool requiresValue = true) =>
        new(token, CliOptionValueKind.Path, requiresValue: requiresValue);

    private static CliOptionContract CompositeOption(string token) =>
        new(token, CliOptionValueKind.Composite, requiresValue: true);

    private static CliOptionContract EnumOption(string token, IReadOnlyList<string> allowedValues, string? defaultValue = null) =>
        new(token, CliOptionValueKind.Enum, requiresValue: true, defaultValue: defaultValue, allowedValues: allowedValues);
}
