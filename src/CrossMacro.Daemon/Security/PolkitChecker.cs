using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using CrossMacro.Daemon.Contracts.Security;

namespace CrossMacro.Daemon.Security;

/// <summary>
/// Authorizes a connecting process through the <c>pkcheck</c> command. Requires polkitd and the
/// CrossMacro polkit policy. <c>pkcheck</c> is used instead of D-Bus to stay Native AOT compatible.
/// A session offers capture and simulation; one check of <see cref="CheckedAction"/> covers both.
/// </summary>
internal sealed class PolkitChecker : IPolkitAuthorizationService
{
    // pkcheck exit codes, not pkexec codes: https://polkit.pages.freedesktop.org/polkit/pkcheck.1.html
    private const int AuthorizedExitCode = 0;
    private const int DeniedExitCode = 1;
    private const int AgentUnavailableExitCode = 2;
    private const int MalformedOptionsExitCode = 126;
    private const int FileNotFoundErrorCode = 2;
    private const int MaxTransientSubjectRetries = 2;
    private const int StartTimeFieldIndexAfterCommand = 19;
    private static readonly TimeSpan DefaultPkcheckTimeout = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan TransientSubjectRetryDelay = TimeSpan.FromMilliseconds(150);

    /// <summary>
    /// The policy makes input-capture imply input-simulate and names the daemon user as its owner.
    /// polkit only lets root or an action's owner check a process of another user, so the daemon
    /// cannot check input-simulate itself: pkcheck would fail for every client.
    /// </summary>
    internal const string CheckedAction = PolkitActions.InputCapture;

    private readonly string _pkcheckPath;
    private readonly TimeSpan _pkcheckTimeout;
    private readonly TimeProvider _timeProvider;

    public PolkitChecker()
        : this("pkcheck", DefaultPkcheckTimeout, TimeProvider.System)
    {
    }

