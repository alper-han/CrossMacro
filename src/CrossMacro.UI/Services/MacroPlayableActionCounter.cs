using System.Linq;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services;

namespace CrossMacro.UI.Services;

internal static class MacroPlayableActionCounter
{
    public static int CountPlayableActions(MacroSequence? macro)
    {
        if (macro == null)
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
