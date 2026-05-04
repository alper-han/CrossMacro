using System;
using System.Globalization;
using System.IO;
using CrossMacro.UI.Icons;
using FluentAssertions;
using Xunit;

namespace CrossMacro.UI.Tests.Icons;

public sealed class AppIconsTests
{
    [Fact]
    public void GetPath_ForEveryDefinedIcon_ReturnsNonEmptyPath()
    {
        foreach (var icon in Enum.GetValues<AppIcon>())
        {
            AppIcons.GetPath(icon).Should().NotBeNullOrWhiteSpace($"{icon} must have a vector path");
        }
    }

    [Fact]
    public void GetPath_WhenIconValueIsUnknown_Throws()
    {
        var invalid = (AppIcon)(-1);

        var act = () => AppIcons.GetPath(invalid);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void AppIconGeometryConverter_ConvertBack_Throws()
    {
        var act = () => AppIconGeometryConverter.Instance.ConvertBack(null, typeof(AppIcon), null, CultureInfo.InvariantCulture);

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void EmojiAppIcon_MappedAssets_ExistForEveryMappedIcon()
    {
        var assetRoot = GetEmojiSvgAssetRoot();

        foreach (var icon in Enum.GetValues<AppIcon>())
        {
            var assetName = EmojiAppIcon.GetAssetName(icon);
            if (assetName is null)
            {
                continue;
            }

            File.Exists(Path.Combine(assetRoot, $"{assetName}.svg"))
                .Should().BeTrue($"{icon} maps to bundled Noto SVG asset {assetName}.svg");
        }
    }

    [Theory]
    [InlineData(AppIcon.Record)]
    [InlineData(AppIcon.Play)]
    [InlineData(AppIcon.Save)]
    [InlineData(AppIcon.EditNote)]
    [InlineData(AppIcon.Keyboard)]
    [InlineData(AppIcon.Clock)]
    [InlineData(AppIcon.Tools)]
    [InlineData(AppIcon.Settings)]
    [InlineData(AppIcon.Mouse)]
    [InlineData(AppIcon.Success)]
    [InlineData(AppIcon.Location)]
    [InlineData(AppIcon.ArrowNorthEast)]
    [InlineData(AppIcon.Stop)]
    [InlineData(AppIcon.Tip)]
    [InlineData(AppIcon.Delete)]
    [InlineData(AppIcon.FolderOpen)]
    [InlineData(AppIcon.Edit)]
    [InlineData(AppIcon.Calendar)]
    [InlineData(AppIcon.Timer)]
    [InlineData(AppIcon.Clipboard)]
    [InlineData(AppIcon.Cancel)]
    [InlineData(AppIcon.Warning)]
    public void EmojiAppIcon_ExpectedColoredIcons_AreMapped(AppIcon icon)
    {
        EmojiAppIcon.GetAssetName(icon).Should().NotBeNullOrWhiteSpace($"{icon} should render as a bundled Noto SVG emoji");
    }

    [Fact]
    public void EmojiAppIcon_GetAssetName_WhenIconIsUnsupported_ReturnsNull()
    {
        EmojiAppIcon.GetAssetName(AppIcon.Close).Should().BeNull("Close is a structural chrome icon, not a colored emoji asset");
    }

    [Fact]
    public void EmojiAppIcon_GetAssetUri_ReturnsCrossMacroAvaloniaResourceUri()
    {
        EmojiAppIcon.GetAssetUri(AppIcon.Record)
            .Should().Be("avares://CrossMacro.UI.Core/Assets/Emoji/NotoColorEmoji/Svg/record.svg");
    }

    [Fact]
    public void EmojiAppIcon_GetAssetUri_WhenIconIsUnsupported_Throws()
    {
        var act = () => EmojiAppIcon.GetAssetUri(AppIcon.Close);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    private static string GetEmojiSvgAssetRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            var candidate = Path.Combine(directory.FullName, "src", "CrossMacro.UI", "Assets", "Emoji", "NotoColorEmoji", "Svg");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate src/CrossMacro.UI/Assets/Emoji/NotoColorEmoji/Svg from the test output directory.");
    }

}
