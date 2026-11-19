namespace CrossMacro.UI.Converters;

/// <summary>Formats the bound value into the localized composite format stored under <see cref="Key"/>.</summary>
public sealed class LocalizedFormatConverter : IValueConverter
{
    public string Key { get; set; } = string.Empty;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return string.Format(culture, LocalizationService.Get(Key), value);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
