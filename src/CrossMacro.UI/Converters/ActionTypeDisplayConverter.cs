namespace CrossMacro.UI.Converters;

public sealed class ActionTypeDisplayConverter : IValueConverter
{
    private readonly LocalizationBindingSource? _bindings = (Avalonia.Application.Current as App)?.LocalizationBindings;
    private ILocalizationService? _resolvedService;
    private EditorActionDisplayFormatter? _resolvedFormatter;
    public EditorActionDisplayFormatter? Formatter { get; set; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not EditorActionType type) { return string.Empty; }
        var service = _bindings?.Service;
        if (!ReferenceEquals(service, _resolvedService))
        {
            _resolvedService = service;
            _resolvedFormatter = service is null ? null : new EditorActionDisplayFormatter(service);
        }
        var formatter = Formatter ?? _resolvedFormatter;
        return formatter?.FormatActionType(type) ?? type.ToString();
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}
