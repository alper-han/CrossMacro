using System;
using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace CrossMacro.UI.Converters;

public sealed class NumericUpDownValueConverter : IValueConverter
{
    public static readonly NumericUpDownValueConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            null => null,
            decimal decimalValue => decimalValue,
            int intValue => (decimal)intValue,
            long longValue => (decimal)longValue,
            double doubleValue => (decimal)doubleValue,
            float floatValue => (decimal)floatValue,
            _ => value
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null)
        {
            return BindingOperations.DoNothing;
        }

        if (value is not decimal decimalValue)
        {
            return BindingOperations.DoNothing;
        }

        var destinationType = Nullable.GetUnderlyingType(targetType) ?? targetType;
        if (destinationType == typeof(int))
        {
            var integerValue = decimal.Truncate(decimalValue);
            if (integerValue < int.MinValue || integerValue > int.MaxValue)
            {
                return BindingOperations.DoNothing;
            }

            return decimal.ToInt32(integerValue);
        }

        if (destinationType == typeof(decimal))
        {
            return decimalValue;
        }

        return BindingOperations.DoNothing;
    }
}
