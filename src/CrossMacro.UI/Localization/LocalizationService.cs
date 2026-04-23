using System;
using System.Globalization;
using System.Resources;
using CrossMacro.Core.Services;

namespace CrossMacro.UI.Localization;

public sealed class LocalizationService : ILocalizationService
{
    private static readonly ResourceManager ResourceManager = Resources.ResourceManager;

    public LocalizationService()
    {
        CurrentCulture = ResolveCulture(null);
    }

    public CultureInfo CurrentCulture { get; private set; }

    public event EventHandler? CultureChanged;

    public string this[string key] => ResourceManager.GetString(key, CurrentCulture) ?? key;

    public void SetCulture(string? cultureName)
    {
        var culture = ResolveCulture(cultureName);
        if (Equals(CurrentCulture, culture))
        {
            return;
        }

        CurrentCulture = culture;
        Resources.Culture = culture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureChanged?.Invoke(this, EventArgs.Empty);
    }

    public static CultureInfo ResolveCulture(string? cultureName)
    {
        if (string.IsNullOrWhiteSpace(cultureName) || cultureName.Equals("auto", StringComparison.OrdinalIgnoreCase))
        {
            return CultureInfo.GetCultureInfo("en");
        }

        try
        {
            return ResolveSupportedCulture(CultureInfo.GetCultureInfo(cultureName));
        }
        catch (CultureNotFoundException)
        {
            return CultureInfo.GetCultureInfo("en");
        }
    }

    private static CultureInfo ResolveSupportedCulture(CultureInfo culture)
    {
        if (culture.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
        {
            return CultureInfo.GetCultureInfo("zh");
        }

        if (culture.Name.StartsWith("ja", StringComparison.OrdinalIgnoreCase))
        {
            return CultureInfo.GetCultureInfo("ja");
        }

        if (culture.Name.StartsWith("es", StringComparison.OrdinalIgnoreCase))
        {
            return CultureInfo.GetCultureInfo("es");
        }

        if (culture.Name.StartsWith("ar", StringComparison.OrdinalIgnoreCase))
        {
            return CultureInfo.GetCultureInfo("ar");
        }

        if (culture.Name.StartsWith("fr", StringComparison.OrdinalIgnoreCase))
        {
            return CultureInfo.GetCultureInfo("fr");
        }

        if (culture.Name.StartsWith("pt", StringComparison.OrdinalIgnoreCase))
        {
            return CultureInfo.GetCultureInfo("pt");
        }

        if (culture.Name.StartsWith("ru", StringComparison.OrdinalIgnoreCase))
        {
            return CultureInfo.GetCultureInfo("ru");
        }

        if (culture.Name.StartsWith("tr", StringComparison.OrdinalIgnoreCase))
        {
            return CultureInfo.GetCultureInfo("tr");
        }

        return CultureInfo.GetCultureInfo("en");
    }
}
