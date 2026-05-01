namespace CrossMacro.Cli;

public sealed class CliParseResult
{
    public enum ParseResultKind
    {
        Gui,
        Help,
        Version,
        Success,
        Error
    }

    private static readonly IReadOnlyList<string> EmptyErrorDetails = [];

    private CliParseResult()
    {
    }

    public ParseResultKind Kind { get; private init; }
    public string? HelpTopic { get; private init; }
    public string? ErrorMessage { get; private init; }
    public IReadOnlyList<string> ErrorDetails { get; private init; } = EmptyErrorDetails;
    public CliCommandOptions? Options { get; private init; }
    public bool PrefersJsonOutput { get; private init; }
    public bool ShowTopLevelUsageInTextMode { get; private init; }

    public bool ShouldStartGui => Kind == ParseResultKind.Gui;
    public bool IsSuccess => Kind is ParseResultKind.Gui or ParseResultKind.Help or ParseResultKind.Version or ParseResultKind.Success;
    public bool ShowHelp => Kind == ParseResultKind.Help;
    public bool ShowVersion => Kind == ParseResultKind.Version;

    public static CliParseResult Gui() => new() { Kind = ParseResultKind.Gui };

    public static CliParseResult Help(string? topic = null) => new()
    {
        Kind = ParseResultKind.Help,
        HelpTopic = topic
    };

    public static CliParseResult Version() => new()
    {
        Kind = ParseResultKind.Version
    };

    public static CliParseResult Success(CliCommandOptions options) => new()
    {
        Kind = ParseResultKind.Success,
        Options = options,
        PrefersJsonOutput = options.JsonOutput
    };

    public static CliParseResult Error(
        string message,
        IReadOnlyList<string>? errorDetails = null,
        bool prefersJsonOutput = false,
        bool showTopLevelUsageInTextMode = false) => new()
    {
        Kind = ParseResultKind.Error,
        ErrorMessage = message,
        ErrorDetails = errorDetails ?? EmptyErrorDetails,
        PrefersJsonOutput = prefersJsonOutput,
        ShowTopLevelUsageInTextMode = showTopLevelUsageInTextMode
    };

}
