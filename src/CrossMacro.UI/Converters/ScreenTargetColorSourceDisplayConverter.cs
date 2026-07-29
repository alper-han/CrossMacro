
namespace CrossMacro.UI.Converters;

public class ScreenTargetColorSourceDisplayConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            EditorActionScreenTargetColorSource source => EditorScreenTargetColorSourceDisplayConverters.FormatSource(source),
            _ => System.Convert.ToString(value, culture) ?? string.Empty,
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
