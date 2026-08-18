
namespace CrossMacro.UI.Converters;

public class ScriptConditionOperatorDisplayConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            ScriptConditionOperator conditionOperator => EditorScriptDisplayConverters.FormatConditionOperator(conditionOperator),
            _ => System.Convert.ToString(value, culture) ?? string.Empty,
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
