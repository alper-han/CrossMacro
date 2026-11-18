namespace CrossMacro.Infrastructure.Services.Shell;

public sealed record ShellCommandRequest(string Command, string? StandardInput = null, int OutputLimitChars = 65_536)
{
    // The generated ToString would print the command and its standard input, which may contain secrets.
    public override string ToString() =>
        $"ShellCommandRequest {{ Command = <{Command.Length} chars>, StandardInput = {(StandardInput is null ? "none" : $"<{StandardInput.Length} chars>")}, OutputLimitChars = {OutputLimitChars} }}";
}
