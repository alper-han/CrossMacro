using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace CrossMacro.UI.Views.Tabs;

public class IndexConverter : IValueConverter
{
    public static readonly IndexConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return "•";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
