using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Svg.Skia;

namespace CrossMacro.UI.Icons;

public sealed class EmojiAppIcon : Image
{
    private static readonly IReadOnlyDictionary<AppIcon, Lazy<SvgSource>> Sources = Enum.GetValues<AppIcon>()
        .Select(icon => new { Icon = icon, AssetName = GetAssetName(icon) })
        .Where(entry => entry.AssetName is not null)
        .ToDictionary(
            entry => entry.Icon,
            entry => new Lazy<SvgSource>(() => SvgSource.Load(GetAssetUri(entry.Icon), null)));

    public static readonly StyledProperty<AppIcon> IconProperty = AvaloniaProperty.Register<EmojiAppIcon, AppIcon>(
        nameof(Icon),
        AppIcon.Info);

    static EmojiAppIcon()
    {
        IconProperty.Changed.AddClassHandler<EmojiAppIcon>((icon, _) => icon.UpdateSource());
    }

    public EmojiAppIcon()
    {
        Stretch = Avalonia.Media.Stretch.Uniform;
        UpdateSource();
    }

    public AppIcon Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    private void UpdateSource()
    {
        if (!Sources.TryGetValue(Icon, out var source))
        {
            Source = null;
            return;
        }

        Source = new SvgImage
        {
            Source = source.Value
        };
    }

    public static string GetAssetUri(AppIcon icon)
    {
        var assetName = GetAssetName(icon);
        if (assetName == null)
        {
            throw new ArgumentOutOfRangeException(nameof(icon), icon, "The icon does not have a bundled SVG asset.");
        }

        // Only trusted, bundled Avalonia resources are loaded through SvgSource. Do not pass
        // user-provided SVG paths or arbitrary asset names into the SVG renderer.
        return $"avares://CrossMacro.UI.Core/Assets/Emoji/NotoColorEmoji/Svg/{assetName}.svg";
    }

    public static string? GetAssetName(AppIcon icon)
    {
        return icon switch
        {
            AppIcon.Record => "record",
            AppIcon.Play => "play",
            AppIcon.Save => "save",
            AppIcon.EditNote => "editNote",
            AppIcon.Keyboard => "keyboard",
            AppIcon.Clock => "clock",
            AppIcon.Tools => "tools",
            AppIcon.Settings => "settings",
            AppIcon.Mouse => "mouse",
            AppIcon.Success => "success",
            AppIcon.Location => "location",
            AppIcon.ArrowNorthEast => "arrowNorthEast",
            AppIcon.Stop => "stop",
            AppIcon.Tip => "tip",
            AppIcon.Delete => "delete",
            AppIcon.FolderOpen => "folderOpen",
            AppIcon.Edit => "edit",
            AppIcon.Calendar => "calendar",
            AppIcon.Timer => "timer",
            AppIcon.Clipboard => "clipboard",
            AppIcon.Cancel => "cancel",
            AppIcon.Warning => "warning",
            AppIcon.Gamepad => "gamepad",
            _ => null
        };
    }
}
