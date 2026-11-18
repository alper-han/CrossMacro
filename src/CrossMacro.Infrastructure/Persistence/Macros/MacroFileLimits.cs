namespace CrossMacro.Infrastructure.Persistence.Macros;

/// <summary>
/// Resource limits and header tokens of the text macro format.
/// </summary>
/// <remarks>
/// A recording at 1 kHz for half an hour yields about 2M events; each event can be preceded by a
/// fixed and a random wait line. A single line is bounded by the largest embedded image asset.
/// </remarks>
internal static class MacroFileLimits
{
    internal const long MaxMacroFileBytes = 256L * 1024 * 1024;
    internal const int MaxMacroLineChars = ScreenImageAssetPolicy.MaxBase64Chars + 1024;
    // Each event can be preceded by a fixed and a random wait line; a script step can span continuation lines.
    internal const int MaxMacroFileLines = (3 * MaxMacroEvents) + (2 * MaxMacroScriptSteps) + 1_024;
    internal const int MaxMacroScriptSteps = 1_000_000;
    internal const int MaxMacroEvents = 2_000_000;
    internal const int CurrentFormatVersion = 4;
    internal const string FormatVersionPrefix = "CrossMacroFormatV";
    internal const string FormatHeader = "# Format: ";
    internal const string TrailingDelayMicrosecondsHeader = "# TrailingDelayUs: ";
    internal const string TrailingDelayHeader = "# TrailingDelayMs: ";
    internal const string TrailingRandomDelayHeader = "# TrailingRandomDelayMs: ";
    internal const string TextInputBoundaryHeader = "# TextInputBoundaryBase64: ";
    internal const string ActionCommentHeader = "# ActionCommentBase64: ";
    internal const int MaxActionCommentChars = 4096;
    internal const string ImageHeader = "# Image: ";
    internal const string ScriptSectionHeader = "[Script]";
    internal const string EventsSectionHeader = "[Events]";
    internal const string ScriptContinuationPrefix = "| ";
}
