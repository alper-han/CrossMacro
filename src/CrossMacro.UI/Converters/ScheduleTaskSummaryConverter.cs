namespace CrossMacro.UI.Converters;

/// <summary>
/// Builds the schedule list summary from a task's <see cref="ScheduleType"/> and macro file path.
/// Bound through a <see cref="MultiBinding"/> so the text follows edits to either value.
/// </summary>
public sealed class ScheduleTaskSummaryConverter : IMultiValueConverter
{
    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values is not [ScheduleType type, string macroFilePath])
        {
            return AvaloniaProperty.UnsetValue;
        }

        var typeDisplay = LocalizationService.Get(type switch
        {
            ScheduleType.Interval => "Schedule_TypeInterval",
            ScheduleType.SpecificTime => "Schedule_TypeDateTime",
            ScheduleType.Weekly => "Schedule_TypeWeekly",
            _ => throw new ArgumentOutOfRangeException(nameof(values), type, message: null),
        });

        var fileDisplay = string.IsNullOrEmpty(macroFilePath)
            ? LocalizationService.Get("Schedule_NoFile")
            : Path.GetFileName(macroFilePath);

        var summaryFormat = LocalizationService.Get("Schedule_ListSummary");
        return string.Format(culture, summaryFormat, typeDisplay, fileDisplay);
    }
}
