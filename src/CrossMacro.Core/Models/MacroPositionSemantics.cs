using System.Linq;

namespace CrossMacro.Core.Models;

/// <summary>
/// Helpers for macros that resolve mouse button position at playback time.
/// </summary>
public static class MacroPositionSemantics
{
    public static bool HasCurrentPositionEvents(MacroSequence macro)
    {
        if (macro == null)
        {
            return false;
        }

        var useLegacyInterpretation = IsLegacyCurrentPositionMacro(macro);
        return macro.Events.Any(ev => UsesCurrentPosition(ev, useLegacyInterpretation));
    }

    public static bool UsesCurrentPosition(MacroEvent ev, bool useLegacyInterpretation = false)
    {
        if (!IsNonScrollMouseButtonEvent(ev))
        {
            return false;
        }

        return ev.UseCurrentPosition || (useLegacyInterpretation && ev.X == 0 && ev.Y == 0);
    }

    public static bool IsLegacyCurrentPositionMacro(MacroSequence macro)
    {
        if (macro == null
            || macro.IsAbsoluteCoordinates
            || !macro.SkipInitialZeroZero)
        {
            return false;
        }

        var hasLegacyCandidate = false;

        foreach (var ev in macro.Events)
        {
            if (UsesCurrentPosition(ev))
            {
                return false;
            }

            if (ev.Type == EventType.MouseMove)
            {
                if (ev.X != 0 || ev.Y != 0)
                {
                    return false;
                }

                continue;
            }

            if (!IsNonScrollMouseButtonEvent(ev))
            {
                continue;
            }

            if (ev.X != 0 || ev.Y != 0)
            {
                return false;
            }

            hasLegacyCandidate = true;
        }

        return hasLegacyCandidate;
    }

    public static bool IsNonScrollMouseButtonEvent(MacroEvent ev)
    {
        if (ev.Type is not EventType.ButtonPress and not EventType.ButtonRelease and not EventType.Click)
        {
            return false;
        }

        return !IsScrollButton(ev.Button);
    }

    public static bool IsScrollButton(MouseButton button)
    {
        return button is MouseButton.ScrollUp
            or MouseButton.ScrollDown
            or MouseButton.ScrollLeft
            or MouseButton.ScrollRight;
    }
}
