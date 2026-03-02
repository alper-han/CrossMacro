using System;
using System.Globalization;
using System.IO;
using FluentAssertions;

namespace CrossMacro.UI.Tests.Theming;

public class ThemeContrastComplianceTests
{
    [Theory]
    [InlineData("PrimaryColor", "TextOnPrimaryColor", 4.5)]
    [InlineData("PrimaryHoverColor", "TextOnPrimaryColor", 4.5)]
    [InlineData("PrimaryPressedColor", "TextOnPrimaryColor", 4.5)]
    [InlineData("SuccessColor", "TextOnSuccessColor", 4.5)]
    [InlineData("SuccessHoverColor", "TextOnSuccessColor", 4.5)]
    [InlineData("SuccessPressedColor", "TextOnSuccessColor", 4.5)]
    [InlineData("DangerColor", "TextOnDangerColor", 4.5)]
    [InlineData("DangerHoverColor", "TextOnDangerColor", 4.5)]
    [InlineData("DangerPressedColor", "TextOnDangerColor", 4.5)]
    [InlineData("WarningColor", "TextOnWarningColor", 4.5)]
    [InlineData("WarningHoverColor", "TextOnWarningColor", 4.5)]
    [InlineData("BackgroundColor", "TextPrimaryColor", 4.5)]
    [InlineData("SurfaceHoverColor", "TextPrimaryColor", 4.5)]
    [InlineData("SurfaceColor", "TextSecondaryColor", 3.0)]
    public void ThemeColors_ShouldMeetContrastTargets(string backgroundKey, string foregroundKey, double minRatio)
    {
        var themeFiles = ThemeTestFileHelper.GetThemeFiles();
        themeFiles.Should().NotBeEmpty();

        foreach (var themeFile in themeFiles)
        {
            var background = ThemeTestFileHelper.ReadColorValue(themeFile, backgroundKey);
            var foreground = ThemeTestFileHelper.ReadColorValue(themeFile, foregroundKey);
            var ratio = ContrastRatio(background, foreground);
            ratio.Should().BeGreaterThanOrEqualTo(
                minRatio,
                because:
                $"{Path.GetFileName(themeFile)} must satisfy contrast for {foregroundKey} on {backgroundKey}");
        }
    }

    private static double ContrastRatio(string first, string second)
    {
        var lumA = RelativeLuminance(ParseColor(first));
        var lumB = RelativeLuminance(ParseColor(second));
        var lighter = Math.Max(lumA, lumB);
        var darker = Math.Min(lumA, lumB);
        return (lighter + 0.05) / (darker + 0.05);
    }

    private static (double R, double G, double B) ParseColor(string hex)
    {
        var normalized = hex.Trim();
        if (normalized.StartsWith("#", StringComparison.Ordinal))
        {
            normalized = normalized[1..];
        }

        if (normalized.Length == 8)
        {
            normalized = normalized[2..];
        }

        if (normalized.Length != 6)
        {
            throw new InvalidOperationException($"Unsupported color format: '{hex}'");
        }

        var red = int.Parse(normalized.AsSpan(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255d;
        var green = int.Parse(normalized.AsSpan(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255d;
        var blue = int.Parse(normalized.AsSpan(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255d;
        return (red, green, blue);
    }

    private static double RelativeLuminance((double R, double G, double B) color)
    {
        return 0.2126 * Linearize(color.R) + 0.7152 * Linearize(color.G) + 0.0722 * Linearize(color.B);
    }

    private static double Linearize(double channel)
    {
        return channel <= 0.03928
            ? channel / 12.92
            : Math.Pow((channel + 0.055) / 1.055, 2.4);
    }
}
