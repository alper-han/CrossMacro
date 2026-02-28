using System.Collections.Generic;

namespace CrossMacro.Cli.Services;

public sealed class CliPreflightResult
{
    private CliPreflightResult()
    {
    }

    public bool Success { get; private init; }
    public CliExitCode ExitCode { get; private init; }
    public string Message { get; private init; } = string.Empty;
    public IReadOnlyList<string> Errors { get; private init; } = [];
    public IReadOnlyList<string> Warnings { get; private init; } = [];

    public static CliPreflightResult Ok(IReadOnlyList<string>? warnings = null)
    {
        return new CliPreflightResult
        {
            Success = true,
            ExitCode = CliExitCode.Success,
            Warnings = warnings ?? []
        };
    }

    public static CliPreflightResult Fail(
        CliExitCode exitCode,
        string message,
        IReadOnlyList<string>? errors = null,
        IReadOnlyList<string>? warnings = null)
    {
        return new CliPreflightResult
        {
            Success = false,
            ExitCode = exitCode,
            Message = message,
            Errors = errors ?? [],
            Warnings = warnings ?? []
        };
    }
}
