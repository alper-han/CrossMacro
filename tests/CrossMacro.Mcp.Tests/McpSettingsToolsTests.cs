namespace CrossMacro.Mcp.Tests;

public sealed class McpSettingsToolsTests
{
    [Fact]
    public async Task SettingsTools_ShouldAllowSettingsButRejectMcpSecurityPolicyChanges()
    {
        var settings = new AppSettings();
        settings.McpSecurity.AllowCommandExecute = true;
        var tools = McpToolTestFactory.CreateSettingsTools(settingsCliService: new SettingsCliService(new TestSettingsService(settings)));

        var all = await tools.GetSettingsAsync(all: true, cancellationToken: CancellationToken.None);
        var commandExecute = Assert.Single(all.Settings, static entry => entry.Key == "mcp.commandExecute");
        var restoreToken = Assert.Single(all.Settings, static entry => entry.Key == "screen.portalRestoreToken");

        Assert.True(all.Outcome.Success);
        Assert.Equal("True", commandExecute.Value);
        Assert.False(commandExecute.Redacted);
        Assert.Null(restoreToken.Value);
        Assert.True(restoreToken.Redacted);

        var set = await tools.SetSettingsAsync("mcp.commandExecute", "false", CancellationToken.None);
        Assert.False(set.Outcome.Success);
        Assert.Equal("capability_denied", Assert.Single(set.Outcome.Errors).Code);
        Assert.True(settings.McpSecurity.AllowCommandExecute);

        var keys = await tools.ListSettingsKeysAsync(CancellationToken.None);
        Assert.True(keys.Outcome.Success);
        Assert.Equal(SettingsCliService.SupportedKeys, keys.Keys);

        var reset = await tools.ResetSettingsAsync("mcp.commandExecute", CancellationToken.None);
        Assert.False(reset.Outcome.Success);
        Assert.Equal("capability_denied", Assert.Single(reset.Outcome.Errors).Code);
        Assert.True(settings.McpSecurity.AllowCommandExecute);

        settings.McpSecurity.AllowCommandExecute = false;
        var commands = McpToolTestFactory.CreateCommandTools();
        var denied = await commands.ExecuteCommandAsync(
            "settings",
            ["get", "mcp.commandExecute", "--json"],
            CancellationToken.None);
        Assert.True(denied.IsError);
    }

    [Fact]
    public async Task SettingsTools_ShouldRequireTheMatchingCapability()
    {
        var capabilityPolicy = new McpCapabilityPolicy(new TestSettingsService(new AppSettings()));
        capabilityPolicy.SetRestricted(true);
        var tools = McpToolTestFactory.CreateSettingsTools(capabilityPolicy: capabilityPolicy);

        var result = await tools.GetSettingsAsync(cancellationToken: CancellationToken.None);

        Assert.False(result.Outcome.Success);
        Assert.Equal("capability_denied", Assert.Single(result.Outcome.Errors).Code);
    }
}
