namespace CrossMacro.Mcp.Tests;

public sealed class McpSafeDefaultsTests
{
    [Fact]
    public void NewSecuritySettings_DenyOnlyShellCommandFileWriteAndSettingsWrite()
    {
        var settings = new McpSecuritySettings();

        Assert.True(settings.AllowMacroRead);
        Assert.True(settings.AllowScreenRead);
        Assert.True(settings.AllowClipboardRead);
        Assert.True(settings.AllowClipboardWrite);
        Assert.True(settings.AllowInputAutomation);
        Assert.True(settings.AllowRecording);
        Assert.True(settings.AllowWindowRead);
        Assert.True(settings.AllowWindowControl);
        Assert.True(settings.AllowFileRead);
        Assert.True(settings.AllowSettingsRead);
        Assert.True(settings.AllowProfileManage);
        Assert.True(settings.AllowTextExpansionRead);
        Assert.True(settings.AllowTextExpansionWrite);
        Assert.True(settings.AllowTaskManage);
        Assert.False(settings.AllowShellExecute);
        Assert.False(settings.AllowCommandExecute);
        Assert.False(settings.AllowFileWrite);
        Assert.False(settings.AllowSettingsWrite);
    }

    [Theory]
    [InlineData(McpCapability.ShellExecute)]
    [InlineData(McpCapability.CommandExecute)]
    [InlineData(McpCapability.FileWrite)]
    [InlineData(McpCapability.SettingsWrite)]
    public void DefaultPolicy_DeniesTheDangerousCapabilities(McpCapability capability)
    {
        var policy = new McpCapabilityPolicy(new TestSettingsService(new AppSettings()));

        Assert.False(policy.IsAllowed(capability));
        Assert.Equal("capability_denied", policy.Require(capability).Errors[0].Code);
    }

    [Fact]
    public void ExplicitOptIn_EnablesTheDangerousCapabilities()
    {
        var settings = new AppSettings();
        settings.McpSecurity.AllowShellExecute = true;
        settings.McpSecurity.AllowCommandExecute = true;
        settings.McpSecurity.AllowFileWrite = true;
        settings.McpSecurity.AllowSettingsWrite = true;
        var policy = new McpCapabilityPolicy(new TestSettingsService(settings));

        Assert.True(policy.IsAllowed(McpCapability.ShellExecute));
        Assert.True(policy.IsAllowed(McpCapability.CommandExecute));
        Assert.True(policy.IsAllowed(McpCapability.FileWrite));
        Assert.True(policy.IsAllowed(McpCapability.SettingsWrite));
    }

    [Theory]
    [InlineData(McpPathSetting.MacroRead, "mcp.paths.macroRead")]
    [InlineData(McpPathSetting.MacroWrite, "mcp.paths.macroWrite")]
    [InlineData(McpPathSetting.ImageRead, "mcp.paths.imageRead")]
    [InlineData(McpPathSetting.ImageWrite, "mcp.paths.imageWrite")]
    [InlineData(McpPathSetting.FileRead, "mcp.paths.fileRead")]
    [InlineData(McpPathSetting.FileWrite, "mcp.paths.fileWrite")]
    public void UnconfiguredPathRoots_DenyAbsolutePathsAndNameTheSettingToConfigure(McpPathSetting setting, string expectedKey)
    {
        var policy = new McpPathPolicy(new TestSettingsService(new AppSettings()));

        var allowed = policy.TryAuthorize(
            Path.Combine(McpTestData.GetPhysicalTemporaryRoot(), "crossmacro-default-deny.macro"),
            setting,
            requireExisting: false,
            out _,
            out var failure);

        Assert.False(allowed);
        var error = Assert.Single(failure.Errors);
        Assert.Equal("path_not_allowed", error.Code);
        Assert.Contains(expectedKey, error.Message, StringComparison.Ordinal);
        Assert.False(policy.HasConfiguredRoots(setting));
    }

    [Fact]
    public void ConfiguredPathRoots_AllowPathsInsideAndDenyPathsOutside()
    {
        var root = McpTestData.CreateTemporaryDirectory();
        var outside = McpTestData.CreateTemporaryDirectory();
        try
        {
            var settings = new AppSettings();
            settings.McpSecurity.Paths = settings.McpSecurity.Paths.WithRoots(McpPathSetting.MacroRead, [root]);
            var policy = new McpPathPolicy(new TestSettingsService(settings));

            Assert.True(policy.TryAuthorize(Path.Combine(root, "safe.macro"), McpPathSetting.MacroRead, requireExisting: false, out _, out _));
            Assert.False(policy.TryAuthorize(Path.Combine(outside, "outside.macro"), McpPathSetting.MacroRead, requireExisting: false, out _, out var failure));
            Assert.Equal("path_not_allowed", failure.Errors[0].Code);
            Assert.True(policy.HasConfiguredRoots(McpPathSetting.MacroRead));
            Assert.False(policy.TryAuthorize(Path.Combine(root, "safe.macro"), McpPathSetting.MacroWrite, requireExisting: false, out _, out _));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
            Directory.Delete(outside, recursive: true);
        }
    }

    [Fact]
    public async Task StatusGet_ReportsDisabledCapabilitiesToolsAndUnconfiguredRoots()
    {
        var settings = new AppSettings();
        settings.McpSecurity.Paths = settings.McpSecurity.Paths.WithRoots(McpPathSetting.MacroRead, [Path.GetTempPath()]);
        var tools = McpToolTestFactory.CreateRuntimeTools(
            capabilityPolicy: new McpCapabilityPolicy(new TestSettingsService(settings)),
            pathPolicy: new McpPathPolicy(new TestSettingsService(settings)));

        var status = await tools.GetStatusAsync(CancellationToken.None);

        Assert.DoesNotContain("ShellExecute", status.EnabledCapabilities, StringComparer.Ordinal);
        Assert.DoesNotContain("CommandExecute", status.EnabledCapabilities, StringComparer.Ordinal);
        Assert.DoesNotContain("command.execute", status.AvailableTools, StringComparer.Ordinal);
        Assert.DoesNotContain("settings.set", status.AvailableTools, StringComparer.Ordinal);
        Assert.Contains("status.get", status.AvailableTools, StringComparer.Ordinal);
        Assert.True(Assert.Single(status.PathRoots, static root => root.Setting is "mcp.paths.macroRead").Configured);
        Assert.False(Assert.Single(status.PathRoots, static root => root.Setting is "mcp.paths.fileWrite").Configured);
    }
}
