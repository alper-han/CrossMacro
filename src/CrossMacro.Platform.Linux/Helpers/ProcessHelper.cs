
namespace CrossMacro.Platform.Linux.Helpers;

/// <summary>
/// Small helpers around external programs and process identity.
/// </summary>
internal static class ProcessHelper
{
    private static readonly TimeSpan CommandTimeout = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Runs a program without a shell and returns its trimmed standard output, within a two-second deadline.
    /// Returns null if the program fails, times out, or is not found. Caller cancellation propagates.
    /// </summary>
    public static async Task<string?> ExecuteCommandAsync(string fileName, IReadOnlyList<string> arguments, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await ExternalCommand.RunAsync(ExternalCommand.CreateStartInfo(fileName, arguments), CommandTimeout, cancellationToken).ConfigureAwait(false);
            return result.ExitCode is 0 ? result.StdOut.Trim() : null;
        }
        catch (TimeoutException)
        {
            Log.Debug("Command timed out: {Command} {Arguments}", fileName, arguments);
        }
        catch (System.ComponentModel.Win32Exception)
        {
            Log.Debug("Command not found: {Command}", fileName);
        }
        catch (Exception ex) when (ex is not (OutOfMemoryException or OperationCanceledException))
        {
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
