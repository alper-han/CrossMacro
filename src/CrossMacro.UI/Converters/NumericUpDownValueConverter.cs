using Avalonia.Data;

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
            double doubleValue => System.Convert.ToDecimal(doubleValue, CultureInfo.InvariantCulture),
            float floatValue => System.Convert.ToDecimal(floatValue, CultureInfo.InvariantCulture),
            _ => value,
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
            if (integerValue is < int.MinValue or > int.MaxValue)
            {
                return BindingOperations.DoNothing;
            }

            return decimal.ToInt32(integerValue);
        }

        if (destinationType == typeof(decimal))
        {
            return decimalValue;
        }

        if (destinationType == typeof(double))
        {
            return decimal.ToDouble(decimalValue);
        }

        return BindingOperations.DoNothing;
    }
}
