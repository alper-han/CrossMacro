using System;

namespace CrossMacro.Core.Services;

public class InputCaptureEventArgs : EventArgs
{
    public InputEventType Type { get; init; }
    
    public int Code { get; init; }
    
    public int Value { get; init; }
    
    public long Timestamp { get; init; }
    
    public string? DeviceName { get; init; }
}
