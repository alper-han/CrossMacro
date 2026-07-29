namespace CrossMacro.Infrastructure.Services;

public sealed record ShellCommandResult(int ExitCode, string StandardOutput, string StandardError);
