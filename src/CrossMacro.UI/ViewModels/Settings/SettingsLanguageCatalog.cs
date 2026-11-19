namespace CrossMacro.UI.ViewModels.Settings;

/// <summary>The languages the UI is translated into and how they are presented in the language picker.</summary>
internal static class SettingsLanguageCatalog
{
    internal static readonly IReadOnlyList<SupportedLanguageDescriptor> SupportedLanguages =
    [
        new("en", "Language_English", "English", isDefault: true),
        new("ar", "Language_Arabic", "Arabic"),
        new("zh", "Language_Chinese", "Chinese"),
        new("fr", "Language_French", "French"),
        new("ja", "Language_Japanese", "Japanese"),
        new("pt", "Language_Portuguese", "Portuguese"),
        new("ru", "Language_Russian", "Russian"),
        new("es", "Language_Spanish", "Spanish"),
        new("tr", "Language_Turkish", "Turkish"),
    ];

    internal static string Normalize(string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            return "en";
        }

        var supportedLanguage = SupportedLanguages.FirstOrDefault(candidate =>
            string.Equals(candidate.Code, language, StringComparison.OrdinalIgnoreCase));
        return supportedLanguage?.Code ?? "en";
    }

    internal static LanguageOption[] CreateOptions(ILocalizationService localization)
    {
        return SupportedLanguages
            .OrderByDescending(language => language.IsDefault)
            .ThenBy(language => language.EnglishName, StringComparer.Ordinal)
            .Select(language => new LanguageOption
            {
                Code = language.Code,
                DisplayName = localization[language.ResourceKey],
            })
            .ToArray();
    }

    internal static string GetDisplayName(ILocalizationService localization, string code)
    {
        var language = SupportedLanguages.FirstOrDefault(language =>
            string.Equals(language.Code, code, StringComparison.OrdinalIgnoreCase));
        return localization[(language ?? SupportedLanguages[0]).ResourceKey];
    }
}
