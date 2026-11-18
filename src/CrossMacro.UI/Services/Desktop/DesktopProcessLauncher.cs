using System.Diagnostics;

namespace CrossMacro.UI.Services.Desktop;

/// <summary>
/// Starts desktop launchers (xdg-open, gio, the shell) for the URL and directory openers.
/// A launcher that is still running after the grace period is treated as having handled the request.
/// </summary>
internal static class DesktopProcessLauncher
{
    private static readonly TimeSpan SuccessGracePeriod = TimeSpan.FromSeconds(2);

    internal static ProcessStartInfo CreateCommand(OpenCommand command, string target)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = command.FileName,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
        };

        foreach (var argument in command.ArgumentsBeforeTarget)
        {
            startInfo.ArgumentList.Add(argument);
        }

        startInfo.ArgumentList.Add(target);
        return startInfo;
    }

    internal static ProcessStartInfo CreateShellExecute(string target) => new(target) { UseShellExecute = true };

    internal static bool CommandExists(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return false;
        }

        if (fileName.Contains(Path.DirectorySeparatorChar, StringComparison.Ordinal)
            || fileName.Contains(Path.AltDirectorySeparatorChar, StringComparison.Ordinal))
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

    /// <summary>Starts the candidates in order until one succeeds, collecting the failures of the others.</summary>
    internal static async Task<bool> TryEachAsync(
        IEnumerable<ProcessStartInfo> candidates,
        Func<ProcessStartInfo, Task<LaunchResult>> tryStart,
        List<Exception> failures)
    {
        foreach (var startInfo in candidates)
        {
            try
            {
                var result = await tryStart(startInfo).ConfigureAwait(false);
                if (result.Success)
                {
                    return true;
                }

                if (result.Failure is not null)
                {
                    failures.Add(result.Failure);
                }
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode is 2)
            {
                // A command can disappear between the PATH check and Process.Start.
                // Missing optional Linux fallback commands should not obscure the real opener failure.
            }
            catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
            {
                failures.Add(ex);
            }
        }

        return false;
    }

    internal static async Task<LaunchResult> TryStartAsync(ProcessStartInfo startInfo)
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
        if (await Task.WhenAny(exitTask, Task.Delay(SuccessGracePeriod, TimeProvider.System, CancellationToken.None)).ConfigureAwait(false) != exitTask)
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

        var exitCode = process.ExitCode.ToString(CultureInfo.InvariantCulture);
        var message = string.IsNullOrWhiteSpace(errorOutput)
            ? $"Launcher '{startInfo.FileName}' exited with code {exitCode}."
            : $"Launcher '{startInfo.FileName}' exited with code {exitCode}: {errorOutput}";
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
}
