using System;
using System.Text;

namespace CrossMacro.UI.Localization;

internal static class TextInputControlCharacterFormatter
{
    public static string Escape(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        return text
            .Replace("\b", "⌫", StringComparison.Ordinal)
            .Replace("\r", "↵", StringComparison.Ordinal)
            .Replace("\n", "↵", StringComparison.Ordinal)
            .Replace("\t", "⇥", StringComparison.Ordinal);
    }

    public static string Unescape(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(text.Length);
        for (var index = 0; index < text.Length; index++)
        {
            var ch = text[index];
            builder.Append(ch switch
            {
                '⌫' => '\b',
                '↵' => '\r',
                '⇥' => '\t',
                _ => ch
            });
        }

        return builder.ToString();
    }
}
