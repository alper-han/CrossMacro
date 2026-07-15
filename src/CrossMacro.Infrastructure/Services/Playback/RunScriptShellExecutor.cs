
namespace CrossMacro.Infrastructure.Services.Playback;

internal sealed class RunScriptShellExecutor
{
    internal const string CommandToken = "shell";
    internal const int MaxRetries = 10_000;
    internal const int OutputLimitChars = 65_536;
    private const int MaxDiagnosticLength = 4000;

    private readonly IShellCommandRunner? _shellCommandRunner;
    private readonly IPlaybackTimingService _timingService;
    private readonly IPlaybackPauseToken _pauseToken;

    public RunScriptShellExecutor(
        IShellCommandRunner? shellCommandRunner,
        IPlaybackTimingService timingService,
        IPlaybackPauseToken pauseToken)
    {
        _shellCommandRunner = shellCommandRunner;
        _timingService = timingService ?? throw new ArgumentNullException(nameof(timingService));
        _pauseToken = pauseToken ?? throw new ArgumentNullException(nameof(pauseToken));
    }

    public async Task ExecuteStepAsync(string step, int stepNumber, IDictionary<string, string> variables, CancellationToken cancellationToken)
    {
        if (_shellCommandRunner is null)
        {
            throw new InvalidOperationException("Shell script steps require an IShellCommandRunner runtime service.");
        }

        if (!TryParse(step, out var options, out var error) || options == null)
        {
            throw new InvalidOperationException($"Step {stepNumber}: {error}");
        }

        var resolvedCommand = ResolveRequiredText(options.Command, variables, stepNumber, "Shell command");
        var resolvedInput = options.StandardInput is null
            ? null
            : RunScriptRuntimeText.ResolveVariables(options.StandardInput, variables, $"Step {stepNumber}: ");
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
                        $"Step {stepNumber}: shell command attempt {attempt}/{totalAttempts} timed out after {options.TimeoutMs} ms.",
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
        return TryParse(step, out _, out var error) ? null : error;
    }

    private static string ResolveRequiredText(
        string text,
        IDictionary<string, string> variables,
        int stepNumber,
        string label)
    {
        var resolved = RunScriptRuntimeText.ResolveVariables(text, variables, $"Step {stepNumber}: ");
        if (string.IsNullOrWhiteSpace(resolved))
        {
            throw new InvalidOperationException($"Step {stepNumber}: {label} cannot be empty.");
        }

        return resolved;
    }

