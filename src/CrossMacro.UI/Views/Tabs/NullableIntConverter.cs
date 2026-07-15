using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace CrossMacro.UI.Views.Tabs;

/// <summary>
/// Converter for int properties that handles empty/invalid string input gracefully.
/// Empty string = 0, invalid text = keeps previous value (DoNothing).
/// </summary>
public class NullableIntConverter : IValueConverter
{
    public static readonly NullableIntConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int intValue)
        {
            return intValue.ToString();
        }
        return value?.ToString() ?? "";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string str)
        {
            // Empty string = 0 (explicit clear)
            if (string.IsNullOrWhiteSpace(str))
                return 0;

            // Valid number = use it (clamped for key codes if needed)
            if (int.TryParse(str, out int result))
            {
                return result;
            }

            // Invalid text (like "a") = don't update, keep previous value
            return Avalonia.Data.BindingOperations.DoNothing;
        }
        return Avalonia.Data.BindingOperations.DoNothing;
    }
}
