using System.Collections.Generic;

namespace CrossMacro.Cli.Services;

public sealed class MacroExecutionResult
{
    public required bool Success { get; init; }

    public required CliExitCode ExitCode { get; init; }

    public required string Message { get; init; }

    public IReadOnlyList<string> Errors { get; init; } = [];

    public IReadOnlyList<string> Warnings { get; init; } = [];

    public object? Data { get; init; }
}
