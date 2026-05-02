using System;
using System.Globalization;
using CrossMacro.UI.Localization;

namespace CrossMacro.UI.Tests.Localization;

internal sealed class LocalizationCultureScope : IDisposable
{
    private readonly CultureInfo _currentCulture;
    private readonly CultureInfo _currentUiCulture;
    private readonly CultureInfo? _defaultThreadCurrentCulture;
    private readonly CultureInfo? _defaultThreadCurrentUiCulture;
    private readonly CultureInfo? _resourceCulture;

    public LocalizationCultureScope(string cultureName = "en")
    {
        _currentCulture = CultureInfo.CurrentCulture;
        _currentUiCulture = CultureInfo.CurrentUICulture;
        _defaultThreadCurrentCulture = CultureInfo.DefaultThreadCurrentCulture;
        _defaultThreadCurrentUiCulture = CultureInfo.DefaultThreadCurrentUICulture;
        _resourceCulture = Resources.Culture;

        SetCulture(CultureInfo.GetCultureInfo(cultureName));
    }

    public void Dispose()
    {
        CultureInfo.CurrentCulture = _currentCulture;
        CultureInfo.CurrentUICulture = _currentUiCulture;
        CultureInfo.DefaultThreadCurrentCulture = _defaultThreadCurrentCulture;
        CultureInfo.DefaultThreadCurrentUICulture = _defaultThreadCurrentUiCulture;
        Resources.Culture = _resourceCulture;
    }

    private static void SetCulture(CultureInfo culture)
    {
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        Resources.Culture = culture;
    }
}
