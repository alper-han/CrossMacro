namespace CrossMacro.Application.Runtime;

/// <summary>
/// Counts the playable units of a macro: recorded input events plus screen-reading script steps.
/// </summary>
public static class MacroPlayableActionCounter
{
    public static int CountPlayableActions(MacroSequence? macro)
    {
        if (macro is null)
        {
            return 0;
        }

        var eventCount = macro.Events?.Count ?? 0;
        var screenReadingStepCount = macro.ScriptSteps?.Count(RunScriptSyntax.IsScreenReadingStep) ?? 0;
        return eventCount + screenReadingStepCount;
    }

    public static bool HasPlayableActions(MacroSequence? macro)
    {
        return CountPlayableActions(macro) > 0;
    }
}
