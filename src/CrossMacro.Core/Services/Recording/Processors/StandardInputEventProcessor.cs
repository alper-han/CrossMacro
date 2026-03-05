using System;
using System.Collections.Generic;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services.Recording.Strategies;
using CrossMacro.Core.Logging;

namespace CrossMacro.Core.Services.Recording.Processors;

public class StandardInputEventProcessor : IInputEventProcessor
{
    private readonly ICoordinateStrategy _coordinateStrategy;
    private bool _recordMouse;
    private bool _recordKeyboard;
    private HashSet<int>? _ignoredKeys;
    
    private bool _isAbsoluteCoordinates;
    private int _lastEmittedX = int.MinValue;
    private int _lastEmittedY = int.MinValue;

    public StandardInputEventProcessor(ICoordinateStrategy coordinateStrategy)
    {
        _coordinateStrategy = coordinateStrategy;
    }

    public void Configure(bool recordMouse, bool recordKeyboard, HashSet<int>? ignoredKeys, bool isAbsoluteCoordinates = false)
    {
        _recordMouse = recordMouse;
        _recordKeyboard = recordKeyboard;
        _ignoredKeys = ignoredKeys;
        _isAbsoluteCoordinates = isAbsoluteCoordinates;
        
        // Reset state
        _lastEmittedX = int.MinValue;
        _lastEmittedY = int.MinValue;
    }

    public MacroEvent? Process(InputCaptureEventArgs args, long timestamp)
    {
        // Let the strategy update its state with every event (e.g. tracking deltas)
        // even if we might filter it out properly later.
        var pos = _coordinateStrategy.ProcessPosition(args);

        switch (args.Type)
        {
            case InputEventType.MouseMove:
                if (!_recordMouse) return null;
                
                // Skip zero-delta moves (strategy is buffering, waiting for SYNC)
                // Also filters out suppressed Sync events (0,0) for Absolute strategies
                if (pos.X == 0 && pos.Y == 0) return null;
                
                if (_isAbsoluteCoordinates)
                {
                    // Deduplication for Absolute Mode to reduce event count
                    if (pos.X == _lastEmittedX && pos.Y == _lastEmittedY)
                        return null;
                        
                    _lastEmittedX = pos.X;
                    _lastEmittedY = pos.Y;
                }
                
                return new MacroEvent
                {
                    Type = EventType.MouseMove,
                    Timestamp = timestamp,
                    X = pos.X,
                    Y = pos.Y
                };

            case InputEventType.MouseScroll:
                if (!_recordMouse) return null;
                
                // Code 1 = Horizontal, Code 0 (or others) = Vertical
                if (args.Code == 1)
                {
                    return new MacroEvent
                    {
                        Type = EventType.Click,
                        Button = args.Value > 0 ? MouseButton.ScrollRight : MouseButton.ScrollLeft,
                        Timestamp = timestamp,
                        X = pos.X,
                        Y = pos.Y
                    };
                }
                
                return new MacroEvent
                {
                    Type = EventType.Click,
                    Button = args.Value > 0 ? MouseButton.ScrollUp : MouseButton.ScrollDown,
                    Timestamp = timestamp,
                    X = pos.X,
                    Y = pos.Y
                };

            case InputEventType.MouseButton:
                if (!_recordMouse) return null;
                return ProcessMouseButton(args, pos.X, pos.Y, timestamp);

            case InputEventType.Key:
                if (!_recordKeyboard) return null;
                return ProcessKeyEvent(args, timestamp);

            case InputEventType.Sync:
                // For relative mode: SYNC triggers flush of buffered deltas
                if (!_recordMouse) return null;
                if (pos.X == 0 && pos.Y == 0) return null;
                
                return new MacroEvent
                {
                    Type = EventType.MouseMove,
                    Timestamp = timestamp,
                    X = pos.X,
                    Y = pos.Y
                };

            case InputEventType.Unknown:
                return null;
        }

        return null;
    }

    private MacroEvent? ProcessMouseButton(InputCaptureEventArgs e, int x, int y, long timestamp)
    {
        var buttonEvent = new MacroEvent
        {
            Timestamp = timestamp,
            X = x,
            Y = y
        };

        if (e.Code == InputEventCode.BTN_LEFT) buttonEvent.Button = MouseButton.Left;
        else if (e.Code == InputEventCode.BTN_RIGHT) buttonEvent.Button = MouseButton.Right;
        else if (e.Code == InputEventCode.BTN_MIDDLE) buttonEvent.Button = MouseButton.Middle;
        else if (e.Code == InputEventCode.BTN_SIDE) buttonEvent.Button = MouseButton.Side1;
        else if (e.Code == InputEventCode.BTN_EXTRA) buttonEvent.Button = MouseButton.Side2;
        else return null; // Ignore extra buttons

        buttonEvent.Type = e.Value == 1 ? EventType.ButtonPress : EventType.ButtonRelease;
        return buttonEvent;
    }

    private MacroEvent? ProcessKeyEvent(InputCaptureEventArgs e, long timestamp)
    {
        if (_ignoredKeys != null && _ignoredKeys.Contains(e.Code))
        {
            return null;
        }
        

        if (e.Value != 0 && e.Value != 1) return null;

        return new MacroEvent
        {
            Timestamp = timestamp,
            Type = e.Value == 1 ? EventType.KeyPress : EventType.KeyRelease,
            KeyCode = e.Code,
            Button = MouseButton.None
        };
    }
}
