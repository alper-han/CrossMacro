using System.Collections.Generic;

namespace CrossMacro.Cli.Services;

public sealed class HeadlessRuntimeResult
{
    public bool Success { get; init; }
    public CliExitCode ExitCode { get; init; }
    public string Message { get; init; } = string.Empty;
    public IReadOnlyList<string> Warnings { get; init; } = [];
    public IReadOnlyList<string> Errors { get; init; } = [];
    public object? Data { get; init; }
}
