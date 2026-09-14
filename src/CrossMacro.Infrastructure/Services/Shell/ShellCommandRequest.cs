namespace CrossMacro.Infrastructure.Services.Shell;

public sealed record ShellCommandRequest(string Command, string? StandardInput = null, int OutputLimitChars = 65_536);
