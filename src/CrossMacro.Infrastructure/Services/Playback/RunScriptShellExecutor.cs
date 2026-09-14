
namespace CrossMacro.Infrastructure.Services.Playback;

internal sealed class RunScriptShellExecutor(
    IShellCommandRunner? shellCommandRunner,
    IPlaybackTimingService timingService,
    IPlaybackPauseToken pauseToken)
{
    internal const string CommandToken = "shell";
    internal const int MaxRetries = RunScriptShellSyntax.MaxRetries;
    internal const int OutputLimitChars = 65_536;
    private const int MaxDiagnosticLength = 4000;

    private readonly IShellCommandRunner? _shellCommandRunner = shellCommandRunner;
    private readonly IPlaybackTimingService _timingService = timingService ?? throw new ArgumentNullException(nameof(timingService));
    private readonly IPlaybackPauseToken _pauseToken = pauseToken ?? throw new ArgumentNullException(nameof(pauseToken));

    public async Task ExecuteStepAsync(string step, int stepNumber, IDictionary<string, string> variables, CancellationToken cancellationToken)
    {
        if (_shellCommandRunner is null)
        {
            throw new InvalidOperationException("Shell script steps require an IShellCommandRunner runtime service.");
        }

        if (!RunScriptShellSyntax.TryParse(step, out var options, out var error) || options == null)
        {
            throw new InvalidOperationException($"Step {stepNumber.ToString(CultureInfo.InvariantCulture)}: {error}");
        }

        var resolvedCommand = ResolveRequiredText(options.Command, variables, stepNumber, "Shell command");
        var resolvedInput = options.StandardInput is null
            ? null
            : RunScriptRuntimeText.ResolveVariables(options.StandardInput, variables, $"Step {stepNumber.ToString(CultureInfo.InvariantCulture)}: ");
        var request = new ShellCommandRequest(resolvedCommand, resolvedInput, OutputLimitChars);
        var timeout = options.TimeoutMs > 0
            ? TimeSpan.FromMilliseconds(options.TimeoutMs)
            : (TimeSpan?)null;
        var totalAttempts = options.Retries + 1;

        for (var attempt = 1; attempt <= totalAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var result = await _shellCommandRunner.RunAsync(request, timeout, cancellationToken).ConfigureAwait(false);
                if (options.CaptureTargets != null)
                {
                    WriteCaptureVariables(options.CaptureTargets, result, variables);
                    return;
                }

                if (result.ExitCode is 0)
                {
                    return;
                }

                if (attempt == totalAttempts)
                {
                    throw BuildExitFailure(stepNumber, attempt, totalAttempts, result);
                }
            }
            catch (ShellCommandTimeoutException ex)
            {
                if (attempt == totalAttempts)
                {
                    throw new TimeoutException(
                        $"Step {stepNumber.ToString(CultureInfo.InvariantCulture)}: shell command attempt {attempt.ToString(CultureInfo.InvariantCulture)}/{totalAttempts.ToString(CultureInfo.InvariantCulture)} timed out after {options.TimeoutMs.ToString(CultureInfo.InvariantCulture)} ms.",
                        ex);
                }
            }

            if (options.BackoffMs > 0)
            {
                await _timingService.WaitAsync(options.BackoffMs, _pauseToken, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    public static string? Validate(string step)
    {
        return RunScriptShellSyntax.TryParse(step, out _, out var error) ? null : error;
    }

    private static string ResolveRequiredText(
        string text,
        IDictionary<string, string> variables,
        int stepNumber,
        string label)
    {
        var resolved = RunScriptRuntimeText.ResolveVariables(text, variables, $"Step {stepNumber.ToString(CultureInfo.InvariantCulture)}: ");
        if (string.IsNullOrWhiteSpace(resolved))
        {
            throw new InvalidOperationException($"Step {stepNumber.ToString(CultureInfo.InvariantCulture)}: {label} cannot be empty.");
        }

        return resolved;
    }

    private static void WriteCaptureVariables(
        RunScriptShellSyntax.ShellCaptureTargets targets,
        ShellCommandResult result,
        IDictionary<string, string> variables)
    {
        WriteCaptureVariable(targets.ExitCodeVariable, result.ExitCode.ToString(CultureInfo.InvariantCulture), variables);
        WriteCaptureVariable(targets.StandardOutputVariable, result.StandardOutput, variables);
        WriteCaptureVariable(targets.StandardErrorVariable, result.StandardError, variables);
    }

    private static void WriteCaptureVariable(string target, string value, IDictionary<string, string> variables)
    {
        if (target is "_")
        {
            return;
        }

        variables[target] = value;
    }

    private static InvalidOperationException BuildExitFailure(
        int stepNumber,
        int attempt,
        int totalAttempts,
        ShellCommandResult result)
    {
        var diagnostics = string.IsNullOrWhiteSpace(result.StandardError)
            ? Truncate(result.StandardOutput.Trim())
            : Truncate(result.StandardError.Trim());
        var streamName = string.IsNullOrWhiteSpace(result.StandardError) ? "stdout" : "stderr";
        var message = $"Step {stepNumber.ToString(CultureInfo.InvariantCulture)}: shell command attempt {attempt.ToString(CultureInfo.InvariantCulture)}/{totalAttempts.ToString(CultureInfo.InvariantCulture)} exited with code {result.ExitCode.ToString(CultureInfo.InvariantCulture)}.";
        if (!string.IsNullOrWhiteSpace(diagnostics))
        {
            message += $" {streamName}: {diagnostics}";
        }

        return new InvalidOperationException(message);
    }

    private static string Truncate(string value)
    {
        return value.Length <= MaxDiagnosticLength
            ? value
            : value[..MaxDiagnosticLength] + "...";
    }

}
