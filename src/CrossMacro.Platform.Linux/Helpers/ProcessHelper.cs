
namespace CrossMacro.Platform.Linux.Helpers;

/// <summary>
/// Utility class for executing shell commands safely.
/// </summary>
public static class ProcessHelper
{
    /// <summary>
    /// Executes a command and returns its standard output.
    /// Returns null if the command fails or is not found.
    /// </summary>
    public static string? ExecuteCommand(string fileName, string arguments = "")
    {
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
                var output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();
                if (process.ExitCode is 0) return output.Trim();
            }
        }
        catch (System.ComponentModel.Win32Exception)
        {
            Log.Debug("Command not found: {Command}", fileName);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to execute command: {Command} {Arguments}", fileName, arguments);
        }
        return null;
    }

    /// <summary>
    /// Retrieves the process name for a given PID.
    /// </summary>
    public static string GetProcessName(int pid)
    {
        if (pid <= 0) return string.Empty;
        try
        {
            var commPath = $"/proc/{pid}/comm";
            if (System.IO.File.Exists(commPath))
            {
                return System.IO.File.ReadAllText(commPath).Trim();
            }
            using var process = Process.GetProcessById(pid);
            return process.ProcessName;
        }
        catch
        {
            return string.Empty;
        }
    }
}
