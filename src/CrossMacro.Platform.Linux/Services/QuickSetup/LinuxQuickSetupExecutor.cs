namespace CrossMacro.Platform.Linux.Services.QuickSetup;

internal sealed class LinuxQuickSetupExecutor(
    LinuxQuickSetupIdentityResolver identityResolver,
    Func<ProcessStartInfo, CancellationToken, Task<(int ExitCode, string StdOut, string StdErr)>> runProcessAsync)
{
    private readonly LinuxQuickSetupIdentityResolver _identityResolver = identityResolver ?? throw new ArgumentNullException(nameof(identityResolver));
    private readonly Func<ProcessStartInfo, CancellationToken, Task<(int ExitCode, string StdOut, string StdErr)>> _runProcessAsync = runProcessAsync ?? throw new ArgumentNullException(nameof(runProcessAsync));

    public LinuxQuickSetupExecutor(LinuxQuickSetupIdentityResolver identityResolver)
        : this(identityResolver, RunProcessAsync) { }

    public async Task<QuickSetupResult> RunAsync(
        IPrivilegedHostCommandLauncher launcher,
        LinuxQuickSetupScriptOptions scriptOptions,
        string logContext,
        string unexpectedFailureMessage,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(launcher);
        cancellationToken.ThrowIfCancellationRequested();
        var identity = await launcher.ResolveIdentityAsync(_identityResolver, cancellationToken).ConfigureAwait(false);
        if (identity == null)
        {
            return new QuickSetupResult(QuickSetupOutcome.Failed, "Could not determine a valid host identity for session setup.");
        }

        try
        {
            var (startInfo, failureMessage) = await launcher.CreateStartInfoAsync(
                LinuxQuickSetupScriptBuilder.Build(scriptOptions), identity.Value, cancellationToken).ConfigureAwait(false);
            if (startInfo is null)
            {
                return new QuickSetupResult(QuickSetupOutcome.PrivilegeUnavailable, BoundedDetails(failureMessage));
            }

            var (exitCode, stdout, stderr) = await _runProcessAsync(startInfo, cancellationToken).ConfigureAwait(false);
            if (exitCode is 0)
            {
                Log.Information("[{LogContext}] Session helper completed for {Identity}", logContext, identity.Value.LogDisplay);
                Log.Debug("[{LogContext}] Session helper output: {StdOut}; stderr: {StdErr}", logContext, stdout, stderr);
                return new QuickSetupResult(QuickSetupOutcome.Succeeded, "Quick setup completed. " + BoundedDetails(stdout));
            }

            Log.Warning("[{LogContext}] Session helper failed (ExitCode={ExitCode}). stdout: {StdOut}; stderr: {StdErr}", logContext, exitCode, stdout, stderr);
            var diagnostics = string.IsNullOrWhiteSpace(stderr) ? stdout : stderr;
            var outcome = ClassifyFailure(startInfo, exitCode, diagnostics);
            var summary = outcome switch
            {
                QuickSetupOutcome.Cancelled => "Quick setup authorization was cancelled.",
                QuickSetupOutcome.AuthorizationDenied => "Host authorization was not completed. The request may have been cancelled or denied.",
                QuickSetupOutcome.AuthenticationUnavailable => "No usable host authentication agent was available. Start a graphical polkit agent and try again.",
                QuickSetupOutcome.PrivilegeUnavailable => "The selected host privilege mechanism cannot elevate this process.",
                QuickSetupOutcome.Failed or QuickSetupOutcome.Succeeded or QuickSetupOutcome.DeviceAccessUnavailable => "Quick setup failed. Host permissions may have been partially changed.",
                _ => "Quick setup failed. Host permissions may have been partially changed.",
            };
            return new QuickSetupResult(outcome, $"{summary} (Exit code {exitCode.ToString(CultureInfo.InvariantCulture)}.) {BoundedDetails(diagnostics)}");
        }
        catch (OperationCanceledException)
        {
            Log.Information("[{LogContext}] Setup wait cancelled; any host permission changes already applied are not rolled back", logContext);
            throw;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.LogError(ex, "[{LogContext}] Failed to run session helper command", logContext);
            return new QuickSetupResult(QuickSetupOutcome.Failed, unexpectedFailureMessage);
        }
    }

    private static QuickSetupOutcome ClassifyFailure(ProcessStartInfo startInfo, int exitCode, string diagnostics)
    {
        var command = Path.GetFileName(startInfo.FileName);
        var isPkexec = string.Equals(command, "pkexec", StringComparison.Ordinal);
        if (string.Equals(command, "flatpak-spawn", StringComparison.Ordinal))
        {
            var hostCommand = startInfo.ArgumentList.FirstOrDefault(static argument => !argument.StartsWith("--", StringComparison.Ordinal));
            isPkexec = string.Equals(Path.GetFileName(hostCommand), "pkexec", StringComparison.Ordinal);
        }

        if (isPkexec && exitCode is 126)
        {
            return QuickSetupOutcome.Cancelled;
        }

        if (diagnostics.Contains("must be setuid root", StringComparison.OrdinalIgnoreCase)
            || diagnostics.Contains("no new privileges", StringComparison.OrdinalIgnoreCase)
            || diagnostics.Contains("nosuid", StringComparison.OrdinalIgnoreCase))
        {
            return QuickSetupOutcome.PrivilegeUnavailable;
        }

        if (diagnostics.Contains("No authentication agent", StringComparison.OrdinalIgnoreCase)
            || diagnostics.Contains("No polkit authentication agent", StringComparison.OrdinalIgnoreCase)
            || diagnostics.Contains("Error creating textual authentication agent", StringComparison.OrdinalIgnoreCase))
        {
            return QuickSetupOutcome.AuthenticationUnavailable;
        }

        if (diagnostics.Contains("not authorized", StringComparison.OrdinalIgnoreCase)
            || diagnostics.Contains("access denied", StringComparison.OrdinalIgnoreCase)
            || diagnostics.Contains("authentication failed", StringComparison.OrdinalIgnoreCase)
            || diagnostics.Contains("interactive authentication", StringComparison.OrdinalIgnoreCase)
            || diagnostics.Contains("authorization failed", StringComparison.OrdinalIgnoreCase))
        {
            return QuickSetupOutcome.AuthorizationDenied;
        }

        return QuickSetupOutcome.Failed;
    }

    private static string BoundedDetails(string text)
    {
        const int maximumLength = 512;
        var trimmed = text.Trim();
        var bounded = trimmed.Length > maximumLength ? trimmed[..maximumLength] + "..." : trimmed;
        return bounded.Replace('\r', ' ').Replace('\n', ' ');
    }

    private static async Task<(int ExitCode, string StdOut, string StdErr)> RunProcessAsync(
        ProcessStartInfo startInfo,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var process = new Process { StartInfo = startInfo };
        _ = process.Start();
        var stdOutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stdErrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            return (process.ExitCode, await stdOutTask.ConfigureAwait(false), await stdErrTask.ConfigureAwait(false));
        }
        catch (OperationCanceledException)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                    await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
                }
            }
            catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or AggregateException)
            {
                // A privileged or systemd-owned helper may outlive its local authorization client.
                Log.Warning(ex, "[LinuxQuickSetupExecutor] Could not terminate the setup process tree; host changes cannot be revoked by cancellation");
            }

            throw;
        }
    }
}
