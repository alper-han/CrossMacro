namespace CrossMacro.Platform.Linux.Services.QuickSetup;

internal static class HostPrivilegeCommand
{
    internal enum Kind
    {
        None,
        Pkexec,
        Run0,
    }

    internal sealed record Selection(Kind Kind, string Path);

    public static async ValueTask<(Selection? Command, string FailureMessage)> SelectAsync(
        Func<string, CancellationToken, ValueTask<string?>> resolveCommand,
        Func<string, CancellationToken, ValueTask<bool>> pkexecIsUsable,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(resolveCommand);
        ArgumentNullException.ThrowIfNull(pkexecIsUsable);

        var pkexecPath = await resolveCommand("pkexec", cancellationToken).ConfigureAwait(false);
        if (pkexecPath is not null && await pkexecIsUsable(pkexecPath, cancellationToken).ConfigureAwait(false))
        {
            return (new Selection(Kind.Pkexec, pkexecPath), string.Empty);
        }

        var run0Path = await resolveCommand("run0", cancellationToken).ConfigureAwait(false);
        if (run0Path is not null)
        {
            // Keep the resolved invocation name: run0 may be a symlink to systemd-run.
            return (new Selection(Kind.Run0, run0Path), string.Empty);
        }

        return (null, pkexecPath is not null
            ? "pkexec cannot elevate privileges in this execution environment, and systemd run0 is unavailable. Use a host environment that permits setuid-root pkexec, or systemd 256+ with run0 and host system-manager access."
            : "Neither executable pkexec nor systemd run0 is available in the host execution environment. Install polkit or systemd 256+ with run0 and host system-manager access.");
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
            // The host service cannot inherit an AppImage/FHS/Flatpak-only working directory.
            // Redirected stdout/stderr select direct stdio already on systemd 256.
            startInfo.ArgumentList.Add("--chdir=/");
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
}
