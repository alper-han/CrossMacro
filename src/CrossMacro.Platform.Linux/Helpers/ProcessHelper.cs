using System.Diagnostics;
using CrossMacro.Core.Logging;

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
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process != null)
            {
                var output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();
                if (process.ExitCode == 0) return output.Trim();
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
}
