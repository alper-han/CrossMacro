using Avalonia.Media;
using CrossMacro.UI.Icons;

namespace CrossMacro.UI.Tests.Icons;

public sealed class AppIconsTests
{
    private static readonly AppIcon[] EmojiIcons =
    [
        AppIcon.ArrowNorthEast, AppIcon.Calendar, AppIcon.Cancel, AppIcon.Clipboard, AppIcon.Clock,
        AppIcon.Delete, AppIcon.Edit, AppIcon.EditNote, AppIcon.FolderOpen, AppIcon.Info,
        AppIcon.Keyboard, AppIcon.Location, AppIcon.Mouse, AppIcon.Play, AppIcon.Record,
        AppIcon.Save, AppIcon.Settings, AppIcon.Stop, AppIcon.Success, AppIcon.Timer,
        AppIcon.Tip, AppIcon.Tools, AppIcon.Trigger, AppIcon.Warning,
    ];

    [Fact]
    public void GetImageSource_ForEveryEmojiIcon_ReturnsGeneratedVectorImage()
    {
        foreach (var icon in EmojiIcons)
        {
            var source = EmojiAppIcon.GetImageSource(icon);

            _ = source.Should().NotBeNull($"{icon} has a generated picture");
            _ = source.Size.Should().Be(new Size(128, 128));
        }
    }

    [Fact]
    public void GetImageSource_ForPathOnlyIcons_ReturnsNull()
    {
        foreach (var icon in Enum.GetValues<AppIcon>().Except(EmojiIcons))
        {
            _ = EmojiAppIcon.GetImageSource(icon).Should().BeNull($"{icon} is drawn from a vector path");
        }
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

}
