using System;
using CrossMacro.Core.Services;

namespace CrossMacro.Infrastructure.Services.Playback;

internal static class RunScriptRuntimeStepClassifier
{
    public static bool IsRuntimeStep(string step)
    {
        var trimmed = step.Trim();
        return RunScriptSyntax.IsScreenReadingStep(trimmed)
            || RunScriptSyntax.IsWindowStep(trimmed)
            || RunScriptSyntax.IsClipboardStep(trimmed)
            || IsRuntimeDelayStep(trimmed)
            || IsRuntimeVariableStep(trimmed)
            || RunScriptSyntax.IsBreakCommand(trimmed)
            || RunScriptSyntax.IsContinueCommand(trimmed)
            || RunScriptSyntax.IsBlockEndToken(trimmed)
            || RunScriptSyntax.IsElseHeader(trimmed)
            || IsRuntimeBlockHeader(trimmed);
    }

    private static bool IsRuntimeDelayStep(string step)
    {
        return step.StartsWith("delay ", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsRuntimeVariableStep(string step)
    {
        return step.StartsWith("set ", StringComparison.OrdinalIgnoreCase)
            || step.StartsWith("inc ", StringComparison.OrdinalIgnoreCase)
            || step.StartsWith("dec ", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsRuntimeBlockHeader(string step)
    {
        return step.EndsWith("{", StringComparison.Ordinal)
            && (step.StartsWith("if ", StringComparison.OrdinalIgnoreCase)
                || step.StartsWith("while ", StringComparison.OrdinalIgnoreCase)
                || step.StartsWith("repeat ", StringComparison.OrdinalIgnoreCase)
                || step.StartsWith("for ", StringComparison.OrdinalIgnoreCase));
    }
}
