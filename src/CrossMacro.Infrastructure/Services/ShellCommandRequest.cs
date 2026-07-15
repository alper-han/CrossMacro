namespace CrossMacro.Infrastructure.Services;

public sealed record class ShellCommandRequest(string Command, string? StandardInput = null, int OutputLimitChars = 65_536);
