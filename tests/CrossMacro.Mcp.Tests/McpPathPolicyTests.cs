namespace CrossMacro.Mcp.Tests;

public sealed class McpPathPolicyTests
{
    private static McpPathPolicy CreatePolicy(McpPathSetting setting, params string[] roots)
    {
        var settings = new AppSettings();
        settings.McpSecurity.Paths = settings.McpSecurity.Paths.WithRoots(setting, roots);
        return new McpPathPolicy(new TestSettingsService(settings));
    }

    [Fact]
    public void PathPolicy_ShouldRequireConfiguredCanonicalRoots()
    {
        using var root = new TempDirectory();
        using var outside = new TempDirectory();
        var file = root.WriteFile("safe.macro", "macro");
        var settings = new AppSettings();
        settings.McpSecurity.Paths = settings.McpSecurity.Paths.WithRoots(McpPathSetting.MacroRead, [root.Path]);
        var policy = new McpPathPolicy(new TestSettingsService(settings));

        Assert.True(policy.TryAuthorize(file, McpPathSetting.MacroRead, requireExisting: true, out var normalized, out var success));
        Assert.Equal(Path.GetFullPath(file), normalized);
        Assert.True(success.Success);

        var traversal = Path.Combine(root.Path, "..", Path.GetFileName(outside.Path), "outside.macro");
        Assert.False(policy.TryAuthorize(traversal, McpPathSetting.MacroRead, requireExisting: false, out _, out var denied));
        Assert.Equal("path_not_allowed", denied.Errors[0].Code);
    }

    [Fact]
    public void PathPolicy_ShouldRejectPersistedRelativeRootsAndAllowTheFilesystemRoot()
    {
        var temporaryRoot = McpTestData.GetPhysicalTemporaryRoot();
        var settings = new AppSettings();
        settings.McpSecurity.Paths = new McpPathSettings(
            macroReadRoots: ["relative-root"],
            macroWriteRoots: [],
            imageReadRoots: [],
            imageWriteRoots: [],
            fileReadRoots: [],
            fileWriteRoots: []);
        var policy = new McpPathPolicy(new TestSettingsService(settings));

        Assert.False(policy.TryAuthorize(temporaryRoot, McpPathSetting.MacroRead, requireExisting: true, out _, out var relativeRootFailure));
        Assert.Equal("path_not_allowed", relativeRootFailure.Errors[0].Code);

        settings.McpSecurity.Paths = settings.McpSecurity.Paths.WithRoots(McpPathSetting.MacroRead, [Path.GetPathRoot(temporaryRoot)!]);

        Assert.True(policy.TryAuthorize(temporaryRoot, McpPathSetting.MacroRead, requireExisting: true, out _, out var rootSuccess));
        Assert.True(rootSuccess.Success);
    }

    [LinuxFact]
    public void PathPolicy_ShouldRejectDanglingSymlinkPaths()
    {
        using var root = new TempDirectory();
        using var outside = new TempDirectory();
        var link = root.Combine("dangling.macro");
        _ = File.CreateSymbolicLink(link, outside.Combine("target.macro"));
        var settings = new AppSettings();
        settings.McpSecurity.Paths = settings.McpSecurity.Paths.WithRoots(McpPathSetting.MacroWrite, [root.Path]);
        var policy = new McpPathPolicy(new TestSettingsService(settings));

        Assert.False(policy.TryAuthorize(link, McpPathSetting.MacroWrite, requireExisting: false, out _, out var failure));
        Assert.Equal("path_not_allowed", failure.Errors[0].Code);
    }

    [Fact]
    public void PathPolicy_WithNoRootsForTheSetting_ReportsTheMissingRootSettingKey()
    {
        using var root = new TempDirectory();
        var policy = CreatePolicy(McpPathSetting.MacroRead, root.Path);

        Assert.False(policy.TryAuthorize(root.Combine("a.macro"), McpPathSetting.MacroWrite, requireExisting: false, out var normalized, out var failure));

        Assert.Equal(string.Empty, normalized);
        Assert.Equal("path_not_allowed", failure.Errors[0].Code);
        Assert.Contains(McpSettingsKeys.GetRootsKey(McpPathSetting.MacroWrite), failure.Errors[0].Message, StringComparison.Ordinal);
        Assert.True(policy.HasConfiguredRoots(McpPathSetting.MacroRead));
        Assert.False(policy.HasConfiguredRoots(McpPathSetting.MacroWrite));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("relative/file.macro")]
    [InlineData("./file.macro")]
    public void PathPolicy_RejectsPathsThatAreNotAbsolute(string path)
    {
        using var root = new TempDirectory();
        var policy = CreatePolicy(McpPathSetting.MacroRead, root.Path);

        Assert.False(policy.TryAuthorize(path, McpPathSetting.MacroRead, requireExisting: false, out _, out var failure));

        Assert.False(failure.Success);
        Assert.Equal((int)CliExitCode.InvalidArguments, failure.ExitCode);
    }

