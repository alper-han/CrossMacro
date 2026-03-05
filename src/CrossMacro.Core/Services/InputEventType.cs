namespace CrossMacro.Core.Services;

public enum InputEventType
{
    Sync = 0,

    Key = 1,
    
    MouseButton = 2,
    
    MouseMove = 3,
    
    MouseScroll = 4,

    // Preserve protocol compatibility while making unknown events explicit.
    Unknown = 255
}
