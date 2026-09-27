namespace CrossMacro.Platform.Linux.Services.QuickSetup;

internal sealed class DirectPolkitHostCommandLauncher(
    Func<string, CancellationToken, ValueTask<string?>> resolveCommand,
    Func<string, CancellationToken, ValueTask<bool>> pkexecIsUsable) : IPrivilegedHostCommandLauncher
{
    private readonly Func<string, CancellationToken, ValueTask<string?>> _resolveCommand = resolveCommand ?? throw new ArgumentNullException(nameof(resolveCommand));
    private readonly Func<string, CancellationToken, ValueTask<bool>> _pkexecIsUsable = pkexecIsUsable ?? throw new ArgumentNullException(nameof(pkexecIsUsable));

    public DirectPolkitHostCommandLauncher()
        : this(HostCommandProbe.ResolveAsync, HostCommandProbe.PkexecIsUsableAsync) { /* Empty */ }

    public ValueTask<LinuxQuickSetupIdentity?> ResolveIdentityAsync(LinuxQuickSetupIdentityResolver resolver, CancellationToken cancellationToken = default)
        => resolver.ResolveAsync(cancellationToken);

    public async ValueTask<(ProcessStartInfo? StartInfo, string FailureMessage)> CreateStartInfoAsync(
        string hostScript, LinuxQuickSetupIdentity identity, CancellationToken cancellationToken = default)
    {
        var (command, failureMessage) = await HostPrivilegeCommand.SelectAsync(
            _resolveCommand,
            _pkexecIsUsable,
            cancellationToken).ConfigureAwait(false);
        if (command is null)
        {
            return (null, failureMessage);
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = command.Path,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        HostPrivilegeCommand.AddArguments(
            startInfo,
            command.Kind,
            hostScript,
            identity,
            "crossmacro-appimage-session-helper");

        return (startInfo, string.Empty);
    }
}