    [Fact]
    public void PathPolicy_DoesNotTreatASiblingWithTheSamePrefixAsInsideTheRoot()
    {
        using var parent = new TempDirectory();
        var allowed = Directory.CreateDirectory(parent.Combine("allowed")).FullName;
        var sibling = Directory.CreateDirectory(parent.Combine("allowed-evil")).FullName;
        var policy = CreatePolicy(McpPathSetting.MacroRead, allowed);

        Assert.False(policy.TryAuthorize(Path.Combine(sibling, "x.macro"), McpPathSetting.MacroRead, requireExisting: false, out _, out var failure));

        Assert.Equal("path_not_allowed", failure.Errors[0].Code);
    }

    [Fact]
    public void PathPolicy_AllowsTheRootItselfAndNestedNewFilesWhenExistenceIsNotRequired()
    {
        using var root = new TempDirectory();
        var policy = CreatePolicy(McpPathSetting.MacroWrite, root.Path);
        var nested = Path.Combine(root.Path, "new", "deep.macro");

        Assert.True(policy.TryAuthorize(root.Path, McpPathSetting.MacroWrite, requireExisting: true, out _, out _));
        Assert.True(policy.TryAuthorize(nested, McpPathSetting.MacroWrite, requireExisting: false, out var normalized, out _));
        Assert.Equal(nested, normalized);
    }

    [Fact]
    public void PathPolicy_WhenTheTargetMustExistButDoesNot_ReportsAFileError()
    {
        using var root = new TempDirectory();
        var policy = CreatePolicy(McpPathSetting.MacroRead, root.Path);

        Assert.False(policy.TryAuthorize(root.Combine("missing.macro"), McpPathSetting.MacroRead, requireExisting: true, out _, out var failure));

        Assert.Equal((int)CliExitCode.FileError, failure.ExitCode);
    }

    [Fact]
    public void PathPolicy_IgnoresConfiguredRootsThatDoNotExist()
    {
        using var root = new TempDirectory();
        var policy = CreatePolicy(McpPathSetting.MacroRead, root.Combine("missing-root"));

        Assert.False(policy.TryAuthorize(Path.Combine(root.Combine("missing-root"), "a.macro"), McpPathSetting.MacroRead, requireExisting: false, out _, out var failure));

        Assert.Equal("path_not_allowed", failure.Errors[0].Code);
    }

    [LinuxFact]
    public void PathPolicy_RejectsFilesReachedThroughASymlinkedDirectoryInsideTheRoot()
    {
        using var root = new TempDirectory();
        using var outside = new TempDirectory();
        outside.WriteFile("secret.macro", "macro");
        _ = Directory.CreateSymbolicLink(root.Combine("escape"), outside.Path);
        var policy = CreatePolicy(McpPathSetting.MacroRead, root.Path);

        Assert.False(policy.TryAuthorize(Path.Combine(root.Combine("escape"), "secret.macro"), McpPathSetting.MacroRead, requireExisting: true, out _, out var failure));

        Assert.Equal("path_not_allowed", failure.Errors[0].Code);
    }

    [LinuxFact]
    public void PathPolicy_IgnoresConfiguredRootsThatAreSymlinks()
    {
        using var real = new TempDirectory();
        using var links = new TempDirectory();
        var link = links.Combine("root-link");
        _ = Directory.CreateSymbolicLink(link, real.Path);
        var policy = CreatePolicy(McpPathSetting.MacroRead, link);

        Assert.False(policy.TryAuthorize(Path.Combine(link, "a.macro"), McpPathSetting.MacroRead, requireExisting: false, out _, out var failure));

        Assert.Equal("path_not_allowed", failure.Errors[0].Code);
    }
}
