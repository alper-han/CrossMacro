
namespace CrossMacro.Core.Models.Macros;

/// <summary>
/// Helpers for macros that resolve mouse button position at playback time.
/// </summary>
public static class MacroPositionSemantics
{
    /// <param name="useLegacyInterpretation">
    /// Treat a button event at (0, 0) as "click at the current position". Only macros that
    /// <see cref="IsLegacyCurrentPositionMacro"/> accepts, i.e. files recorded before <see cref="MacroEvent.UseCurrentPosition"/>
    /// was persisted, need this.
    /// </param>
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
        var mode = ResolveCoordinateMode(ev, legacyIsAbsolute);
        if (mode is MouseCoordinateMode.Absolute)
        {
            return MouseCoordinateSpace.LogicalDesktop;
        }

        return mode is MouseCoordinateMode.Relative ? ev.CoordinateSpace ?? MouseCoordinateSpace.RawDevice : null;
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

    /// <summary>
    /// Detects relative macros persisted before <see cref="MacroEvent.UseCurrentPosition"/> existed, where every button
    /// event sat at (0, 0) and meant "current position". Loading marks those events; playback and the editor still call this
    /// for sequences that were built without going through the file reader.
    /// </summary>
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
