namespace CrossMacro.Cli.Services.Settings;

public sealed class SettingsCommandResult
{
    public required bool Success { get; init; }

    public required CliExitCode ExitCode { get; init; }

    public required string Message { get; init; }

    public object? Data { get; init; }

    public IReadOnlyList<string> Errors { get; init; } = [];
}
