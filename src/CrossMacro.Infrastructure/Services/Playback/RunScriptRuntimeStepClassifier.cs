
namespace CrossMacro.Infrastructure.Services.Playback;

internal static class RunScriptRuntimeStepClassifier
{
    public readonly record struct Requirements(bool IsRuntime, bool ReadsScreen, bool RequiresInput, bool MovesInLogicalDesktop, bool ClicksImage);

    public static Requirements GetRequirements(string step)
    {
        var trimmed = step.TrimStart();
        var separator = trimmed.IndexOf(' ', StringComparison.Ordinal);
        var command = separator < 0 ? string.Empty : trimmed[..separator].ToUpperInvariant();
        var imageClick = string.Equals(command, "IMAGECLICK", StringComparison.Ordinal);
        var input = command is "IMAGECLICK" or "MOVE" or "CLICK" or "DOWN" or "UP" or "SCROLL" or "TAP" or "TYPE" or "KEY"
            || RunScriptClipboardExecutor.TryGetCaptureShortcut(trimmed, out _);
        var logicalMove = false;
        if (string.Equals(command, "MOVE", StringComparison.Ordinal))
        {
            var parts = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            logicalMove = parts.Length >= 2
                && RunScriptSyntax.TryParseMouseMoveMode(parts[1], out _, out var space)
                && space is MouseCoordinateSpace.LogicalDesktop;
        }
        return new Requirements(IsRuntimeStep(step), RunScriptSyntax.IsScreenReadingStep(step), input, logicalMove, imageClick);
    }

    public static bool IsRuntimeStep(string? step)
    {
        var trimmed = step?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return false;
        }

        return RunScriptSyntax.IsScreenReadingStep(trimmed)
            || RunScriptSyntax.IsWindowStep(trimmed)
            || RunScriptSyntax.IsClipboardStep(trimmed)
            || RunScriptSyntax.IsShellStep(trimmed)
            || RunScriptPlatformSyntax.IsScreenshotStep(trimmed)
            || RunScriptSyntax.IsMousePositionStep(trimmed)
            || IsRuntimeDelayStep(trimmed)
            || IsRuntimeVariableStep(trimmed)
            || trimmed.StartsWith("key ", StringComparison.OrdinalIgnoreCase)
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
            || step.StartsWith("dec ", StringComparison.OrdinalIgnoreCase)
            || step.StartsWith("mul ", StringComparison.OrdinalIgnoreCase)
            || step.StartsWith("div ", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsRuntimeBlockHeader(string step)
    {
        return step.EndsWith('{')
            && (step.StartsWith("if ", StringComparison.OrdinalIgnoreCase)
                || step.StartsWith("while ", StringComparison.OrdinalIgnoreCase)
                || step.StartsWith("repeat ", StringComparison.OrdinalIgnoreCase)
                || step.StartsWith("for ", StringComparison.OrdinalIgnoreCase));
    }
}
