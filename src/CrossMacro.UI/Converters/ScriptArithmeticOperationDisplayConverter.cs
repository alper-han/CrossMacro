
namespace CrossMacro.UI.Converters;

public class ScriptArithmeticOperationDisplayConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            ScriptArithmeticOperation operation => EditorScriptDisplayConverters.FormatArithmeticOperation(operation),
            _ => System.Convert.ToString(value, culture) ?? string.Empty,
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
