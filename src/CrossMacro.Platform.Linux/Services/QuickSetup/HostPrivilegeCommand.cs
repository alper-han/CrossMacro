namespace CrossMacro.Platform.Linux.Services.QuickSetup;

internal static class HostPrivilegeCommand
{
    internal enum Kind
    {
        None,
        Pkexec,
        Run0,
    }

    public static async ValueTask<(Kind Kind, string FailureMessage)> SelectAsync(
        Func<string, CancellationToken, ValueTask<bool>> commandExists,
        Func<CancellationToken, ValueTask<bool>> pkexecIsUsable,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(commandExists);
        ArgumentNullException.ThrowIfNull(pkexecIsUsable);

        var hasPkexec = await commandExists("pkexec", cancellationToken).ConfigureAwait(false);
        if (hasPkexec && await pkexecIsUsable(cancellationToken).ConfigureAwait(false))
        {
            return (Kind.Pkexec, string.Empty);
        }

        if (await commandExists("run0", cancellationToken).ConfigureAwait(false))
        {
            return (Kind.Run0, string.Empty);
        }

        return (Kind.None, hasPkexec
            ? "pkexec is installed but its setuid-root wrapper is disabled, and systemd run0 is unavailable. Enable pkexec or install systemd 256+ and retry."
            : "Neither pkexec nor systemd run0 is available on the host. Install polkit or systemd 256+ and retry.");
    }

    public static void AddArguments(
        ProcessStartInfo startInfo,
        Kind commandKind,
        string hostScript,
        LinuxQuickSetupIdentity identity,
        string helperName)
    {
        ArgumentNullException.ThrowIfNull(startInfo);
        ArgumentException.ThrowIfNullOrWhiteSpace(hostScript);
        ArgumentException.ThrowIfNullOrWhiteSpace(helperName);

        if (commandKind is Kind.Run0)
        {
            startInfo.ArgumentList.Add("--description=CrossMacro temporary input setup");
        }
        else if (commandKind is not Kind.Pkexec)
        {
            throw new InvalidOperationException("No host privilege command was selected.");
        }

        startInfo.ArgumentList.Add("/bin/sh");
        startInfo.ArgumentList.Add("-c");
        startInfo.ArgumentList.Add(hostScript);
        startInfo.ArgumentList.Add(helperName);
        startInfo.ArgumentList.Add(identity.Specifier);
    }

    public static string GetFileName(Kind commandKind) => commandKind switch
    {
        Kind.Pkexec => "pkexec",
        Kind.Run0 => "run0",
        Kind.None => throw new InvalidOperationException("No host privilege command was selected."),
        _ => throw new InvalidOperationException("No host privilege command was selected."),
    };
}
