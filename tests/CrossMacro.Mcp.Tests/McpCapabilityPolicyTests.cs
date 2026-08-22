namespace CrossMacro.Mcp.Tests;

public sealed class McpCapabilityPolicyTests
{
    [Fact]
    public void Defaults_ShouldAllowAllConfiguredCapabilities()
    {
        var settings = new AppSettings();
        var policy = new McpCapabilityPolicy(new TestSettingsService(settings));

        foreach (McpCapability capability in Enum.GetValues<McpCapability>())
        {
            Assert.Equal(capability is not McpCapability.PrivilegeElevation, policy.IsAllowed(capability));
        }
    }

    [Fact]
    public void PersistedSecuritySettings_ShouldControlTheirMatchingCapabilities()
    {
        foreach (McpSecuritySetting setting in Enum.GetValues<McpSecuritySetting>())
        {
            var settings = new AppSettings();
            settings.McpSecurity.Set(setting, false);
            var policy = new McpCapabilityPolicy(new TestSettingsService(settings));

            Assert.False(policy.IsAllowed(ToCapability(setting)));
            Assert.Equal("capability_denied", policy.Require(ToCapability(setting)).Errors[0].Code);
        }
    }

    [Fact]
    public void RestrictedMode_ShouldAllowOnlyStatusAndMacroMetadata()
    {
        var settings = new AppSettings();
        settings.McpSecurity.AllowScreenRead = true;
        settings.McpSecurity.AllowWindowRead = true;
        var policy = new McpCapabilityPolicy(new TestSettingsService(settings));

        policy.SetRestricted(true);

        foreach (McpCapability capability in Enum.GetValues<McpCapability>())
        {
            Assert.Equal(capability is McpCapability.StatusRead or McpCapability.MacroRead, policy.IsAllowed(capability));
        }
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

    private static McpCapability ToCapability(McpSecuritySetting setting) => setting switch
    {
        McpSecuritySetting.MacroRead => McpCapability.MacroRead,
        McpSecuritySetting.ScreenRead => McpCapability.ScreenRead,
        McpSecuritySetting.ClipboardRead => McpCapability.ClipboardRead,
        McpSecuritySetting.ClipboardWrite => McpCapability.ClipboardWrite,
        McpSecuritySetting.InputAutomation => McpCapability.InputAutomation,
        McpSecuritySetting.Recording => McpCapability.Recording,
        McpSecuritySetting.WindowRead => McpCapability.WindowRead,
        McpSecuritySetting.WindowControl => McpCapability.WindowControl,
        McpSecuritySetting.FileRead => McpCapability.FileRead,
        McpSecuritySetting.FileWrite => McpCapability.FileWrite,
        McpSecuritySetting.CommandExecute => McpCapability.CommandExecute,
        McpSecuritySetting.ShellExecute => McpCapability.ShellExecute,
        McpSecuritySetting.PrivilegeElevation => McpCapability.PrivilegeElevation,
        McpSecuritySetting.SettingsRead => McpCapability.SettingsRead,
        McpSecuritySetting.SettingsWrite => McpCapability.SettingsWrite,
        McpSecuritySetting.ProfileManage => McpCapability.ProfileManage,
        McpSecuritySetting.TextExpansionRead => McpCapability.TextExpansionRead,
        McpSecuritySetting.TextExpansionWrite => McpCapability.TextExpansionWrite,
        McpSecuritySetting.TaskManage => McpCapability.TaskManage,
        _ => throw new ArgumentOutOfRangeException(nameof(setting), setting, "Unknown MCP security setting."),
    };

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
