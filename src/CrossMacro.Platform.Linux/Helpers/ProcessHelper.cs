
namespace CrossMacro.Platform.Linux.Helpers;

/// <summary>
/// Utility class for executing shell commands safely.
/// </summary>
public static class ProcessHelper
{
    /// <summary>
    /// Executes a command asynchronously with a two-second deadline for output and exit.
    /// Returns null if the command fails, times out, or is not found. Caller cancellation propagates.
    /// </summary>
    public static async Task<string?> ExecuteCommandAsync(string fileName, string arguments = "", CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var commandCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        commandCancellation.CancelAfter(TimeSpan.FromSeconds(2));

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var process = Process.Start(startInfo);
            if (process is not null)
            {
                try
                {
                    var output = await process.StandardOutput.ReadToEndAsync(commandCancellation.Token).ConfigureAwait(false);
                    await process.WaitForExitAsync(commandCancellation.Token).ConfigureAwait(false);
                    return process.ExitCode is 0 ? output.Trim() : null;
                }
                catch (OperationCanceledException)
                {
                    try
                    {
                        if (!process.HasExited)
                        {
                            process.Kill(entireProcessTree: true);
                        }
                    }
                    catch (InvalidOperationException)
                    {
                        // The command exited between checking its state and killing it.
                    }

                    throw;
                }
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            Log.Debug("Command timed out: {Command} {Arguments}", fileName, arguments);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            Log.Debug("Command not found: {Command}", fileName);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Log.LogError(ex, "Failed to execute command: {Command} {Arguments}", fileName, arguments);
        }
        return null;
    }

    /// <summary>
    /// Retrieves the process name for a given PID.
    /// </summary>
    public static string GetProcessName(int pid)
    {
        if (pid <= 0)
        {
            return string.Empty;
        }

        try
        {
            var commPath = $"/proc/{pid.ToString(CultureInfo.InvariantCulture)}/comm";
            if (System.IO.File.Exists(commPath))
            {
                return System.IO.File.ReadAllText(commPath).Trim();
            }
            using var process = Process.GetProcessById(pid);
            return process.ProcessName;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return string.Empty;
        }
    }
}
