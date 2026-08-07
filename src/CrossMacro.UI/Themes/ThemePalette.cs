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

    public static IReadOnlyList<string> ColorKeys { get; } =
        new ReadOnlyCollection<string>(
        [
            nameof(PrimaryColor),
            nameof(PrimaryHoverColor),
            nameof(PrimaryPressedColor),
            nameof(SuccessColor),
            nameof(SuccessHoverColor),
            nameof(SuccessPressedColor),
            nameof(DangerColor),
            nameof(DangerHoverColor),
            nameof(DangerPressedColor),
            nameof(BackgroundColor),
            nameof(SurfaceColor),
            nameof(SurfaceHoverColor),
            nameof(TextPrimaryColor),
            nameof(TextSecondaryColor),
            nameof(WarningColor),
            nameof(WarningHoverColor),
            nameof(AccentColor),
            nameof(SystemAccentColor),
            nameof(SystemAccentColorDark1),
            nameof(SystemAccentColorDark2),
            nameof(SystemAccentColorDark3),
            nameof(SystemAccentColorLight1),
            nameof(SystemAccentColorLight2),
            nameof(SystemAccentColorLight3),
            nameof(TextOnPrimaryColor),
            nameof(TextOnSuccessColor),
            nameof(TextOnDangerColor),
            nameof(TextOnWarningColor),
        ]);

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
        yield return new(nameof(PrimaryColor), PrimaryColor);
        yield return new(nameof(PrimaryHoverColor), PrimaryHoverColor);
        yield return new(nameof(PrimaryPressedColor), PrimaryPressedColor);
        yield return new(nameof(SuccessColor), SuccessColor);
        yield return new(nameof(SuccessHoverColor), SuccessHoverColor);
        yield return new(nameof(SuccessPressedColor), SuccessPressedColor);
        yield return new(nameof(DangerColor), DangerColor);
        yield return new(nameof(DangerHoverColor), DangerHoverColor);
        yield return new(nameof(DangerPressedColor), DangerPressedColor);
        yield return new(nameof(BackgroundColor), BackgroundColor);
        yield return new(nameof(SurfaceColor), SurfaceColor);
        yield return new(nameof(SurfaceHoverColor), SurfaceHoverColor);
        yield return new(nameof(TextPrimaryColor), TextPrimaryColor);
        yield return new(nameof(TextSecondaryColor), TextSecondaryColor);
        yield return new(nameof(WarningColor), WarningColor);
        yield return new(nameof(WarningHoverColor), WarningHoverColor);
        yield return new(nameof(AccentColor), AccentColor);
        yield return new(nameof(SystemAccentColor), SystemAccentColor);
        yield return new(nameof(SystemAccentColorDark1), SystemAccentColorDark1);
        yield return new(nameof(SystemAccentColorDark2), SystemAccentColorDark2);
        yield return new(nameof(SystemAccentColorDark3), SystemAccentColorDark3);
        yield return new(nameof(SystemAccentColorLight1), SystemAccentColorLight1);
        yield return new(nameof(SystemAccentColorLight2), SystemAccentColorLight2);
        yield return new(nameof(SystemAccentColorLight3), SystemAccentColorLight3);
        yield return new(nameof(TextOnPrimaryColor), TextOnPrimaryColor);
        yield return new(nameof(TextOnSuccessColor), TextOnSuccessColor);
        yield return new(nameof(TextOnDangerColor), TextOnDangerColor);
        yield return new(nameof(TextOnWarningColor), TextOnWarningColor);
    }
}