    private static void WriteCaptureVariables(
        ShellCaptureTargets targets,
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
        var message = $"Step {stepNumber}: shell command attempt {attempt}/{totalAttempts} exited with code {result.ExitCode}.";
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

    private static bool TryParse(string step, out ShellCommandOptions? options, out string error)
    {
        options = null;
        error = string.Empty;
        if (!RunScriptSyntax.IsShellStep(step))
        {
            error = SyntaxText();
            return false;
        }

        var payloadStart = CommandToken.Length;
        var trimmedStep = step.Trim();
        var payload = trimmedStep.Length > payloadStart
            ? trimmedStep[payloadStart..].TrimStart()
            : string.Empty;
        if (payload.Length is 0)
        {
            error = "Shell command cannot be empty.";
            return false;
        }

        if (TryConsumeMode(payload, "capture-input", out var afterCaptureInput))
        {
            return TryParseCaptureInput(afterCaptureInput, out options, out error);
        }

        if (TryConsumeMode(payload, "capture", out var afterCapture))
        {
            return TryParseCapture(afterCapture, out options, out error);
        }

        if (TryConsumeMode(payload, "input", out var afterInput))
        {
            return TryParseInput(afterInput, out options, out error);
        }

        if (payload[0] is '"' or '\'')
        {
            return TryParseNormalQuoted(payload, out options, out error);
        }

        return TryParseUnquoted(payload, out options, out error);
    }

    private static bool TryConsumeMode(string payload, string mode, out string remaining)
    {
        remaining = string.Empty;
        if (!payload.StartsWith(mode, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (payload.Length != mode.Length && !char.IsWhiteSpace(payload[mode.Length]))
        {
            return false;
        }

        remaining = payload[mode.Length..].TrimStart();
        return true;
    }

    private static bool TryParseCaptureInput(string payload, out ShellCommandOptions? options, out string error)
    {
        options = null;
        if (!TryReadQuotedFieldToken(payload, allowEmpty: true, out var standardInput, out var afterInput, out error)
            || !TryReadQuotedFieldToken(afterInput, allowEmpty: false, out var command, out var afterCommand, out error)
            || !TryReadCaptureTargets(afterCommand, out var targets, out var optionTokens, out error))
        {
            return false;
        }

        return TryParseOptions(command, standardInput, targets, optionTokens, out options, out error);
    }

    private static bool TryParseCapture(string payload, out ShellCommandOptions? options, out string error)
    {
        options = null;
        if (!TryReadQuotedFieldToken(payload, allowEmpty: false, out var command, out var afterCommand, out error)
            || !TryReadCaptureTargets(afterCommand, out var targets, out var optionTokens, out error))
        {
            return false;
        }

        return TryParseOptions(command, standardInput: null, targets, optionTokens, out options, out error);
    }

    private static bool TryParseInput(string payload, out ShellCommandOptions? options, out string error)
    {
        options = null;
        if (!TryReadQuotedFieldToken(payload, allowEmpty: true, out var standardInput, out var afterInput, out error)
            || !TryReadQuotedFieldToken(afterInput, allowEmpty: false, out var command, out var afterCommand, out error))
        {
            return false;
        }

        var optionTokens = SplitTokens(afterCommand.Trim());
        return TryParseOptions(command, standardInput, targets: null, optionTokens, out options, out error);
    }

    private static bool TryParseNormalQuoted(string payload, out ShellCommandOptions? options, out string error)
    {
        options = null;
        if (!TryReadQuotedFieldToken(payload, allowEmpty: false, out var command, out var afterCommand, out error))
        {
            return false;
        }

        var optionTokens = SplitTokens(afterCommand.Trim());
        return TryParseOptions(command, standardInput: null, targets: null, optionTokens, out options, out error);
    }

    private static bool TryReadQuotedFieldToken(
        string payload,
        bool allowEmpty,
        out string value,
        out string remaining,
        out string error)
    {
        value = string.Empty;
        remaining = string.Empty;
        error = string.Empty;
        var trimmed = payload.TrimStart();
        if (trimmed.Length is 0 || trimmed[0] is not ('"' or '\''))
        {
            error = SyntaxText();
            return false;
        }

        var quote = trimmed[0];
        if (!TryReadQuotedField(trimmed, quote, out value, out var endQuote))
        {
            error = SyntaxText();
            return false;
        }

        if (endQuote + 1 < trimmed.Length && !char.IsWhiteSpace(trimmed[endQuote + 1]))
        {
            error = SyntaxText();
            return false;
        }

        if (!allowEmpty && string.IsNullOrWhiteSpace(value))
        {
            error = "Shell command cannot be empty.";
            return false;
        }

        remaining = trimmed[(endQuote + 1)..].TrimStart();
        return true;
    }

    private static bool TryReadQuotedField(string payload, char quote, out string value, out int endQuote)
    {
        var builder = new StringBuilder();
        for (var i = 1; i < payload.Length; i++)
        {
            var current = payload[i];
            if (current == '\\' && i + 1 < payload.Length && (payload[i + 1] == quote || payload[i + 1] == '\\'))
            {
                builder.Append(payload[i + 1]);
                i++;
                continue;
            }

            if (current == quote)
            {
                value = builder.ToString();
                endQuote = i;
                return true;
            }

            builder.Append(current);
        }

        value = string.Empty;
        endQuote = -1;
        return false;
    }

    private static bool TryReadCaptureTargets(
        string payload,
        out ShellCaptureTargets? targets,
        out string[] optionTokens,
        out string error)
    {
        targets = null;
        optionTokens = [];
        error = string.Empty;
        var tokens = SplitTokens(payload);
        if (tokens.Length < 3)
        {
            error = SyntaxText();
            return false;
        }

        if (!TryValidateCaptureTarget(tokens[0], out error)
            || !TryValidateCaptureTarget(tokens[1], out error)
            || !TryValidateCaptureTarget(tokens[2], out error))
        {
            return false;
        }

        targets = new ShellCaptureTargets(tokens[0], tokens[1], tokens[2]);
        optionTokens = tokens[3..];
        return true;
    }

    private static bool TryValidateCaptureTarget(string target, out string error)
    {
        error = string.Empty;
        if (target is "_")
        {
            return true;
        }

        try
        {
            RunScriptRuntimeText.EnsureValidVariableName(target);
            return true;
        }
        catch (InvalidOperationException ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static bool TryParseUnquoted(string payload, out ShellCommandOptions? options, out string error)
    {
        options = null;
        error = string.Empty;
        var tokens = SplitTokens(payload);
        if (tokens.Length > 1 && LooksLikeInteger(tokens[^1]))
        {
            error = "Quote the shell command when using retries, backoff_ms, or timeout_ms so numeric command arguments are not ambiguous.";
            return false;
        }

        options = new ShellCommandOptions(payload, StandardInput: null, CaptureTargets: null, Retries: 0, BackoffMs: 0, TimeoutMs: 0);
        return true;
    }

    private static bool TryParseOptions(
        string command,
        string? standardInput,
        ShellCaptureTargets? targets,
        string[] optionTokens,
        out ShellCommandOptions? options,
        out string error)
    {
        options = null;
        error = string.Empty;
        if (optionTokens.Length > 3)
        {
            error = SyntaxText();
            return false;
        }

        var values = new[] { 0, 0, 0 };
        var names = new[] { "retries", "backoff_ms", "timeout_ms" };
        for (var i = 0; i < optionTokens.Length; i++)
        {
            if (!int.TryParse(optionTokens[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) || value < 0)
            {
                error = $"Invalid {names[i]} '{optionTokens[i]}'. Expected integer >= 0.";
                return false;
            }

            if (i is 0 && value > MaxRetries)
            {
                error = $"Invalid retries '{optionTokens[i]}'. Expected integer between 0 and {MaxRetries}.";
                return false;
            }

            values[i] = value;
        }

        options = new ShellCommandOptions(command, standardInput, targets, values[0], values[1], values[2]);
        return true;
    }

    private static string[] SplitTokens(string payload)
    {
        return payload.Trim().Length is 0
            ? []
            : payload.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static bool LooksLikeInteger(string token)
    {
        return int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out _);
    }

    private static string SyntaxText()
    {
        return $"Syntax: {CommandToken} \"<command>\" [retries] [backoff_ms] [timeout_ms] | {CommandToken} capture \"<command>\" exitVar stdoutVar stderrVar [retries] [backoff_ms] [timeout_ms] | {CommandToken} input \"<stdin text>\" \"<command>\" [retries] [backoff_ms] [timeout_ms] | {CommandToken} capture-input \"<stdin text>\" \"<command>\" exitVar stdoutVar stderrVar [retries] [backoff_ms] [timeout_ms]";
    }

    private sealed record ShellCommandOptions(
        string Command,
        string? StandardInput,
        ShellCaptureTargets? CaptureTargets,
        int Retries,
        int BackoffMs,
        int TimeoutMs);

    private sealed record ShellCaptureTargets(
        string ExitCodeVariable,
        string StandardOutputVariable,
        string StandardErrorVariable);
}
