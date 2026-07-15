using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;

namespace CrossMacro.UI.Views.Tabs;

/// <summary>
/// Converts block indent level into left margin for the action list.
/// </summary>
public class IndentLevelToMarginConverter : IValueConverter
{
    public static readonly IndentLevelToMarginConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int indentLevel && indentLevel > 0)
        {
            return new Thickness(indentLevel * 14, 0, 0, 0);
        }

        return new Thickness(0);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
