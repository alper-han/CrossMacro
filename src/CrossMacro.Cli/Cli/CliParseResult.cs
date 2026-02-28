namespace CrossMacro.Cli;

public sealed class CliParseResult
{
    private CliParseResult()
    {
    }

    public bool ShouldStartGui { get; private init; }
    public bool IsSuccess { get; private init; }
    public bool ShowHelp { get; private init; }
    public bool ShowVersion { get; private init; }
    public string? HelpTopic { get; private init; }
    public string? ErrorMessage { get; private init; }
    public CliCommandOptions? Options { get; private init; }

    public static CliParseResult Gui() => new() { ShouldStartGui = true, IsSuccess = true };

    public static CliParseResult Help(string? topic = null) => new()
    {
        ShowHelp = true,
        IsSuccess = true,
        HelpTopic = topic
    };

    public static CliParseResult Version() => new()
    {
        ShowVersion = true,
        IsSuccess = true
    };

    public static CliParseResult Success(CliCommandOptions options) => new()
    {
        IsSuccess = true,
        Options = options
    };

    public static CliParseResult Error(string message) => new()
    {
        IsSuccess = false,
        ErrorMessage = message
    };
}
