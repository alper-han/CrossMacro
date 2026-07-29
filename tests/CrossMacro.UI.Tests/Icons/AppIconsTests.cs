
namespace CrossMacro.UI.Tests.Icons;

public sealed class AppIconsTests
{
    [Fact]
    public void GetImageSource_ForEveryBundledColorIcon_ReturnsGeneratedVectorImage()
    {
        foreach (var icon in Enum.GetValues<AppIcon>())
        {
            if (EmojiAppIcon.GetAssetName(icon) is null)
            {
                continue;
            }

            var source = EmojiAppIcon.GetImageSource(icon);

            _ = source.Should().NotBeNull();
            _ = source!.Size.Should().Be(new Size(128, 128));
        }
    }

    [Fact]
    public void BundledAssetMapping_ContainsAllOriginalColorIcons()
    {
        var expected = new[]
        {
            "arrowNorthEast", "calendar", "cancel", "clipboard", "clock", "delete", "edit",
            "editNote", "folderOpen", "keyboard", "location", "mouse", "play", "record", "save",
            "settings", "stop", "success", "timer", "tip", "tools", "trigger", "warning",
        };

        _ = Enum.GetValues<AppIcon>()
            .Select(EmojiAppIcon.GetAssetName)
            .Where(name => name is not null)
            .Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void GetImageSource_WhenIconIsInfo_ReturnsInformationalVectorImage()
    {
        _ = EmojiAppIcon.GetImageSource(AppIcon.Info).Should().NotBeNull();
    }

    [Fact]
    public void GetPath_ForEveryDefinedIcon_ReturnsNonEmptyPath()
    {
        foreach (var icon in Enum.GetValues<AppIcon>())
        {
            _ = AppIcons.GetPath(icon).Should().NotBeNullOrWhiteSpace($"{icon} must have a vector path");
        }
    }

    [Fact]
    public void GetPath_WhenIconValueIsUnknown_Throws()
    {
        const AppIcon invalid = (AppIcon)(-1);

        var act = () => AppIcons.GetPath(invalid);

        _ = act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void AppIconGeometryConverter_ConvertBack_Throws()
    {
        var act = () => AppIconGeometryConverter.Instance.ConvertBack(value: null, typeof(AppIcon), parameter: null, CultureInfo.InvariantCulture);

        _ = act.Should().Throw<NotSupportedException>();
    }

}
