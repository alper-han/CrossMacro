using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
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
    private const int PkcheckTimeoutMs = 60000;
    private const int MaxTransientSubjectRetries = 2;
    private static readonly TimeSpan TransientSubjectRetryDelay = TimeSpan.FromMilliseconds(150);

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
            // Include start-time + uid in process subject to avoid transient polkit UID resolution failures.
            // Preferred subject format: <pid>,<start-time>,<uid>
            var processSubject = BuildProcessSubject(pid, uid);

            for (var attempt = 1; attempt <= MaxTransientSubjectRetries + 1; attempt++)
            {
                // pkcheck --action-id <action> --process <pid,start,uid> --allow-user-interaction
                var startInfo = new ProcessStartInfo
                {
                    FileName = "pkcheck",
                    ArgumentList =
                    {
                        "--action-id", actionId,
                        "--process", processSubject,
                        "--allow-user-interaction"
                    },
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var process = new Process { StartInfo = startInfo };

                Log.Debug(
                    "[Polkit] Running: pkcheck --action-id {Action} --process {ProcessSubject} --allow-user-interaction (attempt {Attempt}/{TotalAttempts})",
                    actionId,
                    processSubject,
                    attempt,
                    MaxTransientSubjectRetries + 1);

                process.Start();

                // Wait for pkcheck with timeout (user might need to enter password)
                var completed = await Task.Run(() => process.WaitForExit(PkcheckTimeoutMs));
                if (!completed)
                {
                    Log.Warning("[Polkit] pkcheck timed out");
                    process.Kill();
                    return false;
                }

                var exitCode = process.ExitCode;
                var stderr = await process.StandardError.ReadToEndAsync();

                if (IsTransientProcessSubjectError(exitCode, stderr) && attempt <= MaxTransientSubjectRetries)
                {
                    Log.Warning(
                        "[Polkit] Transient process-subject failure from pkcheck (exit={Code}): {Stderr}. Retrying.",
                        exitCode,
                        stderr.Trim());
                    await Task.Delay(TransientSubjectRetryDelay);
                    continue;
                }

                // Exit codes:
                // 0 = authorized
                // 1 = not authorized
                // 2 = authorization was dismissed
                // 126 = action does not exist
                if (exitCode == 0)
                {
                    Log.Information("[Polkit] Authorization GRANTED for {Action} (UID={Uid}, PID={Pid})",
                        actionId, uid, pid);
                    _polkitAvailable = true;
                    return true;
                }

                if (exitCode == 1 || exitCode == 2)
                {
                    Log.Information("[Polkit] Authorization DENIED for {Action} (UID={Uid}, PID={Pid})",
                        actionId, uid, pid);
                    _polkitAvailable = true;
                    return false;
                }

                if (exitCode == 126)
                {
                    Log.Warning("[Polkit] Action {Action} not registered - install the polkit policy file", actionId);
                    return false; // Policy not installed - deny connection
                }

                Log.Warning("[Polkit] pkcheck failed with exit code {Code}: {Stderr}",
                    exitCode, stderr);
                return false; // Fail closed - deny connection
            }

            return false;
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

    private static string BuildProcessSubject(int pid, uint uid)
    {
        if (TryGetProcessStartTime(pid, out var processStartTime))
        {
            return string.Create(
                CultureInfo.InvariantCulture,
                $"{pid},{processStartTime},{uid}");
        }

        Log.Debug("[Polkit] Failed to resolve process start-time for PID {Pid}; falling back to --process {Pid}", pid, pid);
        return pid.ToString(CultureInfo.InvariantCulture);
    }

    private static bool TryGetProcessStartTime(int pid, out ulong processStartTime)
    {
        processStartTime = 0;
        try
        {
            var statPath = string.Create(CultureInfo.InvariantCulture, $"/proc/{pid}/stat");
            if (!File.Exists(statPath))
            {
                return false;
            }

            var stat = File.ReadAllText(statPath);
            var commandEnd = stat.LastIndexOf(')');
            if (commandEnd < 0 || commandEnd + 2 >= stat.Length)
            {
                return false;
            }

            // /proc/<pid>/stat after ") " begins with field 3 (state).
            // starttime is field 22 => index 19 in this tail array.
            var tailFields = stat[(commandEnd + 2)..]
                .Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (tailFields.Length <= 19)
            {
                return false;
            }

            return ulong.TryParse(
                tailFields[19],
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out processStartTime);
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "[Polkit] Failed to parse /proc stat for PID {Pid}", pid);
            return false;
        }
    }

    private static bool IsTransientProcessSubjectError(int exitCode, string stderr)
    {
        if (string.IsNullOrWhiteSpace(stderr))
        {
            return false;
        }

        return exitCode != 0 &&
               (stderr.Contains("does not have uid set", StringComparison.OrdinalIgnoreCase) ||
                stderr.Contains("No such process", StringComparison.OrdinalIgnoreCase) ||
                stderr.Contains("process has changed", StringComparison.OrdinalIgnoreCase));
    }
}
