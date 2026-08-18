
namespace CrossMacro.Infrastructure.Services.Recording.Processors;

public class StandardInputEventProcessor(ICoordinateStrategy coordinateStrategy) : IInputEventProcessor
{
    private readonly ICoordinateStrategy _coordinateStrategy = coordinateStrategy;
    private bool _recordMouse;
    private bool _recordKeyboard;
    private IReadOnlySet<int>? _ignoredKeys;
    private bool _isAbsoluteCoordinates;
    private int _lastEmittedX = int.MinValue;
    private int _lastEmittedY = int.MinValue;

    public void Configure(bool recordMouse, bool recordKeyboard, IReadOnlySet<int>? ignoredKeys, bool isAbsoluteCoordinates = false)
    {
        _recordMouse = recordMouse;
        _recordKeyboard = recordKeyboard;
        _ignoredKeys = ignoredKeys;
        _isAbsoluteCoordinates = isAbsoluteCoordinates;
        _lastEmittedX = int.MinValue;
        _lastEmittedY = int.MinValue;
    }

    public MacroEvent? Process(CapturedInputEvent args, long timestamp)
    {
        var pos = _coordinateStrategy.ProcessPosition(args);

        switch (args.Type)
        {
            case InputEventType.MouseMove:
                if (!_recordMouse)
                {
                    return null;
                }

                return ProcessPositionSample(pos, timestamp);

            case InputEventType.MouseScroll:
                if (!_recordMouse)
                {
                    return null;
                }

                if (args.Code == InputEventCode.REL_HWHEEL)
                {
                    return new MacroEvent
                    {
                        Type = EventType.Click,
                        Button = args.Value > 0 ? MacroMouseButton.ScrollRight : MacroMouseButton.ScrollLeft,
                        Timestamp = timestamp,
                        X = pos.HasValue ? pos.X : 0,
                        Y = pos.HasValue ? pos.Y : 0,
                    };
                }

                return new MacroEvent
                {
                    Type = EventType.Click,
                    Button = args.Value > 0 ? MacroMouseButton.ScrollUp : MacroMouseButton.ScrollDown,
                    Timestamp = timestamp,
                    X = pos.HasValue ? pos.X : 0,
                    Y = pos.HasValue ? pos.Y : 0,
                };

            case InputEventType.MouseButton:
                if (!_recordMouse)
                {
                    return null;
                }

                return ProcessMouseButton(
                    args,
                    pos.HasValue ? pos.X : 0,
                    pos.HasValue ? pos.Y : 0,
                    timestamp);

            case InputEventType.Key:
                if (!_recordKeyboard)
                {
                    return null;
                }

                return ProcessKeyEvent(args, timestamp);

            case InputEventType.Sync:
                if (!_recordMouse)
                {
                    return null;
                }

                return ProcessPositionSample(pos, timestamp);

            case InputEventType.Unknown:
                return null;
        }

        return null;
    }

    public MacroEvent? ProcessPositionSample(
        CoordinateSample sample,
        long timestamp,
        CoordinateSampleSpace? coordinateSpace = null)
    {
        if (!_recordMouse || !sample.HasValue)
        {
            return null;
        }

        if (!_isAbsoluteCoordinates && sample.X is 0 && sample.Y is 0)
        {
            return null;
        }

        if (_isAbsoluteCoordinates)
        {
            if (sample.X == _lastEmittedX && sample.Y == _lastEmittedY)
            {
                return null;
            }

            _lastEmittedX = sample.X;
            _lastEmittedY = sample.Y;
        }

        return new MacroEvent
        {
            Type = EventType.MouseMove,
            Timestamp = timestamp,
            X = sample.X,
            Y = sample.Y,
            CoordinateMode = _isAbsoluteCoordinates ? MouseCoordinateMode.Absolute : MouseCoordinateMode.Relative,
            CoordinateSpace = ResolveCoordinateSpace(coordinateSpace),
        };
    }

    private MouseCoordinateSpace ResolveCoordinateSpace(CoordinateSampleSpace? coordinateSpace)
    {
        return coordinateSpace switch
        {
            CoordinateSampleSpace.LogicalDesktop => MouseCoordinateSpace.LogicalDesktop,
            CoordinateSampleSpace.RawDevice => MouseCoordinateSpace.RawDevice,
            _ => _coordinateStrategy.ProducesLogicalCoordinates
                ? MouseCoordinateSpace.LogicalDesktop
                : MouseCoordinateSpace.RawDevice,
        };
    }

    private MacroEvent? ProcessMouseButton(CapturedInputEvent e, int x, int y, long timestamp)
    {
        var buttonEvent = new MacroEvent
        {
            Timestamp = timestamp,
            X = x,
            Y = y,
            CoordinateMode = _isAbsoluteCoordinates ? MouseCoordinateMode.Absolute : MouseCoordinateMode.Relative,
            CoordinateSpace = _coordinateStrategy.ProducesLogicalCoordinates
                ? MouseCoordinateSpace.LogicalDesktop
                : MouseCoordinateSpace.RawDevice,
        };

        if (e.Code == InputEventCode.BTN_LEFT)
        {
            buttonEvent.Button = MacroMouseButton.Left;
        }
        else if (e.Code == InputEventCode.BTN_RIGHT)
        {
            buttonEvent.Button = MacroMouseButton.Right;
        }
        else if (e.Code == InputEventCode.BTN_MIDDLE)
        {
            buttonEvent.Button = MacroMouseButton.Middle;
        }
        else if (e.Code == InputEventCode.BTN_SIDE)
        {
            buttonEvent.Button = MacroMouseButton.Side1;
        }
        else if (e.Code == InputEventCode.BTN_EXTRA)
        {
            buttonEvent.Button = MacroMouseButton.Side2;
        }
        else
        {
            return null;
        }

        buttonEvent.Type = e.Value is 1 ? EventType.ButtonPress : EventType.ButtonRelease;
        return buttonEvent;
    }

    private MacroEvent? ProcessKeyEvent(CapturedInputEvent e, long timestamp)
    {
        if (_ignoredKeys is not null && _ignoredKeys.Contains(e.Code))
        {
            return null;
        }

        if (e.Value is not (0 or 1))
        {
            return null;
        }

        return new MacroEvent
        {
            Timestamp = timestamp,
            Type = e.Value is 1 ? EventType.KeyPress : EventType.KeyRelease,
            KeyCode = e.Code,
            Button = MacroMouseButton.None,
        };
    }
}
