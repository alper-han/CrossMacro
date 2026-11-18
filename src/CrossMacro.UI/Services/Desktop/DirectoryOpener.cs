using System.Diagnostics;

namespace CrossMacro.UI.Services.Desktop;

/// <summary>
/// Opens a local directory in the platform's file manager. Mirrors the desktop-launcher
/// fallback chain of <see cref="ExternalUrlOpener"/> for URLs, adapted for folder paths.
/// </summary>
public sealed class DirectoryOpener
{
    private static readonly OpenCommand[] LinuxOpenCommands =
        [new("xdg-open"), new("gio", "open")];

    private readonly IRuntimeContext _runtimeContext;
    private readonly Func<ProcessStartInfo, Task<LaunchResult>> _tryStart;
    private readonly Func<string, bool> _commandExists;

    public DirectoryOpener(IRuntimeContext runtimeContext)
        : this(runtimeContext, DesktopProcessLauncher.TryStartAsync, DesktopProcessLauncher.CommandExists) { /* Empty */ }

    internal DirectoryOpener(
        IRuntimeContext runtimeContext,
        Func<ProcessStartInfo, Task<LaunchResult>> tryStart,
        Func<string, bool> commandExists)
    {
        _runtimeContext = runtimeContext ?? throw new ArgumentNullException(nameof(runtimeContext));
        _tryStart = tryStart ?? throw new ArgumentNullException(nameof(tryStart));
        _commandExists = commandExists ?? throw new ArgumentNullException(nameof(commandExists));
    }

    public async Task OpenAsync(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!Directory.Exists(path))
        {
            throw new DirectoryNotFoundException($"Directory '{path}' does not exist.");
        }

        List<Exception> failures = [];
        if (!_runtimeContext.IsLinux || _runtimeContext.IsFlatpak)
        {
            if (await DesktopProcessLauncher.TryEachAsync([DesktopProcessLauncher.CreateShellExecute(path)], _tryStart, failures).ConfigureAwait(false))
            {
                return;
            }

            throw new InvalidOperationException(
                $"Failed to open directory '{path}' with the desktop shell.",
                failures.Count is 0 ? null : new AggregateException(failures));
        }

        var candidates = LinuxOpenCommands
            .Where(command => _commandExists(command.FileName))
            .Select(command => DesktopProcessLauncher.CreateCommand(command, path));
        if (await DesktopProcessLauncher.TryEachAsync(candidates, _tryStart, failures).ConfigureAwait(false))
        {
            return;
        }

        throw failures.Count is 0
            ? new InvalidOperationException("No desktop launcher is available to open the directory.")
            : new InvalidOperationException("Unable to open the directory with the available desktop launchers.", new AggregateException(failures));
    }
}
