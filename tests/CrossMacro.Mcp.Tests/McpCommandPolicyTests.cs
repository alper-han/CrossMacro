namespace CrossMacro.Mcp.Tests;

public sealed class McpCommandPolicyTests
{
    private readonly McpCommandPolicy _policy = new();

    [Theory]
    [InlineData("mcp")]
    [InlineData("headless")]
    [InlineData("quick-setup")]
    [InlineData("setup")]
    [InlineData("--headless")]
    [InlineData("sudo")]
    [InlineData("/usr/bin/crossmacro")]
    [InlineData("play now")]
    [InlineData("")]
    public void Validate_RejectsCommandsOutsideTheMcpAllowList(string command)
    {
        var outcome = _policy.Validate(command, []);

        Assert.False(outcome.Success);
        Assert.Equal("invalid_arguments", Assert.Single(outcome.Errors).Code);
    }

    [Theory]
    [InlineData("macro")]
    [InlineData("PLAY")]
    [InlineData(" doctor ")]
    public void Validate_AcceptsAllowListedCommandsCaseInsensitively(string command)
    {
        Assert.True(_policy.Validate(command, ["--json"]).Success);
    }

    [Theory]
    [InlineData("--display")]
    [InlineData("--DRM")]
    [InlineData("--start-minimized")]
    public void Validate_RejectsDisplayStartupOptions(string option)
    {
        Assert.False(_policy.Validate("play", ["demo.macro", option]).Success);
    }

    [Fact]
    public void Validate_RejectsOversizedArgumentLists()
    {
        Assert.False(_policy.Validate("play", [.. Enumerable.Repeat("x", 129)]).Success);
        Assert.False(_policy.Validate("play", [new string('x', 16_385)]).Success);
        Assert.False(_policy.Validate("play", [.. Enumerable.Repeat(new string('x', 16_384), 17)]).Success);
    }
}
