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
}