    internal PolkitChecker(string pkcheckPath, TimeSpan pkcheckTimeout, TimeProvider timeProvider)
    {
        ArgumentException.ThrowIfNullOrEmpty(pkcheckPath);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _pkcheckPath = pkcheckPath;
        _pkcheckTimeout = pkcheckTimeout;
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// Checks whether the process may use the daemon. The subject always carries the process
    /// start-time so polkit rejects a recycled PID; without a start-time the request is denied.
    /// </summary>
    public async Task<bool> IsAuthorizedAsync(uint uid, int pid, CancellationToken cancellationToken = default)
    {
        var processSubject = await TryBuildProcessSubjectAsync(pid, uid, cancellationToken).ConfigureAwait(false);
        if (processSubject is null)
        {
            Log.Warning("[Polkit] Could not read the start-time of PID {Pid}; denying authorization", pid);
            return false;
        }

        return await CheckActionAsync(CheckedAction, processSubject, uid, pid, cancellationToken).ConfigureAwait(false);
    }

    private async Task<bool> CheckActionAsync(
        string actionId,
        string processSubject,
        uint uid,
        int pid,
        CancellationToken cancellationToken)
    {
        var retriesLeft = MaxTransientSubjectRetries;
        while (true)
        {
            var result = await RunPkcheckAsync(actionId, processSubject, cancellationToken).ConfigureAwait(false);
            if (result is null)
            {
                return false;
            }

            var (exitCode, stderr) = result.Value;
            if (retriesLeft > 0 && IsTransientProcessSubjectError(exitCode, stderr))
            {
                Log.Warning(
                    "[Polkit] Transient process-subject failure from pkcheck (exit={Code}): {Stderr}. Retrying.",
                    exitCode,
                    stderr.Trim());
                retriesLeft--;
                await Task.Delay(TransientSubjectRetryDelay, _timeProvider, cancellationToken).ConfigureAwait(false);
                continue;
            }

            switch (exitCode)
            {
                case AuthorizedExitCode:
                    Log.Information("[Polkit] Authorization GRANTED for {Action} (UID={Uid}, PID={Pid})", actionId, uid, pid);
                    return true;
                case DeniedExitCode or AgentUnavailableExitCode:
                    Log.Information("[Polkit] Authorization DENIED for {Action} (UID={Uid}, PID={Pid})", actionId, uid, pid);
                    return false;
                case MalformedOptionsExitCode:
                    Log.Warning("[Polkit] pkcheck rejected malformed options for {Action}: {Stderr}", actionId, stderr);
                    return false;
                default:
                    Log.Warning("[Polkit] pkcheck failed with exit code {Code}: {Stderr}", exitCode, stderr);
                    return false;
            }
        }
    }

    /// <returns>The pkcheck result, or <see langword="null"/> when pkcheck is missing or timed out.</returns>
    private async Task<(int ExitCode, string Stderr)?> RunPkcheckAsync(
        string actionId,
        string processSubject,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = _pkcheckPath,
            ArgumentList =
            {
                "--action-id", actionId,
                "--process", processSubject,
                "--allow-user-interaction",
            },
            UseShellExecute = false,
            // pkcheck prints "polkit\56result=yes"-style details on stdout; keep them out of the journal.
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        using var process = new Process { StartInfo = startInfo };

        Log.Debug("[Polkit] Running: pkcheck --action-id {Action} --process {ProcessSubject} --allow-user-interaction", actionId, processSubject);

        try
        {
            _ = process.Start();
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode is FileNotFoundErrorCode)
        {
            Log.Warning("[Polkit] pkcheck command not found - polkit is required for daemon mode");
            return null;
        }

        using var timeoutCts = new CancellationTokenSource(_pkcheckTimeout, _timeProvider);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
        try
        {
            var stdoutTask = process.StandardOutput.ReadToEndAsync(linkedCts.Token);
            var stderrTask = process.StandardError.ReadToEndAsync(linkedCts.Token);
            await process.WaitForExitAsync(linkedCts.Token).ConfigureAwait(false);
            _ = await stdoutTask.ConfigureAwait(false);
            return (process.ExitCode, await stderrTask.ConfigureAwait(false));
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            Log.Warning("[Polkit] pkcheck timed out");
            return null;
        }
        finally
        {
            KillIfRunning(process);
        }
    }

    private static void KillIfRunning(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill();
            }
        }
        catch (InvalidOperationException)
        {
            // The process exited between the check and the kill.
        }
    }

    private static async Task<string?> TryBuildProcessSubjectAsync(int pid, uint uid, CancellationToken cancellationToken)
    {
        var processStartTime = await TryGetProcessStartTimeAsync(pid, cancellationToken).ConfigureAwait(false);
        return processStartTime is null
            ? null
            : string.Create(CultureInfo.InvariantCulture, $"{pid},{processStartTime.Value},{uid}");
    }

    private static async Task<ulong?> TryGetProcessStartTimeAsync(int pid, CancellationToken cancellationToken)
    {
        try
        {
            var statPath = string.Create(CultureInfo.InvariantCulture, $"/proc/{pid}/stat");
            var stat = await File.ReadAllTextAsync(statPath, cancellationToken).ConfigureAwait(false);
            var commandEnd = stat.LastIndexOf(')');
            if (commandEnd < 0 || commandEnd + 2 >= stat.Length)
            {
                return null;
            }

            // /proc/<pid>/stat after ") " begins with field 3 (state); starttime is field 22.
            var tailFields = stat[(commandEnd + 2)..]
                .Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (tailFields.Length <= StartTimeFieldIndexAfterCommand)
            {
                return null;
            }

            return ulong.TryParse(
                tailFields[StartTimeFieldIndexAfterCommand],
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var processStartTime)
                ? processStartTime
                : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Debug(ex, "[Polkit] Failed to read /proc stat for PID {Pid}", pid);
            return null;
        }
    }

    private static bool IsTransientProcessSubjectError(int exitCode, string stderr)
    {
        if (string.IsNullOrWhiteSpace(stderr))
        {
            return false;
        }

        return exitCode is not 0 &&
               (stderr.Contains("does not have uid set", StringComparison.OrdinalIgnoreCase) ||
                stderr.Contains("No such process", StringComparison.OrdinalIgnoreCase) ||
                stderr.Contains("process has changed", StringComparison.OrdinalIgnoreCase));
    }
}
