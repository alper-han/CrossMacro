namespace CrossMacro.Infrastructure.Services.Scripting;

/// <summary>
/// Compiles <c>move</c>, <c>down</c>/<c>up</c>/<c>click</c> and <c>scroll</c> steps. It remembers where the
/// last <c>move</c> left the cursor so a button step without an explicit position acts there.
/// </summary>
internal sealed class RunScriptMouseEmitter(RunScriptEventTimeline timeline)
{
    private MouseCoordinateMode? _moveMode;
    private MouseCoordinateSpace? _moveSpace;
    private bool _hasAbsolutePosition;
    private int _absoluteX;
    private int _absoluteY;

    public bool TryEmitMove(string step, out string? error)
    {
        if (!RunScriptInputSyntax.TryParseMove(step, out var mode, out var space, out var x, out var y, out error))
        {
            return false;
        }

        if (error is not null)
        {
            return true;
        }

        _moveMode = mode;
        _moveSpace = space;
        timeline.Emit(new MacroEvent
        {
            Type = EventType.MouseMove,
            X = x,
            Y = y,
            CoordinateMode = mode,
            CoordinateSpace = space,
        });

        _hasAbsolutePosition = mode is MouseCoordinateMode.Absolute;
        if (_hasAbsolutePosition)
        {
            _absoluteX = x;
            _absoluteY = y;
        }

        return true;
    }

    public bool TryEmitButton(string step, out string? error) =>
        TryEmitButton(step, "down", EventType.ButtonPress, out error)
        || TryEmitButton(step, "up", EventType.ButtonRelease, out error)
        || TryEmitButton(step, "click", EventType.Click, out error);

    public bool TryEmitScroll(string step, out string? error)
    {
        if (!RunScriptInputSyntax.TryParseScroll(step, out var button, out var count, out error))
        {
            return false;
        }

        if (error is not null)
        {
            return true;
        }

        for (var i = 0; i < count; i++)
        {
            timeline.Emit(new MacroEvent
            {
                Type = EventType.Click,
                Button = button,
            });
        }

        return true;
    }

    private bool TryEmitButton(string step, string command, EventType eventType, out string? error)
    {
        if (!RunScriptInputSyntax.TryParseButton(step, command, out var button, out var useCurrentPosition, out error))
        {
            return false;
        }

        if (error is not null)
        {
            return true;
        }

        var buttonEvent = new MacroEvent
        {
            Type = eventType,
            Button = button,
            UseCurrentPosition = useCurrentPosition || _moveMode is null,
        };

        if (!useCurrentPosition && !TryPlaceAt(ref buttonEvent, command, out error))
        {
            return true;
        }

        timeline.Emit(buttonEvent);
        return true;
    }

    private bool TryPlaceAt(ref MacroEvent buttonEvent, string command, out string? error)
    {
        error = null;
        if (_moveMode is MouseCoordinateMode.Absolute)
        {
            if (!_hasAbsolutePosition)
            {
                error = $"{command} <button> requires a prior 'move abs <x> <y>' step in absolute mode.";
                return false;
            }

            buttonEvent.X = _absoluteX;
            buttonEvent.Y = _absoluteY;
            buttonEvent.CoordinateMode = MouseCoordinateMode.Absolute;
            buttonEvent.CoordinateSpace = MouseCoordinateSpace.LogicalDesktop;
        }
        else if (_moveMode is MouseCoordinateMode.Relative)
        {
            buttonEvent.CoordinateMode = MouseCoordinateMode.Relative;
            buttonEvent.CoordinateSpace = _moveSpace ?? MouseCoordinateSpace.LogicalDesktop;
        }

        return true;
    }
}
