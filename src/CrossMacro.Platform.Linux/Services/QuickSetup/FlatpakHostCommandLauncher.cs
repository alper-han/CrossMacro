
namespace CrossMacro.Platform.Linux.Services.QuickSetup;

internal sealed class FlatpakHostCommandLauncher(
    Func<string, CancellationToken, ValueTask<bool>> commandExistsInSandbox,
    Func<string, CancellationToken, ValueTask<bool>> commandExistsOnHost) : IPrivilegedHostCommandLauncher
{
    private readonly Func<string, CancellationToken, ValueTask<bool>> _commandExistsInSandbox = commandExistsInSandbox ?? throw new ArgumentNullException(nameof(commandExistsInSandbox));
    private readonly Func<string, CancellationToken, ValueTask<bool>> _commandExistsOnHost = commandExistsOnHost ?? throw new ArgumentNullException(nameof(commandExistsOnHost));

    public FlatpakHostCommandLauncher()
        : this(HostCommandProbe.CommandExistsAsync, HostCommandProbe.CommandExistsOnHostViaFlatpakSpawnAsync) { /* Empty */ }

    public FlatpakHostCommandLauncher(Func<string, CancellationToken, ValueTask<bool>> commandExistsInSandbox)
        : this(commandExistsInSandbox, HostCommandProbe.CommandExistsOnHostViaFlatpakSpawnAsync) { /* Empty */ }

    public async ValueTask<(bool IsAvailable, string FailureMessage)> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        if (!await _commandExistsInSandbox("flatpak-spawn", cancellationToken).ConfigureAwait(false))
        {
            return (false, "flatpak-spawn is missing in Flatpak environment.");
        }

        if (!await _commandExistsOnHost("pkexec", cancellationToken).ConfigureAwait(false))
        {
            return (false, "pkexec is missing on host. Install polkit and retry.");
        }

        return (true, string.Empty);
    }

    public ProcessStartInfo CreateStartInfo(string hostScript, LinuxQuickSetupIdentity identity)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "flatpak-spawn",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
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
