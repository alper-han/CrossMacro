namespace CrossMacro.UI.Services;

/// <summary>
/// Opens a local directory in the platform's file manager. Mirrors the desktop-launcher
/// fallback chain of <see cref="ExternalUrlOpener"/> for URLs, adapted for folder paths.
/// </summary>
public sealed class DirectoryOpener : IDirectoryOpener
{
    private static readonly (string FileName, string? Argument)[] LinuxOpenCommands =
        [("xdg-open", null), ("gio", "open")];

    private readonly IRuntimeContext _runtimeContext;
    private readonly Func<ProcessStartInfo, Task<bool>> _tryLaunch;
    private readonly Func<string, bool> _commandExists;

    public DirectoryOpener(IRuntimeContext runtimeContext)
        : this(runtimeContext, TryLaunchProcessAsync, CommandExists) { /* Empty */ }

    internal DirectoryOpener(
        IRuntimeContext runtimeContext,
        Func<ProcessStartInfo, Task<bool>> tryLaunch,
        Func<string, bool> commandExists)
    {
        _runtimeContext = runtimeContext ?? throw new ArgumentNullException(nameof(runtimeContext));
        _tryLaunch = tryLaunch ?? throw new ArgumentNullException(nameof(tryLaunch));
        _commandExists = commandExists ?? throw new ArgumentNullException(nameof(commandExists));
    }

    public async Task OpenAsync(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!Directory.Exists(path))
        {
            throw new DirectoryNotFoundException($"Directory '{path}' does not exist.");
        }

        if (!_runtimeContext.IsLinux || _runtimeContext.IsFlatpak)
        {
            var startInfo = new ProcessStartInfo(path) { UseShellExecute = true };
            if (!await _tryLaunch(startInfo).ConfigureAwait(false))
            {
                throw new InvalidOperationException($"Failed to open directory '{path}' with the desktop shell.");
            }

            return;
        }

        List<Exception> failures = [];
        foreach (var (fileName, argument) in LinuxOpenCommands)
        {
            if (!_commandExists(fileName))
            {
                continue;
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
            };
            if (argument is not null)
            {
                startInfo.ArgumentList.Add(argument);
            }

            startInfo.ArgumentList.Add(path);

            try
            {
                if (await _tryLaunch(startInfo).ConfigureAwait(false))
                {
                    return;
                }

                failures.Add(new InvalidOperationException($"Launcher '{fileName}' did not start."));
            }
            catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
            {
                failures.Add(ex);
            }
        }

        throw failures.Count is 0
            ? new InvalidOperationException("No desktop launcher is available to open the directory.")
            : new InvalidOperationException("Unable to open the directory with the available desktop launchers.", new AggregateException(failures));
    }

    private static async Task<bool> TryLaunchProcessAsync(ProcessStartInfo startInfo)
    {
        using var process = Process.Start(startInfo);
        if (process is null)
        {
            return false;
        }

        // A launcher that stays alive past the grace window dispatched the request fine.
        using var gracePeriod = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        try
        {
            await process.WaitForExitAsync(gracePeriod.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return true;
        }

        if (process.ExitCode is 0)
        {
            return true;
        }

        var errorOutput = startInfo.RedirectStandardError
            ? (await process.StandardError.ReadToEndAsync(CancellationToken.None).ConfigureAwait(false)).Trim()
            : string.Empty;
        throw new InvalidOperationException(string.IsNullOrWhiteSpace(errorOutput)
            ? $"Launcher '{startInfo.FileName}' exited with code {process.ExitCode.ToString(CultureInfo.InvariantCulture)}."
            : $"Launcher '{startInfo.FileName}' exited with code {process.ExitCode.ToString(CultureInfo.InvariantCulture)}: {errorOutput}");
    }

    private static bool CommandExists(string fileName)
    {
        var pathVariable = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(pathVariable))
        {
            return false;
        }

        return pathVariable
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(directory => Path.Combine(directory, fileName))
            .Any(File.Exists);
    }
}
