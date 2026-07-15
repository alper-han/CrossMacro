namespace CrossMacro.Infrastructure.Services;

public sealed record class ShellCommandResult(int ExitCode, string StandardOutput, string StandardError);
