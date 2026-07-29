
namespace CrossMacro.UI.Services;

public class FileDialogFilter
{
    public string Name { get; set; } = string.Empty;
    public IReadOnlyList<string> Extensions { get; set; } = [];

    public static string[] NormalizePatterns(IEnumerable<string>? extensions)
    {
        if (extensions is null)
        {
            return [];
        }

        return extensions
            .Select(NormalizePattern)
            .Where(static pattern => !string.IsNullOrWhiteSpace(pattern))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string NormalizePattern(string? extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            return string.Empty;
        }

        var trimmed = extension.Trim();

        if (trimmed.StartsWith("*.", StringComparison.Ordinal))
        {
            trimmed = trimmed[2..];
        }
        else if (trimmed.StartsWith('.'))
        {
            trimmed = trimmed[1..];
        }
        else if (trimmed.StartsWith('*'))
        {
            trimmed = trimmed[1..];
            if (trimmed.StartsWith('.'))
            {
                trimmed = trimmed[1..];
            }
        }

        return string.IsNullOrWhiteSpace(trimmed) ? string.Empty : $"*.{trimmed}";
    }
}
