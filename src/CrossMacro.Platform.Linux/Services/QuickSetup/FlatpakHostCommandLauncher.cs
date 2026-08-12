
namespace CrossMacro.Platform.Linux.Services.QuickSetup;

internal sealed class FlatpakHostCommandLauncher(
    Func<string, CancellationToken, ValueTask<bool>> commandExistsInSandbox,
    Func<string, CancellationToken, ValueTask<bool>> commandExistsOnHost,
    Func<CancellationToken, ValueTask<bool>> pkexecIsUsableOnHost) : IPrivilegedHostCommandLauncher
{
    private readonly Func<string, CancellationToken, ValueTask<bool>> _commandExistsInSandbox = commandExistsInSandbox ?? throw new ArgumentNullException(nameof(commandExistsInSandbox));
    private readonly Func<string, CancellationToken, ValueTask<bool>> _commandExistsOnHost = commandExistsOnHost ?? throw new ArgumentNullException(nameof(commandExistsOnHost));
    private readonly Func<CancellationToken, ValueTask<bool>> _pkexecIsUsableOnHost = pkexecIsUsableOnHost ?? throw new ArgumentNullException(nameof(pkexecIsUsableOnHost));
    private int _selectedCommand;

    public FlatpakHostCommandLauncher()
        : this(HostCommandProbe.CommandExistsAsync, HostCommandProbe.CommandExistsOnHostViaFlatpakSpawnAsync, HostCommandProbe.PkexecIsUsableOnHostViaFlatpakSpawnAsync) { /* Empty */ }

    public FlatpakHostCommandLauncher(Func<string, CancellationToken, ValueTask<bool>> commandExistsInSandbox)
        : this(commandExistsInSandbox, HostCommandProbe.CommandExistsOnHostViaFlatpakSpawnAsync, HostCommandProbe.PkexecIsUsableOnHostViaFlatpakSpawnAsync) { /* Empty */ }

    public FlatpakHostCommandLauncher(
        Func<string, CancellationToken, ValueTask<bool>> commandExistsInSandbox,
        Func<string, CancellationToken, ValueTask<bool>> commandExistsOnHost)
        : this(commandExistsInSandbox, commandExistsOnHost, HostCommandProbe.PkexecIsUsableOnHostViaFlatpakSpawnAsync) { /* Empty */ }

    public async ValueTask<(bool IsAvailable, string FailureMessage)> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        if (!await _commandExistsInSandbox("flatpak-spawn", cancellationToken).ConfigureAwait(false))
        {
            return (false, "flatpak-spawn is missing in Flatpak environment.");
        }

        var (commandKind, failureMessage) = await HostPrivilegeCommand.SelectAsync(
            _commandExistsOnHost,
            _pkexecIsUsableOnHost,
            cancellationToken).ConfigureAwait(false);
        Volatile.Write(ref _selectedCommand, (int)commandKind);

        return commandKind is HostPrivilegeCommand.Kind.None
            ? (false, failureMessage)
            : (true, string.Empty);
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

        var commandKind = (HostPrivilegeCommand.Kind)Volatile.Read(ref _selectedCommand);
        startInfo.ArgumentList.Add("--host");
        startInfo.ArgumentList.Add(HostPrivilegeCommand.GetFileName(commandKind));
        HostPrivilegeCommand.AddArguments(
            startInfo,
            commandKind,
            hostScript,
            identity,
            "crossmacro-session-helper");

        return startInfo;
    }
}
