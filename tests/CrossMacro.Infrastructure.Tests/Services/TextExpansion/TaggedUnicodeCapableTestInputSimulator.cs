namespace CrossMacro.Infrastructure.Tests.Services.TextExpansion;

internal sealed class TaggedUnicodeCapableTestInputSimulator : TestInputSimulator, ITaggedUnicodeTextInputSimulator
{
    public bool SupportsUnicodeTextInput => true;

    public List<string> TypedText { get; } = new();

    public List<(string Text, long Tag)> TaggedText { get; } = new();

    public void TypeText(string text)
    {
        TypedText.Add(text);
    }

    public void TypeTextTagged(string text, long tag)
    {
        TaggedText.Add((text, tag));
    }
}
