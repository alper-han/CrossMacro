using CrossMacro.Core.Models;

namespace CrossMacro.Infrastructure.Services.Macros;

/// <summary>Derives the cached event counters of a macro in one pass.</summary>
internal static class MacroStatistics
{
    /// <summary>Sets <see cref="MacroSequence.MouseMoveCount"/> and <see cref="MacroSequence.ClickCount"/> (clicks, presses and releases only).</summary>
    public static void Apply(MacroSequence sequence)
    {
        ArgumentNullException.ThrowIfNull(sequence);

        var moves = 0;
        var clicks = 0;
        foreach (var macroEvent in sequence.Events)
        {
            switch (macroEvent.Type)
            {
                case EventType.MouseMove:
                    moves++;
                    break;
                case EventType.Click:
                case EventType.ButtonPress:
                case EventType.ButtonRelease:
                    clicks++;
                    break;
                case EventType.None:
                case EventType.KeyPress:
                case EventType.KeyRelease:
                default:
                    break;
            }
        }

        sequence.MouseMoveCount = moves;
        sequence.ClickCount = clicks;
    }
}
