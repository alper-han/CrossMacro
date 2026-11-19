namespace CrossMacro.Infrastructure.Tests.Services.TextExpansion;

internal sealed class TaggedTestInputSimulator(bool supportsTaggedKeyboardInput) : TestInputSimulator, ITaggedKeyboardInputSimulator
{
    public bool SupportsTaggedKeyboardInput => supportsTaggedKeyboardInput;

    public List<(int KeyCode, bool Pressed, long Tag)> TaggedKeyPresses { get; } = new();

    public List<(int KeyCode, bool Pressed)> RegularKeyPresses { get; } = new();

    public override void KeyPress(int keyCode, bool pressed)
    {
        RegularKeyPresses.Add((keyCode, pressed));
        base.KeyPress(keyCode, pressed);
    }

    public void KeyPressTagged(int keyCode, bool pressed, long tag)
    {
        TaggedKeyPresses.Add((keyCode, pressed, tag));
    }
}
