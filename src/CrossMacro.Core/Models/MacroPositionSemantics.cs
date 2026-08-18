
namespace CrossMacro.Core.Models;

/// <summary>
/// Helpers for macros that resolve mouse button position at playback time.
/// </summary>
public static class MacroPositionSemantics
{
    public static bool HasCurrentPositionEvents(MacroSequence macro)
    {
        if (macro is null)
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

        return ev.UseCurrentPosition || (useLegacyInterpretation && ev.X is 0 && ev.Y is 0);
    }

    public static bool IsCoordinateBearing(MacroEvent ev)
    {
        if (ev.Type is EventType.MouseMove)
        {
            return true;
        }

        return IsNonScrollMouseButtonEvent(ev) && !ev.UseCurrentPosition;
    }

    public static bool HasExplicitCoordinateMode(MacroEvent ev)
    {
        return IsCoordinateBearing(ev) && ev.CoordinateMode is not null;
    }

    public static MouseCoordinateMode? ResolveCoordinateMode(MacroEvent ev, bool legacyIsAbsolute)
    {
        if (!IsCoordinateBearing(ev))
        {
            return null;
        }

        return ev.CoordinateMode ?? (legacyIsAbsolute ? MouseCoordinateMode.Absolute : MouseCoordinateMode.Relative);
    }

    public static MouseCoordinateSpace? ResolveCoordinateSpace(MacroEvent ev, bool legacyIsAbsolute)
    {
        return ResolveCoordinateMode(ev, legacyIsAbsolute) switch
        {
            MouseCoordinateMode.Absolute => MouseCoordinateSpace.LogicalDesktop,
            MouseCoordinateMode.Relative => ev.CoordinateSpace ?? MouseCoordinateSpace.RawDevice,
            null => null,
            _ => null,
        };
    }

    public static bool HasAnyLogicalDesktopCoordinateEvents(MacroSequence macro)
    {
        if (macro is null)
        {
            return false;
        }

        return macro.Events.Any(ev =>
            ResolveCoordinateSpace(ev, macro.IsAbsoluteCoordinates) is MouseCoordinateSpace.LogicalDesktop);
    }

    public static bool HasAnyAbsoluteCoordinateEvents(MacroSequence macro)
    {
        if (macro is null)
        {
            return false;
        }

        return macro.Events.Any(ev => ResolveCoordinateMode(ev, macro.IsAbsoluteCoordinates) is MouseCoordinateMode.Absolute);
    }

    public static MouseCoordinateMode? ResolveInitialCoordinateMode(MacroSequence macro)
    {
        if (macro is null)
        {
            return null;
        }

        var firstPositionRelevantEvent = macro.Events.FirstOrDefault(ev =>
            ev.Type is EventType.MouseMove || IsNonScrollMouseButtonEvent(ev));
        return firstPositionRelevantEvent.Type is EventType.None
            ? null
            : ResolveCoordinateMode(firstPositionRelevantEvent, macro.IsAbsoluteCoordinates);
    }

    public static bool RequiresInitialCornerReset(MacroSequence macro)
    {
        return macro is not null
            && !macro.SkipInitialZeroZero
            && ResolveInitialCoordinateMode(macro) is MouseCoordinateMode.Relative;
    }

    public static CoordinateModeSummary GetCoordinateModeSummary(MacroSequence macro)
    {
        if (macro is null)
        {
            return CoordinateModeSummary.None;
        }

        var hasAbsolute = false;
        var hasRelative = false;

        foreach (var ev in macro.Events)
        {
            switch (ResolveCoordinateMode(ev, macro.IsAbsoluteCoordinates))
            {
                case MouseCoordinateMode.Absolute:
                    hasAbsolute = true;
                    break;
                case MouseCoordinateMode.Relative:
                    hasRelative = true;
                    break;
            }

            if (hasAbsolute && hasRelative)
            {
                return CoordinateModeSummary.Mixed;
            }
        }

        if (hasAbsolute)
        {
            return CoordinateModeSummary.Absolute;
        }

        return hasRelative ? CoordinateModeSummary.Relative : CoordinateModeSummary.None;
    }

    public static bool IsLegacyCurrentPositionMacro(MacroSequence macro)
    {
        if (macro is null || macro.IsAbsoluteCoordinates || !macro.SkipInitialZeroZero)
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

            if (ev.Type is EventType.MouseMove)
            {
                if (ev.X is not 0 || ev.Y is not 0)
                {
                    return false;
                }

                continue;
            }

            if (!IsNonScrollMouseButtonEvent(ev))
            {
                continue;
            }

            if (ev.X is not 0 || ev.Y is not 0)
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

    public static bool IsScrollButton(MacroMouseButton button)
    {
        return button is MacroMouseButton.ScrollUp
            or MacroMouseButton.ScrollDown
            or MacroMouseButton.ScrollLeft
            or MacroMouseButton.ScrollRight;
    }
}
