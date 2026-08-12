
namespace CrossMacro.Platform.Linux.Services.QuickSetup;

internal sealed class DirectPolkitHostCommandLauncher(
    Func<string, CancellationToken, ValueTask<bool>> commandExists,
    Func<CancellationToken, ValueTask<bool>> pkexecIsUsable) : IPrivilegedHostCommandLauncher
{
    private readonly Func<string, CancellationToken, ValueTask<bool>> _commandExists = commandExists ?? throw new ArgumentNullException(nameof(commandExists));
    private readonly Func<CancellationToken, ValueTask<bool>> _pkexecIsUsable = pkexecIsUsable ?? throw new ArgumentNullException(nameof(pkexecIsUsable));
    private int _selectedCommand;

    public DirectPolkitHostCommandLauncher()
        : this(HostCommandProbe.CommandExistsAsync, HostCommandProbe.PkexecIsUsableAsync) { /* Empty */ }

    public DirectPolkitHostCommandLauncher(Func<string, CancellationToken, ValueTask<bool>> commandExists)
        : this(commandExists, HostCommandProbe.PkexecIsUsableAsync) { /* Empty */ }

    public async ValueTask<(bool IsAvailable, string FailureMessage)> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        var (commandKind, failureMessage) = await HostPrivilegeCommand.SelectAsync(
            _commandExists,
            _pkexecIsUsable,
            cancellationToken).ConfigureAwait(false);
        Volatile.Write(ref _selectedCommand, (int)commandKind);

        return commandKind is HostPrivilegeCommand.Kind.None
            ? (false, failureMessage)
            : (true, string.Empty);
    }

    public ProcessStartInfo CreateStartInfo(string hostScript, LinuxQuickSetupIdentity identity)
    {
        var commandKind = (HostPrivilegeCommand.Kind)Volatile.Read(ref _selectedCommand);
        var startInfo = new ProcessStartInfo
        {
            FileName = HostPrivilegeCommand.GetFileName(commandKind),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        HostPrivilegeCommand.AddArguments(
            startInfo,
            commandKind,
            hostScript,
            identity,
            "crossmacro-appimage-session-helper");

        return startInfo;
    }
}
