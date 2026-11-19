namespace CrossMacro.Mcp.Tests;

public sealed class McpCommandPolicyTests
{
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
        var outcome = McpCommandPolicy.Validate(command, []);

        Assert.False(outcome.Success);
        Assert.Equal("invalid_arguments", Assert.Single(outcome.Errors).Code);
    }

    [Theory]
    [InlineData("macro")]
    [InlineData("PLAY")]
    [InlineData(" doctor ")]
    public void Validate_AcceptsAllowListedCommandsCaseInsensitively(string command)
    {
        Assert.True(McpCommandPolicy.Validate(command, ["--json"]).Success);
    }

    [Theory]
    [InlineData("--display")]
    [InlineData("--DRM")]
    [InlineData("--start-minimized")]
    public void Validate_RejectsDisplayStartupOptions(string option)
    {
        Assert.False(McpCommandPolicy.Validate("play", ["demo.macro", option]).Success);
    }

    [Fact]
    public void Validate_RejectsOversizedArgumentLists()
    {
        Assert.False(McpCommandPolicy.Validate("play", [.. Enumerable.Repeat("x", 129)]).Success);
        Assert.False(McpCommandPolicy.Validate("play", [new string('x', 16_385)]).Success);
        Assert.False(McpCommandPolicy.Validate("play", [.. Enumerable.Repeat(new string('x', 16_384), 17)]).Success);
    }
}
