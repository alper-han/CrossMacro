namespace CrossMacro.Mcp.Tests;

public sealed class McpAutomationPolicyTests
{
    [Fact]
    public void CompatibilityPolicy_KeepsCliUnlimitedFiniteAndTypedZeroInvalid()
    {
        Assert.True(McpAutomationPolicy.TryFromCliTimeout(0, "timeoutSeconds", out var unlimited, out _));
        Assert.Equal(McpAutomationPolicy.DefaultTimeoutSeconds, unlimited);
        Assert.False(McpAutomationPolicy.TryGetSeconds(0, "timeoutSeconds", McpAutomationPolicy.DefaultTimeoutSeconds, allowZero: false, out _, out _));
        Assert.True(McpAutomationPolicy.TryGetSeconds(value: null, "timeoutSeconds", McpAutomationPolicy.DefaultTimeoutSeconds, allowZero: false, out var timeout, out _));
        Assert.Equal(McpAutomationPolicy.DefaultTimeoutSeconds, timeout);
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(McpAutomationPolicy.MaximumTimeoutSeconds, true)]
    [InlineData(McpAutomationPolicy.MaximumTimeoutSeconds + 1, false)]
    [InlineData(-1, false)]
    public void CliTimeout_FollowsTheSameBoundsAsTheTypedArgument(int seconds, bool valid)
    {
        Assert.Equal(valid, McpAutomationPolicy.TryFromCliTimeout(seconds, "timeoutSeconds", out var cli, out _));
        Assert.Equal(valid, McpAutomationPolicy.TryGetSeconds(seconds, "timeoutSeconds", McpAutomationPolicy.DefaultTimeoutSeconds, allowZero: false, out var typed, out _));
        if (valid)
        {
            Assert.Equal(typed, cli);
        }
    }
}
