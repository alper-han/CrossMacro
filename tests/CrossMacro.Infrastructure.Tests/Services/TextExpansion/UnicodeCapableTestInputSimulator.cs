namespace CrossMacro.Infrastructure.Tests.Services.TextExpansion;

internal sealed class UnicodeCapableTestInputSimulator : TestInputSimulator, IUnicodeTextInputSimulator
{
    public bool SupportsUnicodeTextInput => true;

    public List<string> TypedText { get; } = new();

    public void TypeText(string text)
    {
        TypedText.Add(text);
    }
}
