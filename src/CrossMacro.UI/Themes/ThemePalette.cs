using System.Collections.ObjectModel;

namespace CrossMacro.UI.Themes;

public sealed record ThemePalette
{
    public required string PrimaryColor { get; init; }

    public required string PrimaryHoverColor { get; init; }

    public required string PrimaryPressedColor { get; init; }

    public required string SuccessColor { get; init; }

    public required string SuccessHoverColor { get; init; }

    public required string SuccessPressedColor { get; init; }

    public required string DangerColor { get; init; }

    public required string DangerHoverColor { get; init; }

    public required string DangerPressedColor { get; init; }

    public required string BackgroundColor { get; init; }

    public required string SurfaceColor { get; init; }

    public required string SurfaceHoverColor { get; init; }

    public required string TextPrimaryColor { get; init; }

    public required string TextSecondaryColor { get; init; }

    public required string WarningColor { get; init; }

    public required string WarningHoverColor { get; init; }

    public required string AccentColor { get; init; }

    public required string SystemAccentColor { get; init; }

    public required string SystemAccentColorDark1 { get; init; }

    public required string SystemAccentColorDark2 { get; init; }

    public required string SystemAccentColorDark3 { get; init; }

    public required string SystemAccentColorLight1 { get; init; }

    public required string SystemAccentColorLight2 { get; init; }

    public required string SystemAccentColorLight3 { get; init; }

    public required string TextOnPrimaryColor { get; init; }

    public required string TextOnSuccessColor { get; init; }

    public required string TextOnDangerColor { get; init; }

    public required string TextOnWarningColor { get; init; }

    private static readonly (string Key, Func<ThemePalette, string?> Read)[] ColorAccessors =
    [
        (nameof(PrimaryColor), palette => palette.PrimaryColor),
        (nameof(PrimaryHoverColor), palette => palette.PrimaryHoverColor),
        (nameof(PrimaryPressedColor), palette => palette.PrimaryPressedColor),
        (nameof(SuccessColor), palette => palette.SuccessColor),
        (nameof(SuccessHoverColor), palette => palette.SuccessHoverColor),
        (nameof(SuccessPressedColor), palette => palette.SuccessPressedColor),
        (nameof(DangerColor), palette => palette.DangerColor),
        (nameof(DangerHoverColor), palette => palette.DangerHoverColor),
        (nameof(DangerPressedColor), palette => palette.DangerPressedColor),
        (nameof(BackgroundColor), palette => palette.BackgroundColor),
        (nameof(SurfaceColor), palette => palette.SurfaceColor),
        (nameof(SurfaceHoverColor), palette => palette.SurfaceHoverColor),
        (nameof(TextPrimaryColor), palette => palette.TextPrimaryColor),
        (nameof(TextSecondaryColor), palette => palette.TextSecondaryColor),
        (nameof(WarningColor), palette => palette.WarningColor),
        (nameof(WarningHoverColor), palette => palette.WarningHoverColor),
        (nameof(AccentColor), palette => palette.AccentColor),
        (nameof(SystemAccentColor), palette => palette.SystemAccentColor),
        (nameof(SystemAccentColorDark1), palette => palette.SystemAccentColorDark1),
        (nameof(SystemAccentColorDark2), palette => palette.SystemAccentColorDark2),
        (nameof(SystemAccentColorDark3), palette => palette.SystemAccentColorDark3),
        (nameof(SystemAccentColorLight1), palette => palette.SystemAccentColorLight1),
        (nameof(SystemAccentColorLight2), palette => palette.SystemAccentColorLight2),
        (nameof(SystemAccentColorLight3), palette => palette.SystemAccentColorLight3),
        (nameof(TextOnPrimaryColor), palette => palette.TextOnPrimaryColor),
        (nameof(TextOnSuccessColor), palette => palette.TextOnSuccessColor),
        (nameof(TextOnDangerColor), palette => palette.TextOnDangerColor),
        (nameof(TextOnWarningColor), palette => palette.TextOnWarningColor),
    ];

    public IReadOnlyDictionary<string, Color> ParseColors()
    {
        var colors = new Dictionary<string, Color>(StringComparer.Ordinal);
        foreach (var (key, value) in EnumerateColorValues())
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidDataException($"Theme color '{key}' is missing.");
            }

            if (!Color.TryParse(value, out var color))
            {
                throw new InvalidDataException($"Theme color '{key}' has an invalid value '{value}'.");
            }

            colors.Add(key, color);
        }

        return new ReadOnlyDictionary<string, Color>(colors);
    }

    public IEnumerable<KeyValuePair<string, string?>> EnumerateColorValues()
    {
        return ColorAccessors.Select(accessor => new KeyValuePair<string, string?>(accessor.Key, accessor.Read(this)));
    }
}
