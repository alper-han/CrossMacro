
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
}