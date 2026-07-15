using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services;

namespace CrossMacro.Infrastructure.Services.Playback;

/// <summary>
/// Executes "clipboard get/set" script steps at runtime using the platform's IClipboardService.
/// </summary>
internal sealed class RunScriptClipboardExecutor
{
    private readonly IClipboardService? _clipboardService;

    internal const string CommandToken = "clipboard";

    public RunScriptClipboardExecutor(IClipboardService? clipboardService)
    {
        _clipboardService = clipboardService;
    }

    public async Task ExecuteStepAsync(string step, int stepNumber, IDictionary<string, string> variables, CancellationToken cancellationToken)
    {
        if (_clipboardService is null)
        {
            throw new InvalidOperationException("Clipboard script steps require an IClipboardService runtime service.");
        }

        if (!_clipboardService.IsSupported)
        {
            throw new InvalidOperationException($"Step {stepNumber}: Clipboard script steps require a supported IClipboardService runtime service.");
        }

        var trimmedStep = step.Trim();
        var parts = trimmedStep.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 2)
        {
            throw new InvalidOperationException($"Step {stepNumber}: Syntax: {CommandToken} get <var> | {CommandToken} set <text>");
        }

        var subCommand = parts[1].ToLowerInvariant();
        if (subCommand is "get")
        {
            if (parts.Length is not 3)
            {
                throw new InvalidOperationException($"Step {stepNumber}: Syntax: {CommandToken} get <var>");
            }

            var varName = RunScriptRuntimeText.NormalizeAndValidateVariableName(parts[2]);
            var clipboardText = await _clipboardService.GetTextAsync(cancellationToken).ConfigureAwait(false);
            variables[varName] = clipboardText ?? string.Empty;
            return;
        }

        if (subCommand is not "set")
        {
            throw new InvalidOperationException($"Step {stepNumber}: Unknown clipboard subcommand: {subCommand}");
        }

        var rawText = ExtractSetPayload(trimmedStep, stepNumber);
        var resolvedText = RunScriptRuntimeText.ResolveVariables(rawText, variables, $"Step {stepNumber}: ");
        var text = RunScriptRuntimeText.Unquote(resolvedText);

        try
        {
            await _clipboardService.SetTextAsync(text, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Step {stepNumber}: Failed to set clipboard text.", ex);
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
            throw new InvalidOperationException($"Step {stepNumber}: Syntax: {CommandToken} set <text>");
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

    public static string? Validate(string step)
    {
        var parts = step.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 2 || !parts[0].Equals(CommandToken, StringComparison.OrdinalIgnoreCase))
        {
            return $"Syntax: {CommandToken} get <var> | {CommandToken} set <text>";
        }

        var subCommand = parts[1].ToLowerInvariant();
        if (subCommand is "get")
        {
            if (parts.Length is not 3) return $"Syntax: {CommandToken} get <var>";
            var variableName = EditorActionScriptTokens.NormalizeVariableToken(parts[2]);
            if (!EditorActionScriptTokens.IsValidVariableName(variableName))
            {
                return $"Invalid variable name '{parts[2]}'. Allowed pattern: [A-Za-z_][A-Za-z0-9_]*";
            }

            return null;
        }

        if (subCommand is "set")
        {
            if (parts.Length < 3) return $"Syntax: {CommandToken} set <text>";
            return null;
        }

        return $"Unknown clipboard subcommand: {subCommand}";
    }
}
