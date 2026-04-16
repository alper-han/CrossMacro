using System;

namespace CrossMacro.Core.Services;

public interface IInputSimulator : IDisposable
{
    string ProviderName { get; }
    
    bool IsSupported { get; }
    
    void Initialize(int screenWidth = 0, int screenHeight = 0);
    
    void MoveAbsolute(int x, int y);
    
    void MoveRelative(int dx, int dy);
    
    void MouseButton(int button, bool pressed);
    
    void Scroll(int delta, bool isHorizontal = false);
    
    void KeyPress(int keyCode, bool pressed);
    
    void Sync();
}

/// <summary>
/// Optional capability for simulators that can inject arbitrary Unicode text
/// without relying on the active keyboard layout.
/// </summary>
public interface IUnicodeTextInputSimulator
{
    bool SupportsUnicodeTextInput { get; }

    void TypeText(string text);
}

/// <summary>
/// Optional capability for simulators that can tag synthesized keyboard events
/// so platform capture backends can identify CrossMacro-originated input.
/// </summary>
public interface ITaggedKeyboardInputSimulator
{
    bool SupportsTaggedKeyboardInput { get; }

    void KeyPressTagged(int keyCode, bool pressed, long tag);
}

/// <summary>
/// Optional capability for simulators that can tag synthesized Unicode text input.
/// </summary>
public interface ITaggedUnicodeTextInputSimulator : IUnicodeTextInputSimulator
{
    void TypeTextTagged(string text, long tag);
}

/// <summary>
/// Marker values used to identify CrossMacro-generated injected input.
/// </summary>
public static class InputEventMarkers
{
    public const long TextExpansionKeyboardEvent = 0x4354584B;

    public static IntPtr ToIntPtr(long marker)
    {
        return new IntPtr(unchecked((nint)marker));
    }
}

public interface IInputSimulatorCapabilities
{
    bool SupportsAbsoluteCoordinates { get; }
}
