
namespace CrossMacro.UI.Views.Tabs;

public static class ScheduleTaskConverters
{
    private static ILocalizationService? _localizationService;

    public static void Configure(ILocalizationService localizationService)
    {
        _localizationService = localizationService;
    }

    public static readonly IValueConverter SummaryText = new FuncValueConverter<ScheduledTask?, string>(task =>
    {
        if (task is null)
        {
            return string.Empty;
        }

        var localizationService = _localizationService;
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
    });
}
