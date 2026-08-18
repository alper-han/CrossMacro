
namespace CrossMacro.UI.Tests.Localization;

[Collection(LocalizationGlobalStateCollection.Name)]
public sealed class LocalizationServiceTests
{
    [Theory]
    [InlineData("zh-CN", "zh")]
    [InlineData("ja-JP", "ja")]
    [InlineData("es-ES", "es")]
    [InlineData("ar-SA", "ar")]
    [InlineData("fr-FR", "fr")]
    [InlineData("pt-BR", "pt")]
    [InlineData("ru-RU", "ru")]
    [InlineData("tr-TR", "tr")]
    [InlineData("en-US", "en")]
    public void ResolveCulture_WhenSpecificSupportedCultureProvided_NormalizesToSupportedBaseCulture(string input, string expected)
    {
        var culture = LocalizationService.ResolveCulture(input);

        _ = culture.Name.Should().Be(expected);
    }

    [Fact]
    public void ResolveCulture_WhenUnsupportedCultureProvided_FallsBackToEnglish()
    {
        var culture = LocalizationService.ResolveCulture("de-DE");

        _ = culture.Name.Should().Be("en");
    }

    [Fact]
    public void ResolveCulture_WhenNullOrAutoProvided_FallsBackToEnglish()
    {
        _ = LocalizationService.ResolveCulture(cultureName: null).Name.Should().Be("en");
        _ = LocalizationService.ResolveCulture(string.Empty).Name.Should().Be("en");
        _ = LocalizationService.ResolveCulture("auto").Name.Should().Be("en");
    }

    [Fact]
    public void SetCulture_WhenSupportedLanguageProvided_UpdatesCurrentCulture()
    {
        using var cultureScope = new LocalizationCultureScope();
        var service = new LocalizationService();

        service.SetCulture("fr-FR");

        _ = service.CurrentCulture.Name.Should().Be("fr");
    }

    [Fact]
    public void SetCulture_WhenEnglishAlreadySelected_StillAppliesThreadAndResourceCultures()
    {
        using var cultureScope = new LocalizationCultureScope("tr-TR");
        CultureInfo.DefaultThreadCurrentCulture = null;
        CultureInfo.DefaultThreadCurrentUICulture = null;
        Resources.Culture = null;

        var service = new LocalizationService();

        service.SetCulture("en");

        _ = service.CurrentCulture.Name.Should().Be("en");
        _ = CultureInfo.CurrentCulture.Name.Should().Be("en");
        _ = CultureInfo.CurrentUICulture.Name.Should().Be("en");
        _ = (CultureInfo.DefaultThreadCurrentCulture?.Name.Should().Be("en"));
        _ = (CultureInfo.DefaultThreadCurrentUICulture?.Name.Should().Be("en"));
        _ = (Resources.Culture?.Name.Should().Be("en"));
    }
}
