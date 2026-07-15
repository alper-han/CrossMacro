
namespace CrossMacro.UI.Views.Tabs;

public class ScriptConditionOperatorDisplayConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            ScriptConditionOperator conditionOperator => EditorScriptDisplayConverters.FormatConditionOperator(conditionOperator),
            _ => value?.ToString() ?? string.Empty,
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
