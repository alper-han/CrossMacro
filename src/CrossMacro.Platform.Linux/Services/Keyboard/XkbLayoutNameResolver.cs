using System.Xml.Linq;
using CrossMacro.Core.Logging;

namespace CrossMacro.Platform.Linux.Services.Keyboard;

internal sealed class XkbLayoutNameResolver
{
    private static readonly string[] DefaultRulesPaths =
    [
        "/usr/share/X11/xkb/rules/evdev.xml",
        "/usr/share/X11/xkb/rules/base.xml"
    ];

    private static readonly Dictionary<string, string> KnownLayoutNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["English (US)"] = "us",
        ["English (UK)"] = "gb",
        ["Turkish"] = "tr",
        ["German"] = "de",
        ["French"] = "fr",
        ["Spanish"] = "es",
        ["Italian"] = "it",
        ["Russian"] = "ru",
        ["Portuguese"] = "pt",
        ["Arabic"] = "ara",
        ["Chinese"] = "cn",
        ["Japanese"] = "jp"
    };

    private readonly IReadOnlyList<string> _rulesPaths;

    internal XkbLayoutNameResolver()
        : this(DefaultRulesPaths)
    {
    }

    internal XkbLayoutNameResolver(IReadOnlyList<string> rulesPaths)
    {
        _rulesPaths = rulesPaths ?? throw new ArgumentNullException(nameof(rulesPaths));
    }

    public string? TryResolveLayoutCode(string layoutName)
    {
        if (string.IsNullOrWhiteSpace(layoutName)) return null;

        var normalizedLayoutCode = NormalizeLayoutCode(layoutName);
        if (!string.IsNullOrWhiteSpace(normalizedLayoutCode)) return normalizedLayoutCode;

        foreach (var rulesPath in _rulesPaths)
        {
            var layout = TryResolveFromRulesFile(layoutName, rulesPath);
            if (!string.IsNullOrWhiteSpace(layout)) return layout;
        }

        return KnownLayoutNames.TryGetValue(layoutName.Trim(), out var knownLayout)
            ? knownLayout
            : null;
    }

    internal static string? TryResolveFromRulesFile(string layoutName, string rulesPath)
    {
        if (string.IsNullOrWhiteSpace(layoutName) || !File.Exists(rulesPath)) return null;

        try
        {
            var document = XDocument.Load(rulesPath);
            foreach (var layoutElement in document.Descendants("layout"))
            {
                var layoutCode = layoutElement.Element("configItem")?.Element("name")?.Value;
                if (string.IsNullOrWhiteSpace(layoutCode)) continue;

                var layoutDescription = layoutElement.Element("configItem")?.Element("description")?.Value;
                if (string.Equals(layoutDescription, layoutName, StringComparison.OrdinalIgnoreCase))
                {
                    return layoutCode;
                }

                foreach (var variantElement in layoutElement.Descendants("variant"))
                {
                    var variantDescription = variantElement.Element("configItem")?.Element("description")?.Value;
                    if (string.Equals(variantDescription, layoutName, StringComparison.OrdinalIgnoreCase))
                    {
                        return layoutCode;
                    }
                }
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.Xml.XmlException or IOException or UnauthorizedAccessException)
        {
            Log.Debug("[XkbLayoutNameResolver] Failed to parse XKB rules file {Path}: {Message}", rulesPath, ex.Message);
        }

        return null;
    }

    private static string? NormalizeLayoutCode(string layoutName)
    {
        var trimmed = layoutName.Trim();
        return trimmed.Length == 2 && trimmed.All(char.IsAsciiLetter)
            ? trimmed.ToLowerInvariant()
            : null;
    }
}
