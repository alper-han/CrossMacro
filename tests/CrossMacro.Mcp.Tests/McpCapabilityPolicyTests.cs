namespace CrossMacro.Mcp.Tests;

public sealed class McpCapabilityPolicyTests
{
    [Fact]
    public void Defaults_ShouldAllowAllConfiguredCapabilities()
    {
        var settings = new AppSettings();
        var policy = new McpCapabilityPolicy(new TestSettingsService(settings));

        Assert.True(policy.IsAllowed(McpCapability.StatusRead));
        Assert.True(policy.IsAllowed(McpCapability.MacroRead));
        Assert.True(policy.IsAllowed(McpCapability.ScreenRead));
        Assert.True(policy.IsAllowed(McpCapability.ClipboardRead));
        Assert.True(policy.IsAllowed(McpCapability.InputAutomation));
        Assert.True(policy.IsAllowed(McpCapability.CommandExecute));
        Assert.True(policy.IsAllowed(McpCapability.ShellExecute));
    }

    [Fact]
    public void RestrictedMode_ShouldAllowOnlyStatusAndMacroMetadata()
    {
        var settings = new AppSettings();
        settings.McpSecurity.AllowScreenRead = true;
        settings.McpSecurity.AllowWindowRead = true;
        var policy = new McpCapabilityPolicy(new TestSettingsService(settings));

        policy.SetRestricted(true);

        Assert.True(policy.IsAllowed(McpCapability.StatusRead));
        Assert.True(policy.IsAllowed(McpCapability.MacroRead));
        Assert.False(policy.IsAllowed(McpCapability.ScreenRead));
        Assert.False(policy.IsAllowed(McpCapability.WindowRead));
        Assert.Equal("capability_denied", policy.Require(McpCapability.ScreenRead).Errors[0].Code);
    }

    [Fact]
    public void PrivilegeElevation_ShouldRemainDisabledByDefault()
    {
        var policy = new McpCapabilityPolicy(new TestSettingsService(new AppSettings()));

        Assert.False(policy.IsAllowed(McpCapability.PrivilegeElevation));
    }

    [Fact]
    public void PersistedManagementCapabilities_ShouldControlMatchingToolGroups()
    {
        var settings = new AppSettings();
        settings.McpSecurity.AllowSettingsRead = false;
        settings.McpSecurity.AllowSettingsWrite = false;
        settings.McpSecurity.AllowProfileManage = false;
        settings.McpSecurity.AllowTextExpansionRead = false;
        settings.McpSecurity.AllowTextExpansionWrite = false;
        settings.McpSecurity.AllowTaskManage = false;
        var policy = new McpCapabilityPolicy(new TestSettingsService(settings));

        Assert.False(policy.IsAllowed(McpCapability.SettingsRead));
        Assert.False(policy.IsAllowed(McpCapability.SettingsWrite));
        Assert.False(policy.IsAllowed(McpCapability.ProfileManage));
        Assert.False(policy.IsAllowed(McpCapability.TextExpansionRead));
        Assert.False(policy.IsAllowed(McpCapability.TextExpansionWrite));
        Assert.False(policy.IsAllowed(McpCapability.TaskManage));
    }

    private sealed class TestSettingsService(AppSettings settings) : ISettingsService
    {
        public AppSettings Current { get; } = settings;

        public AppSettings Load() => Current;

        public Task<AppSettings> LoadAsync() => Task.FromResult(Current);

        public void Save()
        {
        }

        public Task SaveAsync() => Task.CompletedTask;
    }
}
