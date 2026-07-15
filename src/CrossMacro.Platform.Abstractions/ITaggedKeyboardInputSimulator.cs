namespace CrossMacro.Platform.Abstractions;

/// <summary>
/// Optional capability for simulators that can tag synthesized keyboard events
/// so platform capture backends can identify CrossMacro-originated input.
/// </summary>
public interface ITaggedKeyboardInputSimulator
{
    bool SupportsTaggedKeyboardInput { get; }

    void KeyPressTagged(int keyCode, bool pressed, long tag);
}
