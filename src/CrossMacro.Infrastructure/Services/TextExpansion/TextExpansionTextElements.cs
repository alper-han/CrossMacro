using System;
using System.Collections.Generic;

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

internal static class TextExpansionTextElements
{
    public static IEnumerable<TextExpansionTextElement> Enumerate(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        for (int i = 0; i < text.Length; i++)
        {
            var current = text[i];

            if (current == '\r')
            {
                continue;
            }

            if (current == '\n')
            {
                yield return new TextExpansionTextElement(i, 1, current, null, true);
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

            yield return new TextExpansionTextElement(i, 1, current, current, false);
        }
    }
}
