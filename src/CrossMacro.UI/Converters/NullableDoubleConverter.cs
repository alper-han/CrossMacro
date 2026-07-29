
namespace CrossMacro.UI.Converters;

public class NullableDoubleConverter : IValueConverter
{
    public static readonly NullableDoubleConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is double doubleValue)
        {
            return doubleValue.ToString("0.##", culture);
        }
        return System.Convert.ToString(value, culture) ?? "";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string str)
        {
            if (string.IsNullOrWhiteSpace(str))
            {
                return 0.0;
            }

            var normalized = str.Replace(',', '.').Trim();

            if (double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out double result))
            {
                return result;
            }

            return Avalonia.Data.BindingOperations.DoNothing;
        }
        return Avalonia.Data.BindingOperations.DoNothing;
    }
}
