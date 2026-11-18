using System.Diagnostics.CodeAnalysis;

namespace CrossMacro.Infrastructure.Services.Editing;

/// <summary>Projects shell and screenshot script steps into editable actions.</summary>
internal static class EditorCommandScriptReader
{
    internal static bool TryParseShellStep(string step, [NotNullWhen(true)] out EditorAction? action)
    {
        action = null;
        if (!RunScriptShellSyntax.TryParse(step, out var parsed, out _) || parsed is null)
        {
            return false;
        }

        var payload = step.Trim()[RunScriptSyntax.ShellCommand.Length..].TrimStart();
        // Keep unquoted legacy command text opaque so editing does not rewrite its quoting.
        if (parsed.CaptureTargets is null && parsed.StandardInput is null && payload[0] is not ('"' or '\''))
        {
            return false;
        }

        var mode = (parsed.CaptureTargets is not null, parsed.StandardInput is not null) switch
        {
            (true, true) => ShellCommandMode.ShellCaptureInput,
            (true, false) => ShellCommandMode.ShellCapture,
            (false, true) => ShellCommandMode.ShellInput,
            _ => ShellCommandMode.Shell,
        };
        action = new EditorAction
        {
            Type = EditorActionType.ShellCommand,
            ShellCommandMode = mode,
            ShellCommand = parsed.Command,
            ShellStandardInput = parsed.StandardInput ?? string.Empty,
            ShellExitCodeVariableName = parsed.CaptureTargets?.ExitCodeVariable ?? "exit_code",
            ShellStandardOutputVariableName = parsed.CaptureTargets?.StandardOutputVariable ?? "stdout",
            ShellStandardErrorVariableName = parsed.CaptureTargets?.StandardErrorVariable ?? "stderr",
            ShellRetries = parsed.Retries,
            ShellBackoffMs = parsed.BackoffMs,
            ShellTimeoutMs = parsed.TimeoutMs,
        };
        return true;
    }

    internal static bool TryParseScreenshotStep(string step, [NotNullWhen(true)] out EditorAction? action)
    {
        action = null;
        if (!RunScriptSyntax.IsScreenshotStep(step) || !RunScriptPlatformSyntax.TryParseScreenshotStep(step, out var parsed, out _))
        {
            return false;
        }

        action = new EditorAction
        {
            Type = EditorActionType.Screenshot,
            ScreenshotOutputPath = parsed.OutputPath ?? string.Empty,
            ScreenshotCopyToClipboard = parsed.CopyToClipboard,
            ScreenshotUseRegion = parsed.UseRegion,
            ScreenshotRegionX = parsed.UseRegion ? parsed.RegionX : "0",
            ScreenshotRegionY = parsed.UseRegion ? parsed.RegionY : "0",
            ScreenshotRegionWidth = parsed.UseRegion ? parsed.RegionWidth : "100",
            ScreenshotRegionHeight = parsed.UseRegion ? parsed.RegionHeight : "100",
        };
        return true;
    }
}
