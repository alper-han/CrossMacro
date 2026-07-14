using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace CrossMacro.UI.Icons;

public sealed class EmojiAppIcon : Image
{
    private static readonly IReadOnlyDictionary<AppIcon, Lazy<Bitmap>> Sources = Enum.GetValues<AppIcon>()
        .Select(icon => new { Icon = icon, AssetName = GetAssetName(icon) })
        .Where(entry => entry.AssetName is not null)
        .ToDictionary(
            entry => entry.Icon,
            entry => new Lazy<Bitmap>(() => LoadBitmap(GetAssetUri(entry.Icon))));

    public static readonly StyledProperty<AppIcon> IconProperty = AvaloniaProperty.Register<EmojiAppIcon, AppIcon>(
        nameof(Icon),
        AppIcon.Info);

    static EmojiAppIcon()
    {
        IconProperty.Changed.AddClassHandler<EmojiAppIcon>((icon, _) => icon.UpdateSource());
    }

    public EmojiAppIcon()
    {
        Stretch = Stretch.Uniform;
        UpdateSource();
    }

    public AppIcon Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    private void UpdateSource()
    {
        Source = Sources.TryGetValue(Icon, out var source) ? source.Value : null;
    }

    public static string GetAssetUri(AppIcon icon)
    {
        var assetName = GetAssetName(icon);
        if (assetName is null)
        {
            throw new ArgumentOutOfRangeException(nameof(icon), icon, "The icon does not have a bundled PNG asset.");
        }

        return $"avares://CrossMacro.UI.Core/Assets/Emoji/NotoColorEmoji/Png/{assetName}.png";
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
            AppIcon.Trigger => "trigger",
            _ => null
        };
    }

    private static Bitmap LoadBitmap(string uri)
    {
        using var stream = AssetLoader.Open(new Uri(uri));
        return new Bitmap(stream);
    }
}
