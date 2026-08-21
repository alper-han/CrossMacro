namespace CrossMacro.Mcp.Tests;

public sealed class McpUnrestrictedPolicyTests
{
    [Fact]
    public void NewSecuritySettings_EnableAllCapabilitiesByDefault()
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
        Assert.True(settings.AllowFileWrite);
        Assert.True(settings.AllowCommandExecute);
        Assert.True(settings.AllowShellExecute);
        Assert.True(settings.AllowSettingsRead);
        Assert.True(settings.AllowSettingsWrite);
        Assert.True(settings.AllowProfileManage);
        Assert.True(settings.AllowTextExpansionRead);
        Assert.True(settings.AllowTextExpansionWrite);
        Assert.True(settings.AllowTaskManage);
    }

    [Fact]
    public void EmptyPathRoots_AllowAbsolutePaths()
    {
        var policy = new McpPathPolicy(new TestSettingsService(new AppSettings()));

        Assert.True(policy.TryAuthorize(
            Path.Combine(Path.GetTempPath(), "crossmacro-unrestricted-test.macro"),
            McpPathKind.MacroRead,
            requireExisting: false,
            out _,
            out var outcome));
        Assert.True(outcome.Success);
    }

    [Fact]
    public void ConfiguredPathRoots_StillConstrainAbsolutePaths()
    {
        var root = Directory.CreateTempSubdirectory("crossmacro-mcp-root-");
        var outside = Directory.CreateTempSubdirectory("crossmacro-mcp-outside-");
        try
        {
            var settings = new AppSettings();
            settings.McpSecurity.Paths = settings.McpSecurity.Paths.WithRoots(McpPathSetting.MacroRead, [root.FullName]);
            var policy = new McpPathPolicy(new TestSettingsService(settings));

            Assert.True(policy.TryAuthorize(Path.Combine(root.FullName, "safe.macro"), McpPathKind.MacroRead, false, out _, out _));
            Assert.False(policy.TryAuthorize(Path.Combine(outside.FullName, "outside.macro"), McpPathKind.MacroRead, false, out _, out var failure));
            Assert.Equal("path_not_allowed", failure.Errors[0].Code);
        }
        finally
        {
            root.Delete(true);
            outside.Delete(true);
        }
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
