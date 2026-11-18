namespace CrossMacro.Core.Tests.Models;

public sealed class McpPathSettingsTests
{
    [Fact]
    public void NewSettings_HaveNoRootsForAnySetting()
    {
        var settings = new McpPathSettings();

        foreach (var setting in Enum.GetValues<McpPathSetting>())
        {
            _ = settings.GetRoots(setting).Should().BeEmpty();
        }
    }

    [Fact]
    public void Constructor_TrimsDeduplicatesAndDropsBlankRoots()
    {
        var settings = new McpPathSettings(
            macroReadRoots: [" /macros ", "/macros", "", "   ", "/other"],
            macroWriteRoots: null,
            imageReadRoots: null,
            imageWriteRoots: null,
            fileReadRoots: null,
            fileWriteRoots: null);

        _ = settings.MacroReadRoots.Should().Equal("/macros", "/other");
        _ = settings.MacroWriteRoots.Should().BeEmpty();
    }

    [Fact]
    public void WithRoots_ReplacesOnlyTheRequestedSettingAndLeavesTheOriginalUntouched()
    {
        var original = new McpPathSettings();

        var updated = original.WithRoots(McpPathSetting.ImageWrite, ["/images"]);

        _ = updated.GetRoots(McpPathSetting.ImageWrite).Should().Equal("/images");
        foreach (var setting in Enum.GetValues<McpPathSetting>().Where(setting => setting is not McpPathSetting.ImageWrite))
        {
            _ = updated.GetRoots(setting).Should().BeEmpty();
        }

        _ = original.GetRoots(McpPathSetting.ImageWrite).Should().BeEmpty();
    }

    [Fact]
    public void WithRoots_EachSettingMapsToItsOwnRootList()
    {
        var settings = new McpPathSettings();
        foreach (var setting in Enum.GetValues<McpPathSetting>())
        {
            settings = settings.WithRoots(setting, [$"/{setting}"]);
        }

        foreach (var setting in Enum.GetValues<McpPathSetting>())
        {
            _ = settings.GetRoots(setting).Should().Equal($"/{setting}");
        }
    }

    [Fact]
    public void WithRoots_NullRootsClearsTheSetting()
    {
        var settings = new McpPathSettings().WithRoots(McpPathSetting.FileRead, ["/files"]);

        _ = settings.WithRoots(McpPathSetting.FileRead, roots: null).GetRoots(McpPathSetting.FileRead).Should().BeEmpty();
    }

    [Fact]
    public void GetRootsAndWithRoots_RejectUnknownSettings()
    {
        var settings = new McpPathSettings();
        var unknown = (McpPathSetting)999;

        _ = FluentActions.Invoking(() => settings.GetRoots(unknown)).Should().Throw<ArgumentOutOfRangeException>();
        _ = FluentActions.Invoking(() => settings.WithRoots(unknown, roots: [])).Should().Throw<ArgumentOutOfRangeException>();
    }
}
