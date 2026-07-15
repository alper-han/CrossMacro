
namespace CrossMacro.UI.Views.Tabs;

public class ScreenTargetColorSourceDisplayConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            EditorActionScreenTargetColorSource source => EditorScreenTargetColorSourceDisplayConverters.FormatSource(source),
            _ => value?.ToString() ?? string.Empty,
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
