
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
            .Replace('\b', '⌫')
            .Replace('\r', '↵')
            .Replace('\n', '↵')
            .Replace('\t', '⇥');
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
            _ = builder.Append(ch switch
            {
                '⌫' => '\b',
                '↵' => '\r',
                '⇥' => '\t',
                _ => ch,
            });
        }

        return builder.ToString();
    }
}
