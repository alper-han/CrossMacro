namespace CrossMacro.Platform.Linux.Services.QuickSetup;

internal sealed class FlatpakHostCommandLauncher(
    Func<string, CancellationToken, ValueTask<string?>> resolveCommandInSandbox,
    Func<string, string, CancellationToken, ValueTask<string?>> resolveCommandOnHost,
    Func<string, string, CancellationToken, ValueTask<bool>> pkexecIsUsableOnHost) : IPrivilegedHostCommandLauncher
{
    private readonly Func<string, CancellationToken, ValueTask<string?>> _resolveCommandInSandbox = resolveCommandInSandbox ?? throw new ArgumentNullException(nameof(resolveCommandInSandbox));
    private readonly Func<string, string, CancellationToken, ValueTask<string?>> _resolveCommandOnHost = resolveCommandOnHost ?? throw new ArgumentNullException(nameof(resolveCommandOnHost));
    private readonly Func<string, string, CancellationToken, ValueTask<bool>> _pkexecIsUsableOnHost = pkexecIsUsableOnHost ?? throw new ArgumentNullException(nameof(pkexecIsUsableOnHost));

    public FlatpakHostCommandLauncher()
        : this(HostCommandProbe.ResolveAsync, HostCommandProbe.ResolveOnHostViaFlatpakSpawnAsync, HostCommandProbe.PkexecIsUsableOnHostViaFlatpakSpawnAsync) { /* Empty */ }

    public async ValueTask<LinuxQuickSetupIdentity?> ResolveIdentityAsync(LinuxQuickSetupIdentityResolver resolver, CancellationToken cancellationToken = default)
    {
        var spawnPath = await _resolveCommandInSandbox("flatpak-spawn", cancellationToken).ConfigureAwait(false);
        if (spawnPath is null)
        {
            return null;
        }

        var output = await HostCommandProbe.ReadUidOnHostViaFlatpakSpawnAsync(spawnPath, cancellationToken).ConfigureAwait(false);
        return LinuxQuickSetupIdentityResolver.FromHostUid(output);
    }

    public async ValueTask<(ProcessStartInfo? StartInfo, string FailureMessage)> CreateStartInfoAsync(
        string hostScript, LinuxQuickSetupIdentity identity, CancellationToken cancellationToken = default)
    {
        var spawnPath = await _resolveCommandInSandbox("flatpak-spawn", cancellationToken).ConfigureAwait(false);
        if (spawnPath is null)
        {
            return (null, "Executable flatpak-spawn is missing in the Flatpak environment.");
        }

        var (command, failureMessage) = await HostPrivilegeCommand.SelectAsync(
            (name, token) => _resolveCommandOnHost(spawnPath, name, token),
            (path, token) => _pkexecIsUsableOnHost(spawnPath, path, token),
            cancellationToken).ConfigureAwait(false);
        if (command is null)
        {
            return (null, failureMessage);
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = spawnPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        startInfo.ArgumentList.Add("--host");
        startInfo.ArgumentList.Add("--watch-bus");
        startInfo.ArgumentList.Add("--directory=/");
        startInfo.ArgumentList.Add(command.Path);
        HostPrivilegeCommand.AddArguments(
            startInfo,
            command.Kind,
            hostScript,
            identity,
            "crossmacro-session-helper");

        return (startInfo, string.Empty);
    }
}
