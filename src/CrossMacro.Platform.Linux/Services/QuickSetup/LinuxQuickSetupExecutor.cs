using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Core.Services;
using Serilog;

namespace CrossMacro.Platform.Linux.Services.QuickSetup;

internal sealed class LinuxQuickSetupExecutor
{
    private readonly LinuxQuickSetupIdentityResolver _identityResolver;
    private readonly LinuxQuickSetupScriptBuilder _scriptBuilder;
    private readonly Func<ProcessStartInfo, CancellationToken, Task<(int ExitCode, string StdOut, string StdErr)>> _runProcessAsync;

    public LinuxQuickSetupExecutor(
        LinuxQuickSetupIdentityResolver identityResolver,
        LinuxQuickSetupScriptBuilder scriptBuilder)
        : this(identityResolver, scriptBuilder, RunProcessAsync)
    {
    }

    public LinuxQuickSetupExecutor(
        LinuxQuickSetupIdentityResolver identityResolver,
        LinuxQuickSetupScriptBuilder scriptBuilder,
        Func<ProcessStartInfo, CancellationToken, Task<(int ExitCode, string StdOut, string StdErr)>> runProcessAsync)
    {
        _identityResolver = identityResolver ?? throw new ArgumentNullException(nameof(identityResolver));
        _scriptBuilder = scriptBuilder ?? throw new ArgumentNullException(nameof(scriptBuilder));
        _runProcessAsync = runProcessAsync ?? throw new ArgumentNullException(nameof(runProcessAsync));
    }

    public async Task<QuickSetupResult> RunAsync(
        IPrivilegedHostCommandLauncher launcher,
        LinuxQuickSetupScriptOptions scriptOptions,
        string logContext,
        string unexpectedFailureMessage,
        CancellationToken cancellationToken = default)
    {
        if (launcher == null)
        {
            throw new ArgumentNullException(nameof(launcher));
        }

        var identity = _identityResolver.Resolve();
        if (identity == null)
        {
            return new QuickSetupResult(
                Success: false,
                Message: "Could not determine a valid host identity for session setup.");
        }

        if (!launcher.IsAvailable(out var failureMessage))
        {
            return new QuickSetupResult(
                Success: false,
                Message: failureMessage);
        }

        var startInfo = launcher.CreateStartInfo(_scriptBuilder.Build(scriptOptions), identity.Value);

        try
        {
            var (exitCode, stdout, stderr) = await _runProcessAsync(startInfo, cancellationToken);
            if (exitCode == 0)
            {
                Log.Information("[{LogContext}] Session helper completed successfully for {Identity}", logContext, identity.Value.LogDisplay);
                return new QuickSetupResult(
                    Success: true,
                    Message: "Quick setup completed.");
            }

            var errorText = FirstNonEmptyLine(stderr) ?? FirstNonEmptyLine(stdout) ?? "Unknown host setup error.";
            Log.Warning("[{LogContext}] Session helper failed (ExitCode={ExitCode}): {Error}", logContext, exitCode, errorText);
            return new QuickSetupResult(
                Success: false,
                Message: $"Quick setup failed (exit code {exitCode}). {errorText}");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[{LogContext}] Failed to run session helper command", logContext);
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
}
