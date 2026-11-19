using System.ComponentModel;

namespace CrossMacro.Platform.Linux.Helpers;

/// <summary>
/// Runs one external program without a shell: arguments are passed as discrete values, output and error are
/// drained concurrently, and the whole process tree is killed when the run is cancelled or its deadline passes.
/// </summary>
internal static class ExternalCommand
{
    private static readonly TimeSpan KillWait = TimeSpan.FromSeconds(5);

    /// <summary>Creates start info that runs <paramref name="fileName"/> directly with each argument kept as is.</summary>
    internal static ProcessStartInfo CreateStartInfo(string fileName, IEnumerable<string> arguments)
    {
        var startInfo = new ProcessStartInfo(fileName) { UseShellExecute = false };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        return startInfo;
    }

    /// <summary>
    /// Starts the process and returns its exit code with everything it wrote to standard output and error.
    /// Standard input is left to the caller's start info; output redirection is enforced.
    /// </summary>
    /// <param name="startInfo">The program to run. Redirection, shell execution and window creation are overwritten.</param>
    /// <param name="timeout">How long the process may run, or <see langword="null"/> for no limit.</param>
    /// <param name="cancellationToken">Cancels the run and kills the process tree.</param>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled.</exception>
    /// <exception cref="TimeoutException"><paramref name="timeout"/> elapsed before the process finished.</exception>
    /// <exception cref="Win32Exception">The program could not be started.</exception>
    internal static async Task<(int ExitCode, string StdOut, string StdErr)> RunAsync(
        ProcessStartInfo startInfo,
        TimeSpan? timeout,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        startInfo.UseShellExecute = false;
        startInfo.CreateNoWindow = true;
        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;

        using var process = new Process { StartInfo = startInfo };
        using var run = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (timeout is { } limit)
        {
            run.CancelAfter(limit);
        }

        _ = process.Start();
        var stdOut = process.StandardOutput.ReadToEndAsync(run.Token);
        var stdErr = process.StandardError.ReadToEndAsync(run.Token);
        try
        {
            await process.WaitForExitAsync(run.Token).ConfigureAwait(false);
            return (process.ExitCode, await stdOut.ConfigureAwait(false), await stdErr.ConfigureAwait(false));
        }
        catch (OperationCanceledException interrupted)
        {
            await KillTreeAsync(process, startInfo.FileName).ConfigureAwait(false);
            Task[] reads = [stdOut, stdErr];
            await Task.WhenAll(reads).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
            if (cancellationToken.IsCancellationRequested)
            {
                throw;
            }

            throw new TimeoutException($"'{startInfo.FileName}' did not finish before its deadline.", interrupted);
        }
    }

    private static async Task KillTreeAsync(Process process, string fileName)
    {
        try
        {
            if (process.HasExited)
            {
                return;
            }

            process.Kill(entireProcessTree: true);
            using var wait = new CancellationTokenSource(KillWait);
            await process.WaitForExitAsync(wait.Token).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception or AggregateException or OperationCanceledException)
        {
            // A privileged or systemd-owned helper may outlive its local client.
            Log.Warning(ex, "[ExternalCommand] Could not terminate the process tree of {Command}", fileName);
        }
    }
}
