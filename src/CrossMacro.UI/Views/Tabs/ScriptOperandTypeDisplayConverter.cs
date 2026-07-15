
namespace CrossMacro.UI.Views.Tabs;

public class ScriptOperandTypeDisplayConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            ScriptOperandType operandType => EditorScriptDisplayConverters.FormatOperandType(operandType),
            _ => value?.ToString() ?? string.Empty,
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
