
namespace CrossMacro.Platform.Linux.Services.QuickSetup;

internal sealed class DirectPkexecHostCommandLauncher(Func<string, CancellationToken, ValueTask<bool>> commandExists) : IPrivilegedHostCommandLauncher
{
    private readonly Func<string, CancellationToken, ValueTask<bool>> _commandExists = commandExists ?? throw new ArgumentNullException(nameof(commandExists));

    public DirectPkexecHostCommandLauncher()
        : this(HostCommandProbe.CommandExistsAsync) { /* Empty */ }

    public async ValueTask<(bool IsAvailable, string FailureMessage)> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        if (await _commandExists("pkexec", cancellationToken).ConfigureAwait(false))
        {
            return (true, string.Empty);
        }

        return (false, "pkexec is missing on host. Install polkit and retry.");
    }

    public ProcessStartInfo CreateStartInfo(string hostScript, LinuxQuickSetupIdentity identity)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "pkexec",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        startInfo.ArgumentList.Add("sh");
        startInfo.ArgumentList.Add("-c");
        startInfo.ArgumentList.Add(hostScript);
        startInfo.ArgumentList.Add("crossmacro-appimage-session-helper");
        startInfo.ArgumentList.Add(identity.Specifier);

        return startInfo;
    }
}
