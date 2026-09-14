
namespace CrossMacro.UI.Converters;

public sealed class ScheduleTaskSummaryConverter : IValueConverter
{
    private readonly LocalizationBindingSource? _bindings = (Avalonia.Application.Current as App)?.LocalizationBindings;
    public ILocalizationService? LocalizationService { get; set; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not ScheduledTask task)
        {
            return string.Empty;
        }

        var localizationService = LocalizationService ?? _bindings?.Service;
        if (localizationService is null)
        {
            var fileName = string.IsNullOrEmpty(task.MacroFilePath) ? "No file" : System.IO.Path.GetFileName(task.MacroFilePath);
            return $"{task.Type} • {fileName}";
        }

        var typeDisplay = task.Type switch
        {
            ScheduleType.Interval => localizationService["Schedule_TypeInterval"],
            ScheduleType.SpecificTime => localizationService["Schedule_TypeDateTime"],
            ScheduleType.Weekly => localizationService["Schedule_TypeWeekly"],
            _ => task.Type.ToString(),
        };

        var fileDisplay = string.IsNullOrEmpty(task.MacroFilePath)
            ? localizationService["Schedule_NoFile"]
            : System.IO.Path.GetFileName(task.MacroFilePath);

        return string.Format(localizationService.CurrentCulture, localizationService["Schedule_ListSummary"], typeDisplay, fileDisplay);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}
