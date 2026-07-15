namespace CrossMacro.Platform.Abstractions;

/// <summary>
/// Optional capability for simulators that can tag synthesized Unicode text input.
/// </summary>
public interface ITaggedUnicodeTextInputSimulator : IUnicodeTextInputSimulator
{
    public void TypeTextTagged(string text, long tag);
}
