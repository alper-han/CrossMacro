
namespace CrossMacro.UI.Icons;

public sealed class EmojiAppIcon : Image
{
    private static readonly Dictionary<AppIcon, Lazy<IImage>> Sources = new Dictionary<AppIcon, Lazy<IImage>>
    {
        [AppIcon.ArrowNorthEast] = CreateSource(ArrowNorthEastEmojiIcon.Picture),
        [AppIcon.Calendar] = CreateSource(CalendarEmojiIcon.Picture),
        [AppIcon.Cancel] = CreateSource(CancelEmojiIcon.Picture),
        [AppIcon.Clipboard] = CreateSource(ClipboardEmojiIcon.Picture),
        [AppIcon.Clock] = CreateSource(ClockEmojiIcon.Picture),
        [AppIcon.Delete] = CreateSource(DeleteEmojiIcon.Picture),
        [AppIcon.Edit] = CreateSource(EditEmojiIcon.Picture),
        [AppIcon.EditNote] = CreateSource(EditNoteEmojiIcon.Picture),
        [AppIcon.FolderOpen] = CreateSource(FolderOpenEmojiIcon.Picture),
        [AppIcon.Info] = CreateSource(TipEmojiIcon.Picture),
        [AppIcon.Keyboard] = CreateSource(KeyboardEmojiIcon.Picture),
        [AppIcon.Location] = CreateSource(LocationEmojiIcon.Picture),
        [AppIcon.Mouse] = CreateSource(MouseEmojiIcon.Picture),
        [AppIcon.Play] = CreateSource(PlayEmojiIcon.Picture),
        [AppIcon.Record] = CreateSource(RecordEmojiIcon.Picture),
        [AppIcon.Save] = CreateSource(SaveEmojiIcon.Picture),
        [AppIcon.Settings] = CreateSource(SettingsEmojiIcon.Picture),
        [AppIcon.Stop] = CreateSource(StopEmojiIcon.Picture),
        [AppIcon.Success] = CreateSource(SuccessEmojiIcon.Picture),
        [AppIcon.Timer] = CreateSource(TimerEmojiIcon.Picture),
        [AppIcon.Tip] = CreateSource(TipEmojiIcon.Picture),
        [AppIcon.Tools] = CreateSource(ToolsEmojiIcon.Picture),
        [AppIcon.Trigger] = CreateSource(TriggerEmojiIcon.Picture),
        [AppIcon.Warning] = CreateSource(WarningEmojiIcon.Picture),
    };

    public static readonly StyledProperty<AppIcon> IconProperty = AvaloniaProperty.Register<EmojiAppIcon, AppIcon>(
        nameof(Icon),
        AppIcon.Info);

    static EmojiAppIcon()
    {
        _ = IconProperty.Changed.AddClassHandler<EmojiAppIcon>((icon, _) => icon.UpdateSource());
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
        Source = GetImageSource(Icon);
    }

    public static IImage? GetImageSource(AppIcon icon)
    {
        return Sources.TryGetValue(icon, out var source) ? source.Value : null;
    }

    private static Lazy<IImage> CreateSource(SkiaSharp.SKPicture picture)
    {
        return new Lazy<IImage>(() => new StaticSkPictureImage(picture));
    }
}
