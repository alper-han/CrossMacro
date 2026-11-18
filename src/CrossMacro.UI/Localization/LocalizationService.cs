
namespace CrossMacro.UI.Localization;

public sealed class LocalizationService : ILocalizationService
{
    private static readonly ResourceManager ResourceManager = Resources.ResourceManager;
    private static readonly CultureInfo DefaultCulture = CultureInfo.GetCultureInfo("en");
    private static readonly string[] TranslatedLanguages = ["zh", "ja", "es", "ar", "fr", "pt", "ru", "tr"];

    public LocalizationService()
    {
        CurrentCulture = DefaultCulture;
    }

    public CultureInfo CurrentCulture { get; private set; }

    public event EventHandler? CultureChanged;

    public string this[string key] => ResourceManager.GetString(key, CurrentCulture) ?? key;

    /// <summary>
    /// Looks <paramref name="key"/> up in the language last applied through <see cref="SetCulture"/>,
    /// for code that cannot take an <see cref="ILocalizationService"/> (XAML-instantiated converters, static text).
    /// </summary>
    public static string Get(string key) => ResourceManager.GetString(key, Resources.Culture) ?? key;

    public void SetCulture(string? cultureName)
    {
        var culture = ResolveCulture(cultureName);
        var cultureChanged = !Equals(CurrentCulture, culture);
        CurrentCulture = culture;
        ApplyCulture(culture);

        if (cultureChanged)
        {
            CultureChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public static CultureInfo ResolveCulture(string? cultureName)
    {
        if (string.IsNullOrWhiteSpace(cultureName))
        {
            return DefaultCulture;
        }

        try
        {
            return ResolveSupportedCulture(CultureInfo.GetCultureInfo(cultureName));
        }
        catch (CultureNotFoundException)
        {
            return DefaultCulture;
        }
    }

    private static CultureInfo ResolveSupportedCulture(CultureInfo culture)
    {
        var language = TranslatedLanguages.FirstOrDefault(
            code => culture.Name.StartsWith(code, StringComparison.OrdinalIgnoreCase));

        return language is null ? DefaultCulture : CultureInfo.GetCultureInfo(language);
    }

    private static void ApplyCulture(CultureInfo culture)
    {
        Resources.Culture = culture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
    }
}
