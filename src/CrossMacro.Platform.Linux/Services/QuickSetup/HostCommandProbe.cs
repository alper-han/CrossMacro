using System.Diagnostics;

namespace CrossMacro.Platform.Linux.Services.QuickSetup;

internal static class HostCommandProbe
{
    public static bool CommandExists(string fileName)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "sh",
                    ArgumentList = { "-c", $"command -v {fileName} >/dev/null 2>&1" },
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            process.WaitForExit();
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    public static bool CommandExistsOnHostViaFlatpakSpawn(string fileName)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "flatpak-spawn",
                    ArgumentList = { "--host", "sh", "-c", $"command -v {fileName} >/dev/null 2>&1" },
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            process.WaitForExit();
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}
