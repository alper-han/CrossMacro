namespace CrossMacro.UI.Converters;

/// <summary>
/// Shows an enum member as the localized text stored under <see cref="KeyPrefix"/> + member name.
/// A member without a resource resolves to its key so a missing translation is visible in the UI.
/// </summary>
public sealed class LocalizedEnumConverter : IValueConverter
{
    public string KeyPrefix { get; set; } = string.Empty;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is Enum member
            ? LocalizationService.Get(string.Concat(KeyPrefix, member.ToString()))
            : string.Empty;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
