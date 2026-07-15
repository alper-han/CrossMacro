using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace CrossMacro.UI.Views.Tabs;

public class HexColorBrushConverter : IValueConverter
{
    public static readonly HexColorBrushConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var text = value?.ToString()?.Trim();
        if (text is { Length: 6 } && uint.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var rgb))
        {
            var red = (byte)((rgb >> 16) & 0xFF);
            var green = (byte)((rgb >> 8) & 0xFF);
            var blue = (byte)(rgb & 0xFF);
            return new SolidColorBrush(Color.FromRgb(red, green, blue));
        }

        return Brushes.Transparent;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
