using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace CrossMacro.UI.Icons;

public sealed class AppIconGeometryConverter : IValueConverter
{
    public static readonly AppIconGeometryConverter Instance = new();

    private AppIconGeometryConverter()
    {
    }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not AppIcon icon)
        {
            return AppIcons.Get(AppIcon.Info);
        }

        try
        {
            return AppIcons.Get(icon);
        }
        catch (ArgumentOutOfRangeException)
        {
            return AppIcons.Get(AppIcon.Info);
        }
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
