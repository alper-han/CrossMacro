using System.Reflection;

namespace CrossMacro.UI.Tests.Localization;

[Collection(LocalizationGlobalStateGroup.Name)]
public sealed class UIStringsTests
{
    private static readonly string[] Languages = ["en", "ar", "es", "fr", "ja", "pt", "ru", "tr", "zh"];

    public static IEnumerable<object[]> StringAccessors() =>
        typeof(UIStrings)
            .GetProperties(BindingFlags.Public | BindingFlags.Static)
            .Select(property => new object[] { property.Name });

    [Theory]
    [MemberData(nameof(StringAccessors))]
    public void EveryString_ResolvesToAResourceInEveryLanguage(string propertyName)
    {
        var property = typeof(UIStrings).GetProperty(propertyName)!;

        foreach (var language in Languages)
        {
            using var cultureScope = new LocalizationCultureScope(language);

            var text = (string)property.GetValue(obj: null)!;

            _ = text.Should().NotBeNullOrWhiteSpace();
            _ = text.Should().NotContain("_", $"{propertyName} must resolve to text, not to a missing resource key, in '{language}'");
        }
    }
}
