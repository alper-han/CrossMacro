using System;
using System.Diagnostics;

namespace CrossMacro.Platform.Linux.Services.QuickSetup;

internal sealed class DirectPkexecHostCommandLauncher : IPrivilegedHostCommandLauncher
{
    private readonly Func<string, bool> _commandExists;

    public DirectPkexecHostCommandLauncher()
        : this(HostCommandProbe.CommandExists)
    {
    }

    public DirectPkexecHostCommandLauncher(Func<string, bool> commandExists)
    {
        _commandExists = commandExists ?? throw new ArgumentNullException(nameof(commandExists));
    }

    public bool IsAvailable(out string failureMessage)
    {
        if (_commandExists("pkexec"))
        {
            failureMessage = string.Empty;
            return true;
        }

        failureMessage = "pkexec is missing on host. Install polkit and retry.";
        return false;
    }

    public ProcessStartInfo CreateStartInfo(string hostScript, LinuxQuickSetupIdentity identity)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "pkexec",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        startInfo.ArgumentList.Add("sh");
        startInfo.ArgumentList.Add("-c");
        startInfo.ArgumentList.Add(hostScript);
        startInfo.ArgumentList.Add("crossmacro-appimage-session-helper");
        startInfo.ArgumentList.Add(identity.Specifier);

        return startInfo;
    }
}
