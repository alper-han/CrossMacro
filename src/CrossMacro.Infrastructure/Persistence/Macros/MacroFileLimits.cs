namespace CrossMacro.Infrastructure.Persistence.Macros;

/// <summary>
/// Compatibility and resource limits for the legacy text macro format.
/// </summary>
internal static class MacroFileLimits
{
    internal const long MaxMacroFileBytes = 2L * 1024 * 1024 * 1024;
    internal const int MaxMacroLineChars = 128 * 1024 * 1024;
    internal const int MaxMacroFileLines = 10_000_000;
    internal const int MaxMacroScriptSteps = 10_000_000;
    internal const int MaxMacroEvents = 10_000_000;
    internal const string TrailingDelayMicrosecondsHeader = "# TrailingDelayUs: ";
    internal const string TrailingDelayHeader = "# TrailingDelayMs: ";
    internal const string TrailingRandomDelayHeader = "# TrailingRandomDelayMs: ";
    internal const string TextInputBoundaryHeader = "# TextInputBoundaryBase64: ";
    internal const string ImageHeader = "# Image: ";
    internal const string ScriptSectionHeader = "[Script]";
    internal const string EventsSectionHeader = "[Events]";
    internal const string ScriptContinuationPrefix = "| ";
}
