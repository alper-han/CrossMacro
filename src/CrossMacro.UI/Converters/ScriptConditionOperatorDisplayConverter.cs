
namespace CrossMacro.UI.Converters;

public class ScriptConditionOperatorDisplayConverter : IValueConverter
{
    private readonly LocalizationBindingSource? _bindings = (Avalonia.Application.Current as App)?.LocalizationBindings;
    public ILocalizationService? LocalizationService { get; set; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            ScriptConditionOperator conditionOperator => EditorScriptDisplayConverters.FormatConditionOperator(conditionOperator, LocalizationService ?? _bindings?.Service),
            _ => System.Convert.ToString(value, culture) ?? string.Empty,
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
