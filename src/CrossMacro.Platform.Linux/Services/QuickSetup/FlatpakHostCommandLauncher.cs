using System;
using System.Diagnostics;

namespace CrossMacro.Platform.Linux.Services.QuickSetup;

internal sealed class FlatpakHostCommandLauncher : IPrivilegedHostCommandLauncher
{
    private readonly Func<string, bool> _commandExistsInSandbox;
    private readonly Func<string, bool> _commandExistsOnHost;

    public FlatpakHostCommandLauncher()
        : this(HostCommandProbe.CommandExists, HostCommandProbe.CommandExistsOnHostViaFlatpakSpawn)
    {
    }

    public FlatpakHostCommandLauncher(Func<string, bool> commandExistsInSandbox)
        : this(commandExistsInSandbox, HostCommandProbe.CommandExistsOnHostViaFlatpakSpawn)
    {
    }

    public FlatpakHostCommandLauncher(
        Func<string, bool> commandExistsInSandbox,
        Func<string, bool> commandExistsOnHost)
    {
        _commandExistsInSandbox = commandExistsInSandbox ?? throw new ArgumentNullException(nameof(commandExistsInSandbox));
        _commandExistsOnHost = commandExistsOnHost ?? throw new ArgumentNullException(nameof(commandExistsOnHost));
    }

    public bool IsAvailable(out string failureMessage)
    {
        if (!_commandExistsInSandbox("flatpak-spawn"))
        {
            failureMessage = "flatpak-spawn is missing in Flatpak environment.";
            return false;
        }

        if (!_commandExistsOnHost("pkexec"))
        {
            failureMessage = "pkexec is missing on host. Install polkit and retry.";
            return false;
        }

        failureMessage = string.Empty;
        return true;
    }

    public ProcessStartInfo CreateStartInfo(string hostScript, LinuxQuickSetupIdentity identity)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "flatpak-spawn",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        startInfo.ArgumentList.Add("--host");
        startInfo.ArgumentList.Add("pkexec");
        startInfo.ArgumentList.Add("sh");
        startInfo.ArgumentList.Add("-c");
        startInfo.ArgumentList.Add(hostScript);
        startInfo.ArgumentList.Add("crossmacro-session-helper");
        startInfo.ArgumentList.Add(identity.Specifier);

        return startInfo;
    }
}
