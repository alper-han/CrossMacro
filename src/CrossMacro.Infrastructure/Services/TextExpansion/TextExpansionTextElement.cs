
namespace CrossMacro.Infrastructure.Services.TextExpansion;

internal readonly record struct TextExpansionTextElement(
    int StartIndex,
    int Length,
    int CodePoint,
    char? KeyboardLayoutCharacter,
    bool IsNewLine)
{
    public bool CanUseKeyboardLayoutMapping => KeyboardLayoutCharacter.HasValue;

    public string GetText(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return source.Substring(StartIndex, Length);
    }
}
