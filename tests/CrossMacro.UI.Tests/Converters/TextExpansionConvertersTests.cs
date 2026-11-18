namespace CrossMacro.UI.Tests.Converters;

[Collection(LocalizationGlobalStateCollection.Name)]
public sealed class TextExpansionConvertersTests
{
    [Theory]
    [InlineData("en", TextInsertionMode.Paste, "Paste")]
    [InlineData("en", TextInsertionMode.DirectTyping, "Direct Typing")]
    [InlineData("tr", TextInsertionMode.Paste, "Yapıştır")]
    [InlineData("tr", TextInsertionMode.DirectTyping, "Doğrudan Yazma")]
    [InlineData("fr", TextInsertionMode.Paste, "Coller")]
    [InlineData("fr", TextInsertionMode.DirectTyping, "Saisie directe")]
    public void InsertionModeDisplayText_ReturnsExpectedLocalizedLabel(
        string cultureName,
        TextInsertionMode mode,
        string expected)
    {
        using var cultureScope = new LocalizationCultureScope(cultureName);

        var result = TextExpansionConverters.InsertionModeDisplayText.Convert(
            mode,
            typeof(string),
            parameter: null,
            CultureInfo.InvariantCulture);

        _ = result.Should().Be(expected);
    }

    [Theory]
    [InlineData(PasteMethod.CtrlV, "Ctrl+V")]
    [InlineData(PasteMethod.CtrlShiftV, "Ctrl+Shift+V")]
    [InlineData(PasteMethod.ShiftInsert, "Shift+Insert")]
    public void PasteMethodDisplayText_ReturnsExpectedLabel(PasteMethod method, string expected)
    {
        var result = TextExpansionConverters.PasteMethodDisplayText.Convert(
            method,
            typeof(string),
            parameter: null,
            CultureInfo.InvariantCulture);

        _ = result.Should().Be(expected);
    }

    [Theory]
    [InlineData(DirectTypingMethod.FastBatch, "Fast (batched)")]
    [InlineData(DirectTypingMethod.CompatibleKeyByKey, "Compatible (key-by-key)")]
    public void DirectTypingMethodDisplayText_ReturnsExpectedLabel(DirectTypingMethod method, string expected)
    {
        var result = TextExpansionConverters.DirectTypingMethodDisplayText.Convert(
            method,
            typeof(string),
            parameter: null,
            CultureInfo.InvariantCulture);

        _ = result.Should().Be(expected);
    }

    [Theory]
    [InlineData(TextInsertionMode.Paste, true)]
    [InlineData(TextInsertionMode.DirectTyping, false)]
    public void IsPasteMode_ReturnsWhetherModeUsesClipboard(TextInsertionMode mode, bool expected)
    {
        var result = TextExpansionConverters.IsPasteMode.Convert(
            mode,
            typeof(bool),
            parameter: null,
            CultureInfo.InvariantCulture);

        _ = result.Should().Be(expected);
    }

    [Theory]
    [InlineData(TextInsertionMode.Paste, false)]
    [InlineData(TextInsertionMode.DirectTyping, true)]
    public void IsDirectTypingMode_ReturnsWhetherModeUsesDirectTyping(TextInsertionMode mode, bool expected)
    {
        var result = TextExpansionConverters.IsDirectTypingMode.Convert(
            mode,
            typeof(bool),
            parameter: null,
            CultureInfo.InvariantCulture);

        _ = result.Should().Be(expected);
    }
}
