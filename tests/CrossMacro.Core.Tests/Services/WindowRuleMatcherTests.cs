namespace CrossMacro.Core.Tests.Services;

public sealed class WindowRuleMatcherTests
{
    [Theory]
    [InlineData(TriggerMatchMode.Equals, "Firefox", "Firefox", true)]
    [InlineData(TriggerMatchMode.Equals, "Firefox", "firefox", false)]
    [InlineData(TriggerMatchMode.Contains, "Fire", "Mozilla Firefox", true)]
    [InlineData(TriggerMatchMode.Regex, "^Mozilla .*", "Mozilla Firefox", true)]
    [InlineData(TriggerMatchMode.Regex, "[", "Mozilla Firefox", false)]
    public void IsMatch_UsesTheConfiguredRuleSemantics(
        TriggerMatchMode matchMode,
        string value,
        string actual,
        bool expected)
    {
        var result = WindowRuleMatcher.IsMatch(matchMode, value, actual);

        _ = result.Should().Be(expected);
    }

    [Fact]
    public void IsValid_RejectsUnsupportedFieldsAndInvalidRegex()
    {
        _ = WindowRuleMatcher.IsValid(TriggerField.Workspace, TriggerMatchMode.Equals, "1").Should().BeFalse();
        _ = WindowRuleMatcher.IsValid(TriggerField.WindowClass, TriggerMatchMode.Regex, "[").Should().BeFalse();
        _ = WindowRuleMatcher.IsValid(TriggerField.WindowClass, TriggerMatchMode.Regex, "(?<=Firefox) Browser").Should().BeFalse();
        _ = WindowRuleMatcher.IsValid(TriggerField.WindowClass, TriggerMatchMode.Regex, "^firefox$").Should().BeTrue();
    }

    [Theory]
    [InlineData(TriggerField.WindowTitle, "Browser", "Browser", null, null, true)]
    [InlineData(TriggerField.WindowClass, "Firefox", null, "Firefox", null, true)]
    [InlineData(TriggerField.ProcessName, "firefox", null, null, "firefox", true)]
    [InlineData(TriggerField.Workspace, "1", "1", "1", "1", false)]
    [InlineData(TriggerField.None, "1", "1", "1", "1", false)]
    public void IsMatch_FieldOverload_SelectsConfiguredWindowValue(
        TriggerField field,
        string value,
        string? windowTitle,
        string? windowClass,
        string? processName,
        bool expected)
    {
        var result = WindowRuleMatcher.IsMatch(
            field,
            TriggerMatchMode.Equals,
            value,
            windowTitle,
            windowClass,
            processName);

        _ = result.Should().Be(expected);
    }

    [Theory]
    [InlineData(" ", TriggerMatchMode.Equals)]
    [InlineData("Firefox", (TriggerMatchMode)999)]
    public void IsValid_RejectsBlankValuesAndUnknownMatchModes(string value, TriggerMatchMode matchMode)
    {
        _ = WindowRuleMatcher.IsValid(TriggerField.WindowTitle, matchMode, value).Should().BeFalse();
    }
}
