using System;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Core.Services;
using Serilog;

namespace CrossMacro.Platform.Linux.Services;

public sealed class FlatpakQuickSetupService : IFlatpakQuickSetupService
{
    private const string FlatpakIdKey = "FLATPAK_ID";
    private const string SessionTypeKey = "XDG_SESSION_TYPE";

    [DllImport("libc", SetLastError = true)]
    private static extern uint geteuid();

    private readonly Func<string, string?> _getEnvironmentVariable;
    private readonly Func<uint?> _getEffectiveUid;
    private readonly Func<string> _getUserName;
    private readonly Func<ProcessStartInfo, CancellationToken, Task<(int ExitCode, string StdOut, string StdErr)>> _runProcessAsync;

    public FlatpakQuickSetupService()
        : this(Environment.GetEnvironmentVariable, () => Environment.UserName, TryGetEffectiveUid, RunProcessAsync)
    {
    }

    public FlatpakQuickSetupService(
        Func<string, string?> getEnvironmentVariable,
        Func<string> getUserName,
        Func<uint?> getEffectiveUid,
        Func<ProcessStartInfo, CancellationToken, Task<(int ExitCode, string StdOut, string StdErr)>> runProcessAsync)
    {
        _getEnvironmentVariable = getEnvironmentVariable ?? throw new ArgumentNullException(nameof(getEnvironmentVariable));
        _getUserName = getUserName ?? throw new ArgumentNullException(nameof(getUserName));
        _getEffectiveUid = getEffectiveUid ?? throw new ArgumentNullException(nameof(getEffectiveUid));
        _runProcessAsync = runProcessAsync ?? throw new ArgumentNullException(nameof(runProcessAsync));
    }

    public bool IsApplicable()
    {
        if (!OperatingSystem.IsLinux())
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(_getEnvironmentVariable(FlatpakIdKey)))
        {
            return false;
        }

        var sessionType = _getEnvironmentVariable(SessionTypeKey);
        if (!string.Equals(sessionType, "wayland", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    public async Task<FlatpakQuickSetupResult> RunAsync(CancellationToken cancellationToken = default)
    {
        var aclIdentity = ResolveAclIdentity();
        if (aclIdentity == null)
        {
            return new FlatpakQuickSetupResult(
                Success: false,
                Message: "Could not determine a valid host identity for session setup.");
        }

        var hostScript =
            "set -eu; " +
            "TARGET_IDENTITY=\"$1\"; " +
            "if ! command -v setfacl >/dev/null 2>&1; then " +
                "echo 'setfacl is missing on host. Install ACL package and retry.' >&2; " +
                "exit 22; " +
            "fi; " +
            "if command -v modprobe >/dev/null 2>&1; then modprobe uinput >/dev/null 2>&1 || true; fi; " +
            "for p in /dev/uinput /dev/input/uinput; do " +
                "if [ -e \"$p\" ]; then setfacl -m \"u:${TARGET_IDENTITY}:rw\" \"$p\"; fi; " +
            "done; " +
            "for p in /dev/input/event*; do " +
                "if [ -e \"$p\" ]; then setfacl -m \"u:${TARGET_IDENTITY}:r\" \"$p\"; fi; " +
            "done";

        var startInfo = new ProcessStartInfo
        {
            FileName = "flatpak-spawn",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        startInfo.ArgumentList.Add("--host");
        startInfo.ArgumentList.Add("pkexec");
        startInfo.ArgumentList.Add("sh");
        startInfo.ArgumentList.Add("-c");
        startInfo.ArgumentList.Add(hostScript);
        startInfo.ArgumentList.Add("crossmacro-session-helper");
        startInfo.ArgumentList.Add(aclIdentity.Value.Specifier);

        try
        {
            var (exitCode, stdout, stderr) = await _runProcessAsync(startInfo, cancellationToken);
            if (exitCode == 0)
            {
                Log.Information("[FlatpakQuickSetupService] Session helper completed successfully for {Identity}", aclIdentity.Value.LogDisplay);
                return new FlatpakQuickSetupResult(
                    Success: true,
                    Message: "Quick setup completed.");
            }

            var errorText = FirstNonEmptyLine(stderr) ?? FirstNonEmptyLine(stdout) ?? "Unknown host setup error.";
            Log.Warning("[FlatpakQuickSetupService] Session helper failed (ExitCode={ExitCode}): {Error}", exitCode, errorText);
            return new FlatpakQuickSetupResult(
                Success: false,
                Message: $"Quick setup failed (exit code {exitCode}). {errorText}");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[FlatpakQuickSetupService] Failed to run session helper command");
            return new FlatpakQuickSetupResult(
                Success: false,
                Message: "Failed to run quick setup command inside Flatpak.");
        }
    }

    private (string Specifier, string LogDisplay)? ResolveAclIdentity()
    {
        var uid = _getEffectiveUid();
        if (uid.HasValue)
        {
            var uidText = uid.Value.ToString(CultureInfo.InvariantCulture);
            return (uidText, $"uid:{uidText}");
        }

        var userName = _getUserName();
        if (string.IsNullOrWhiteSpace(userName))
        {
            return null;
        }

        var normalizedUserName = userName.Trim();
        if (HasControlCharacters(normalizedUserName))
        {
            return null;
        }

        return (normalizedUserName, normalizedUserName);
    }

    private static bool HasControlCharacters(string value)
    {
        foreach (var c in value)
        {
            if (char.IsControl(c))
            {
                return true;
            }
        }

        return false;
    }

    private static string? FirstNonEmptyLine(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        foreach (var line in content.Split('\n', StringSplitOptions.TrimEntries))
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                return line;
            }
        }

        return null;
    }

    private static async Task<(int ExitCode, string StdOut, string StdErr)> RunProcessAsync(
        ProcessStartInfo startInfo,
        CancellationToken cancellationToken)
    {
        using var process = new Process { StartInfo = startInfo };

        process.Start();

        var stdOutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stdErrTask = process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken);

        return (process.ExitCode, await stdOutTask, await stdErrTask);
    }

    private static uint? TryGetEffectiveUid()
    {
        if (!OperatingSystem.IsLinux())
        {
            return null;
        }

        try
        {
            return geteuid();
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "[FlatpakQuickSetupService] Failed to read effective UID");
            return null;
        }
    }
}
