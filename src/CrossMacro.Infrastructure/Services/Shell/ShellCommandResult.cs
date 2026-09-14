namespace CrossMacro.Infrastructure.Services.Shell;

public sealed record ShellCommandResult(int ExitCode, string StandardOutput, string StandardError);
