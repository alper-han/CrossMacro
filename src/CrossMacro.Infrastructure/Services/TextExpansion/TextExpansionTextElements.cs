
namespace CrossMacro.Infrastructure.Services.TextExpansion;

internal static class TextExpansionTextElements
{
    public static IEnumerable<TextExpansionTextElement> Enumerate(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return EnumerateImpl(text);
    }

    private static IEnumerable<TextExpansionTextElement> EnumerateImpl(string text)
    {
        for (int i = 0; i < text.Length; i++)
        {
            var current = text[i];

            if (current == '\r')
            {
                continue;
            }

            if (current == '\n')
            {
                yield return new TextExpansionTextElement(i, 1, current, KeyboardLayoutCharacter: null, IsNewLine: true);
                continue;
            }

            if (char.IsHighSurrogate(current) &&
                i + 1 < text.Length &&
                char.IsLowSurrogate(text[i + 1]))
            {
                yield return new TextExpansionTextElement(
                    StartIndex: i,
                    Length: 2,
                    CodePoint: char.ConvertToUtf32(text, i),
                    KeyboardLayoutCharacter: null,
                    IsNewLine: false);
                i++;
                continue;
            }

            yield return new TextExpansionTextElement(i, 1, current, current, IsNewLine: false);
        }
    }
}
