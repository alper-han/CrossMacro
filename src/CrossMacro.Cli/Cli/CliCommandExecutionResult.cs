using System.Collections.Generic;

namespace CrossMacro.Cli;

public sealed class CliCommandExecutionResult
{
    private CliCommandExecutionResult()
    {
    }

    public bool Success { get; private init; }
    public int ExitCode { get; private init; }
    public string Message { get; private init; } = string.Empty;
    public object? Data { get; private init; }
    public IReadOnlyList<string> Warnings { get; private init; } = [];
    public IReadOnlyList<string> Errors { get; private init; } = [];

    public static CliCommandExecutionResult Ok(
        string message,
        object? data = null,
        IReadOnlyList<string>? warnings = null) =>
        new()
        {
            Success = true,
            ExitCode = (int)CliExitCode.Success,
            Message = message,
            Data = data,
            Warnings = warnings ?? []
        };

    public static CliCommandExecutionResult Fail(
        CliExitCode exitCode,
        string message,
        IReadOnlyList<string>? errors = null,
        IReadOnlyList<string>? warnings = null,
        object? data = null) =>
        new()
        {
            Success = false,
            ExitCode = (int)exitCode,
            Message = message,
            Errors = errors ?? [],
            Warnings = warnings ?? [],
            Data = data
        };
}
