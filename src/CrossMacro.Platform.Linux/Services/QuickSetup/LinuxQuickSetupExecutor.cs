
namespace CrossMacro.Platform.Linux.Services.QuickSetup;

internal sealed class LinuxQuickSetupExecutor(
    LinuxQuickSetupIdentityResolver identityResolver,
    Func<ProcessStartInfo, CancellationToken, Task<(int ExitCode, string StdOut, string StdErr)>> runProcessAsync)
{
    private readonly LinuxQuickSetupIdentityResolver _identityResolver = identityResolver ?? throw new ArgumentNullException(nameof(identityResolver));
    private readonly Func<ProcessStartInfo, CancellationToken, Task<(int ExitCode, string StdOut, string StdErr)>> _runProcessAsync = runProcessAsync ?? throw new ArgumentNullException(nameof(runProcessAsync));

    public LinuxQuickSetupExecutor(
        LinuxQuickSetupIdentityResolver identityResolver)
        : this(identityResolver, RunProcessAsync) { /* Empty */ }

    public async Task<QuickSetupResult> RunAsync(
        IPrivilegedHostCommandLauncher launcher,
        LinuxQuickSetupScriptOptions scriptOptions,
        string logContext,
        string unexpectedFailureMessage,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(launcher);

        var identity = _identityResolver.Resolve();
        if (identity == null)
        {
            return new QuickSetupResult(
                Success: false,
                Message: "Could not determine a valid host identity for session setup.");
        }

        var (isAvailable, failureMessage) = await launcher.IsAvailableAsync(cancellationToken).ConfigureAwait(false);
        if (!isAvailable)
        {
            return new QuickSetupResult(
                Success: false,
                Message: failureMessage);
        }

        var startInfo = launcher.CreateStartInfo(LinuxQuickSetupScriptBuilder.Build(scriptOptions), identity.Value);

        try
        {
            var (exitCode, stdout, stderr) = await _runProcessAsync(startInfo, cancellationToken).ConfigureAwait(false);
            if (exitCode is 0)
            {
                var successText = BuildSuccessMessage(stdout);
                Log.Information("[{LogContext}] Session helper completed successfully for {Identity}", logContext, identity.Value.LogDisplay);
                return new QuickSetupResult(
                    Success: true,
                    Message: successText);
            }

            var errorText = FirstNonEmptyLine(stderr) ?? FirstNonEmptyLine(stdout) ?? "Unknown host setup error.";
            Log.Warning("[{LogContext}] Session helper failed (ExitCode={ExitCode}): {Error}", logContext, exitCode, errorText);
            return new QuickSetupResult(
                Success: false,
                Message: BuildFailureMessage(exitCode, errorText));
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.LogError(ex, "[{LogContext}] Failed to run session helper command", logContext);
            return new QuickSetupResult(
                Success: false,
                Message: unexpectedFailureMessage);
        }
    }

    private static string? FirstNonEmptyLine(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        return content.Split('\n', StringSplitOptions.TrimEntries)
            .FirstOrDefault(static line => !string.IsNullOrWhiteSpace(line));
    }

    private static string BuildSuccessMessage(string stdout)
    {
        var detail = FirstNonEmptyLine(stdout);
        if (string.IsNullOrWhiteSpace(detail))
        {
            return "Quick setup completed.";
        }

        return detail.StartsWith("Quick setup", StringComparison.Ordinal)
            ? detail
            : $"Quick setup completed. {detail}";
    }

    private static string BuildFailureMessage(int exitCode, string errorText)
    {
        var formattedExitCode = exitCode.ToString(CultureInfo.InvariantCulture);
        if (IsPolkitAuthenticationFailure(errorText))
        {
            return "Quick setup could not obtain host authorization "
                + $"(exit code {formattedExitCode}). No usable polkit authentication agent is available "
                + "for this desktop-launched process. Start a host graphical polkit authentication agent or run Quick Setup "
                + $"from a terminal so pkexec can prompt there. Details: {errorText}";
        }

        return $"Quick setup failed (exit code {formattedExitCode}). {errorText}";
    }

    private static bool IsPolkitAuthenticationFailure(string errorText)
    {
        return errorText.Contains("authentication agent", StringComparison.OrdinalIgnoreCase)
            || errorText.Contains("interactive authentication has not been enabled", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<(int ExitCode, string StdOut, string StdErr)> RunProcessAsync(
        ProcessStartInfo startInfo,
        CancellationToken cancellationToken)
    {
        using var process = new Process { StartInfo = startInfo };

        _ = process.Start();

        var stdOutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stdErrTask = process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

        return (process.ExitCode, await stdOutTask.ConfigureAwait(false), await stdErrTask.ConfigureAwait(false));
    }
}
