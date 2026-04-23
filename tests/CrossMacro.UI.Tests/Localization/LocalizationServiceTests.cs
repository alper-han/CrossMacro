using System.Globalization;
using CrossMacro.UI.Localization;
using FluentAssertions;
using Xunit;

namespace CrossMacro.UI.Tests.Localization;

public class LocalizationServiceTests
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

        culture.Name.Should().Be(expected);
    }

    [Fact]
    public void ResolveCulture_WhenUnsupportedCultureProvided_FallsBackToEnglish()
    {
        var culture = LocalizationService.ResolveCulture("de-DE");

        culture.Name.Should().Be("en");
    }

    [Fact]
    public void ResolveCulture_WhenNullOrAutoProvided_FallsBackToEnglish()
    {
        LocalizationService.ResolveCulture(null).Name.Should().Be("en");
        LocalizationService.ResolveCulture(string.Empty).Name.Should().Be("en");
        LocalizationService.ResolveCulture("auto").Name.Should().Be("en");
    }

    [Fact]
    public void SetCulture_WhenSupportedLanguageProvided_UpdatesCurrentCulture()
    {
        var service = new LocalizationService();

        service.SetCulture("fr-FR");

        service.CurrentCulture.Name.Should().Be("fr");
    }
}
