
namespace CrossMacro.UI.Services;

public sealed class ExternalUrlOpener : IExternalUrlOpener
{
    private readonly Func<ProcessStartInfo, Task<LaunchResult>> _tryStart;
    private readonly Func<string, bool> _commandExists;
    private readonly IRuntimeContext _runtimeContext;

    public ExternalUrlOpener()
    {
        throw new InvalidOperationException("IRuntimeContext must be supplied by composition.");
    }

    public ExternalUrlOpener(IRuntimeContext runtimeContext)
        : this(runtimeContext, TryStartProcessAsync, CommandExists) { /* Empty */ }

    internal ExternalUrlOpener(
        IRuntimeContext runtimeContext,
        Func<ProcessStartInfo, Task<LaunchResult>> tryStart,
        Func<string, bool> commandExists)
    {
        _runtimeContext = runtimeContext ?? throw new ArgumentNullException(nameof(runtimeContext));
        _tryStart = tryStart ?? throw new ArgumentNullException(nameof(tryStart));
        _commandExists = commandExists ?? throw new ArgumentNullException(nameof(commandExists));
    }

    public async Task OpenAsync(string url)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || !IsSupportedUrl(uri))
        {
            throw new ArgumentException("Only absolute HTTP and HTTPS URLs can be opened.", nameof(url));
        }

        await OpenCoreAsync(url).ConfigureAwait(false);
    }

    public async Task OpenAsync(Uri url)
    {
        ArgumentNullException.ThrowIfNull(url);
        if (!url.IsAbsoluteUri || !IsSupportedUrl(url))
        {
            throw new ArgumentException("Only absolute HTTP and HTTPS URLs can be opened.", nameof(url));
        }

        await OpenCoreAsync(url.AbsoluteUri).ConfigureAwait(false);
    }

    private async Task OpenCoreAsync(string url)
    {
        List<Exception> failures = [];
        foreach (var startInfo in CreateStartInfos(url, _runtimeContext, _commandExists))
        {
            try
            {
                var result = await _tryStart(startInfo).ConfigureAwait(false);
                if (result.Success)
                {
                    return;
                }

                if (result.Failure is not null)
                {
                    failures.Add(result.Failure);
                }
            }
            catch (Win32Exception ex) when (IsCommandNotFound(ex))
            {
                // A command can disappear between the PATH check and Process.Start.
                // Missing optional Linux fallback commands should not obscure the real opener failure.
            }
            catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
            {
                failures.Add(ex);
            }
        }

        throw CreateOpenFailedException(failures);
    }

    private static bool IsSupportedUrl(Uri uri)
    {
        return string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.Ordinal)
            || string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal);
    }

    private static IEnumerable<ProcessStartInfo> CreateStartInfos(
        string url,
        IRuntimeContext runtimeContext,
        Func<string, bool> commandExists)
    {
        if (!runtimeContext.IsLinux || runtimeContext.IsFlatpak)
        {
            yield return new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true,
            };

            yield break;
        }

        foreach (var command in GetLinuxFallbackCommands().Where(command => commandExists(command.FileName)))
        {
            yield return CreateCommand(command.FileName, url, command.ArgumentsBeforeUrl);
        }
    }

    private static IEnumerable<LinuxOpenCommand> GetLinuxFallbackCommands()
    {
        yield return new LinuxOpenCommand("xdg-open");
        yield return new LinuxOpenCommand("gio", "open");
        yield return new LinuxOpenCommand("sensible-browser");
    }

    private static ProcessStartInfo CreateCommand(string fileName, string url, IReadOnlyList<string> argumentsBeforeUrl)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
        };

        foreach (var argument in argumentsBeforeUrl)
        {
            startInfo.ArgumentList.Add(argument);
        }

        startInfo.ArgumentList.Add(url);
        return startInfo;
    }

    private static async Task<LaunchResult> TryStartProcessAsync(ProcessStartInfo startInfo)
    {
        using var process = Process.Start(startInfo);
        if (process is null)
        {
            return LaunchResult.Failed(new InvalidOperationException($"Launcher '{startInfo.FileName}' did not start."));
        }

        var standardErrorTask = startInfo.RedirectStandardError
            ? process.StandardError.ReadToEndAsync(default)
            : null;
        var standardOutputTask = startInfo.RedirectStandardOutput
            ? process.StandardOutput.ReadToEndAsync(default)
            : null;

        var exitTask = process.WaitForExitAsync(CancellationToken.None);
        if (await Task.WhenAny(exitTask, Task.Delay(TimeSpan.FromSeconds(2), TimeProvider.System, CancellationToken.None)).ConfigureAwait(false) != exitTask)
        {
            ObserveFault(exitTask);
            ObserveFault(standardErrorTask);
            ObserveFault(standardOutputTask);
            return LaunchResult.Succeeded;
        }

        await exitTask.ConfigureAwait(false);

        var errorOutput = standardErrorTask is null
            ? string.Empty
            : (await standardErrorTask.ConfigureAwait(false)).Trim();
        if (standardOutputTask is not null)
        {
            _ = await standardOutputTask.ConfigureAwait(false);
        }

        if (process.ExitCode is 0)
        {
            return LaunchResult.Succeeded;
        }

        var error = errorOutput;
        var message = string.IsNullOrWhiteSpace(error)
            ? $"Launcher '{startInfo.FileName}' exited with code {process.ExitCode.ToString(CultureInfo.InvariantCulture)}."
            : $"Launcher '{startInfo.FileName}' exited with code {process.ExitCode.ToString(CultureInfo.InvariantCulture)}: {error}";
        return LaunchResult.Failed(new InvalidOperationException(message));
    }

    private static void ObserveFault(Task? task)
    {
        if (task is null)
        {
            return;
        }

        _ = task.ContinueWith(
            static completedTask => _ = completedTask.Exception,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private static InvalidOperationException CreateOpenFailedException(List<Exception> failures)
    {
        const string message = "Unable to open the URL with the available desktop launchers.";
        return failures.Count is 0
            ? new InvalidOperationException(message)
            : new InvalidOperationException(message, new AggregateException(failures));
    }

    private static bool CommandExists(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return false;
        }

        if (fileName.Contains(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
            || fileName.Contains(Path.AltDirectorySeparatorChar.ToString(), StringComparison.Ordinal))
        {
            return File.Exists(fileName);
        }

        var path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        return path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(directory => Path.Combine(directory, fileName))
            .Any(File.Exists);
    }

    private static bool IsCommandNotFound(Win32Exception exception)
    {
        return exception.NativeErrorCode is 2;
    }

    internal readonly record struct LaunchResult(bool Success, Exception? Failure)
    {
        public static LaunchResult Succeeded { get; } = new(Success: true, Failure: null);

        public static LaunchResult Failed(Exception failure)
        {
            return new LaunchResult(Success: false, failure ?? throw new ArgumentNullException(nameof(failure)));
        }
    }

    private readonly record struct LinuxOpenCommand(string FileName, params string[] ArgumentsBeforeUrl);
}
