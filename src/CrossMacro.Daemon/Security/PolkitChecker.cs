using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Serilog;

namespace CrossMacro.Daemon.Security;

/// <summary>
/// Polkit authorization checker using pkcheck command.
/// Requires polkitd to be running and org.crossmacro.policy to be installed.
/// Uses auth_self_keep - user enters their own password, cached for 5 minutes.
/// 
/// This implementation uses pkcheck instead of D-Bus for simplicity and
/// guaranteed AOT compatibility.
/// </summary>
public static class PolkitChecker
{
    /// <summary>
    /// Polkit action IDs for CrossMacro operations.
    /// </summary>
    public static class Actions
    {
        public const string InputCapture = "org.crossmacro.input-capture";
        public const string InputSimulate = "org.crossmacro.input-simulate";
    }

    private static bool _polkitAvailable = true;
    private static DateTime _lastPolkitCheck = DateTime.MinValue;

    /// <summary>
    /// Checks if the process is authorized to perform the given action.
    /// Uses pkcheck command with --allow-user-interaction flag.
    /// </summary>
    /// <param name="uid">User ID of the process</param>
    /// <param name="pid">Process ID</param>
    /// <param name="actionId">Polkit action ID (e.g., org.crossmacro.input-capture)</param>
    /// <returns>True if authorized, false otherwise</returns>
    public static async Task<bool> CheckAuthorizationAsync(uint uid, int pid, string actionId)
    {
        // Reject if Polkit was unavailable recently
        if (!_polkitAvailable && DateTime.UtcNow - _lastPolkitCheck < TimeSpan.FromMinutes(5))
        {
            Log.Warning("[Polkit] Polkit is not available - daemon requires polkit for authorization");
            return false; // Polkit is required for daemon mode
        }

        try
        {
            // pkcheck --action-id <action> --process <pid> --allow-user-interaction
            var startInfo = new ProcessStartInfo
            {
                FileName = "pkcheck",
                ArgumentList = 
                {
                    "--action-id", actionId,
                    "--process", pid.ToString(),
                    "--allow-user-interaction"
                },
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = startInfo };
            
            Log.Debug("[Polkit] Running: pkcheck --action-id {Action} --process {Pid} --allow-user-interaction", 
                actionId, pid);
            
            process.Start();

            // Wait for pkcheck with timeout (user might need to enter password)
            var completed = await Task.Run(() => process.WaitForExit(60000)); // 60 second timeout
            
            if (!completed)
            {
                Log.Warning("[Polkit] pkcheck timed out");
                process.Kill();
                return false;
            }

            var exitCode = process.ExitCode;
            var stderr = await process.StandardError.ReadToEndAsync();

            // Exit codes:
            // 0 = authorized
            // 1 = not authorized
            // 2 = authorization was dismissed
            // 126 = action does not exist
            // 127 = pkcheck not found
            
            if (exitCode == 0)
            {
                Log.Information("[Polkit] Authorization GRANTED for {Action} (UID={Uid}, PID={Pid})", 
                    actionId, uid, pid);
                _polkitAvailable = true;
                return true;
            }
            else if (exitCode == 1 || exitCode == 2)
            {
                Log.Information("[Polkit] Authorization DENIED for {Action} (UID={Uid}, PID={Pid})", 
                    actionId, uid, pid);
                _polkitAvailable = true;
                return false;
            }
            else if (exitCode == 126)
            {
                Log.Warning("[Polkit] Action {Action} not registered - install the polkit policy file", actionId);
                return false; // Policy not installed - deny connection
            }
            else
            {
                Log.Warning("[Polkit] pkcheck failed with exit code {Code}: {Stderr}", 
                    exitCode, stderr);
                return false; // Fail closed - deny connection
            }
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 2)
        {
            // pkcheck not found (ENOENT)
            Log.Warning("[Polkit] pkcheck command not found - polkit is required for daemon mode");
            _polkitAvailable = false;
            _lastPolkitCheck = DateTime.UtcNow;
            return false; // Polkit is required
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[Polkit] Authorization check failed");
            _polkitAvailable = false;
            _lastPolkitCheck = DateTime.UtcNow;
            return false; // Fail closed - polkit is required for daemon mode
        }
    }
}
