
namespace CrossMacro.Infrastructure.Services.Playback;

/// <summary>
/// Executes "clipboard get/set" script steps at runtime using the platform's IClipboardService.
/// </summary>
internal sealed class RunScriptClipboardExecutor(IClipboardService? clipboardService)
{
    private readonly IClipboardService? _clipboardService = clipboardService;

    internal const string CommandToken = "clipboard";

    internal const string CaptureSubcommandToken = "capture";

    internal const int CaptureSettleDelayMilliseconds = 10;

    private const string GetSyntax = "clipboard get <var>";
    private const string SetSyntax = "clipboard set <text>";
    private const string CaptureSyntax = "clipboard capture <ctrl+c|ctrl+shift+c> <var>";
    private const string AllSyntax = GetSyntax + " | " + SetSyntax + " | " + CaptureSyntax;

    public void EnsureSupported(int stepNumber)
    {
        _ = GetSupportedClipboardService(stepNumber);
    }

    public async Task ExecuteStepAsync(string step, int stepNumber, IDictionary<string, string> variables, CancellationToken cancellationToken)
    {
        var clipboard = GetSupportedClipboardService(stepNumber);

        var trimmedStep = step.Trim();
        var parts = trimmedStep.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 2)
        {
            throw new InvalidOperationException($"Step {stepNumber.ToString(CultureInfo.InvariantCulture)}: Syntax: {AllSyntax}");
        }

        var subCommand = parts[1].ToUpperInvariant();
        if (subCommand is "GET")
        {
            if (parts.Length is not 3)
            {
                throw new InvalidOperationException($"Step {stepNumber.ToString(CultureInfo.InvariantCulture)}: Syntax: {GetSyntax}");
            }

            var varName = RunScriptRuntimeText.NormalizeAndValidateVariableName(parts[2]);
            var clipboardText = await clipboard.GetTextAsync(cancellationToken).ConfigureAwait(false);
            variables[varName] = clipboardText ?? string.Empty;
            return;
        }

        if (subCommand.Equals(CaptureSubcommandToken, StringComparison.OrdinalIgnoreCase))
        {
            if (!TryParseCaptureStep(trimmedStep, out _, out var varName))
            {
                throw new InvalidOperationException($"Step {stepNumber.ToString(CultureInfo.InvariantCulture)}: Syntax: {CaptureSyntax}");
            }

            var clipboardText = await clipboard.GetTextAsync(cancellationToken).ConfigureAwait(false);
            variables[varName] = clipboardText ?? string.Empty;
            return;
        }

        if (subCommand is not "SET")
        {
            throw new InvalidOperationException($"Step {stepNumber.ToString(CultureInfo.InvariantCulture)}: Unknown clipboard subcommand: {subCommand}");
        }

        var rawText = ExtractSetPayload(trimmedStep, stepNumber);
        var resolvedText = RunScriptRuntimeText.ResolveVariables(rawText, variables, $"Step {stepNumber.ToString(CultureInfo.InvariantCulture)}: ");
        var text = RunScriptRuntimeText.Unquote(resolvedText);

        try
        {
            await clipboard.SetTextAsync(text, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            throw new InvalidOperationException($"Step {stepNumber.ToString(CultureInfo.InvariantCulture)}: Failed to set clipboard text.", ex);
        }
    }

    private static string ExtractSetPayload(string trimmedStep, int stepNumber)
    {
        var commandEnd = CommandToken.Length;
        var subCommandStart = SkipWhiteSpace(trimmedStep, commandEnd);
        var subCommandEnd = subCommandStart + "set".Length;
        var payloadStart = SkipWhiteSpace(trimmedStep, subCommandEnd);
        if (payloadStart >= trimmedStep.Length)
        {
            throw new InvalidOperationException($"Step {stepNumber.ToString(CultureInfo.InvariantCulture)}: Syntax: {SetSyntax}");
        }

        return trimmedStep[payloadStart..];
    }

    private static int SkipWhiteSpace(string value, int start)
    {
        var index = start;
        while (index < value.Length && char.IsWhiteSpace(value[index]))
        {
            index++;
        }

        return index;
    }

    private IClipboardService GetSupportedClipboardService(int stepNumber)
    {
        if (_clipboardService is null)
        {
            throw new InvalidOperationException("Clipboard script steps require an IClipboardService runtime service.");
        }

        if (!_clipboardService.IsSupported)
        {
            throw new InvalidOperationException($"Step {stepNumber.ToString(CultureInfo.InvariantCulture)}: Clipboard script steps require a supported IClipboardService runtime service.");
        }

        return _clipboardService;
    }

    public static string? Validate(string step)
    {
        var parts = step.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 2 || !parts[0].Equals(CommandToken, StringComparison.OrdinalIgnoreCase))
        {
            return $"Syntax: {AllSyntax}";
        }

        var subCommand = parts[1].ToUpperInvariant();
        if (subCommand is "GET")
        {
            if (parts.Length is not 3)
            {
                return $"Syntax: {GetSyntax}";
            }

            var variableName = EditorActionScriptTokens.NormalizeVariableToken(parts[2]);
            if (!EditorActionScriptTokens.IsValidVariableName(variableName))
            {
                return $"Invalid variable name '{parts[2]}'. Allowed pattern: [A-Za-z_][A-Za-z0-9_]*";
            }

            return null;
        }

        if (subCommand is "SET")
        {
            if (parts.Length < 3)
            {
                return $"Syntax: {SetSyntax}";
            }

            return null;
        }

        if (subCommand.Equals(CaptureSubcommandToken, StringComparison.OrdinalIgnoreCase))
        {
            return TryParseCaptureStep(step, out _, out _)
                ? null
                : $"Syntax: {CaptureSyntax}";
        }

        return $"Unknown clipboard subcommand: {subCommand}";
    }

    public static bool TryGetCaptureShortcut(string step, out string shortcut)
    {
        var isCapture = TryParseCaptureStep(step, out var parsedShortcut, out _);
        shortcut = isCapture ? ClipboardCopyShortcutSyntax.ToScriptToken(parsedShortcut) : string.Empty;
        return isCapture;
    }

    private static bool TryParseCaptureStep(string step, out ClipboardCopyShortcut shortcut, out string variableName)
    {
        shortcut = default;
        variableName = string.Empty;
        var parts = step.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length is not 4
            || !parts[0].Equals(CommandToken, StringComparison.OrdinalIgnoreCase)
            || !parts[1].Equals(CaptureSubcommandToken, StringComparison.OrdinalIgnoreCase)
            || !ClipboardCopyShortcutSyntax.TryParse(parts[2], out shortcut))
        {
            return false;
        }

        variableName = EditorActionScriptTokens.NormalizeVariableToken(parts[3]);
        return EditorActionScriptTokens.IsValidVariableName(variableName);
    }
}
