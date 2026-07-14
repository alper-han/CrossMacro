using System;
using System.Globalization;
using Avalonia.Platform;
using CrossMacro.UI.Icons;
using FluentAssertions;
using Xunit;

namespace CrossMacro.UI.Tests.Icons;

public sealed class AppIconsTests
{
    [Fact]
    public void GetAssetUri_ForEveryBundledIcon_ReturnsPngResourceUri()
    {
        foreach (var icon in Enum.GetValues<AppIcon>())
        {
            var assetName = EmojiAppIcon.GetAssetName(icon);
            if (assetName is null)
            {
                continue;
            }

            EmojiAppIcon.GetAssetUri(icon).Should().Be(
                $"avares://CrossMacro.UI.Core/Assets/Emoji/NotoColorEmoji/Png/{assetName}.png");
        }
    }

    [Fact]
    public void BundledAssetMapping_ContainsAllOriginalColorIcons()
    {
        var expected = new[]
        {
            "arrowNorthEast", "calendar", "cancel", "clipboard", "clock", "delete", "edit",
            "editNote", "folderOpen", "keyboard", "location", "mouse", "play", "record", "save",
            "settings", "stop", "success", "timer", "tip", "tools", "trigger", "warning"
        };

        Enum.GetValues<AppIcon>()
            .Select(EmojiAppIcon.GetAssetName)
            .Where(name => name is not null)
            .Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void BundledPngAssets_AreEmbeddedAndHavePngSignatures()
    {
        foreach (var icon in Enum.GetValues<AppIcon>())
        {
            if (EmojiAppIcon.GetAssetName(icon) is null)
            {
                continue;
            }

            using var stream = new StandardAssetLoader().Open(new Uri(EmojiAppIcon.GetAssetUri(icon)), null);
            var signature = new byte[8];
            stream.ReadExactly(signature);
            signature.Should().Equal(137, 80, 78, 71, 13, 10, 26, 10);
        }
    }

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

}
