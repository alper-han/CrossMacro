using Avalonia.Data;
using CrossMacro.UI.Tests.Localization;
using CrossMacro.UI.Tests.Services;

namespace CrossMacro.UI.Tests.Converters;

[Collection(TestCollections.HeadlessStartup)]
public sealed class ScheduleTaskSummaryConverterTests
{
    [Fact]
    public Task BoundToScheduledTaskEditor_ShowsTypeAndFileAndFollowsEdits() =>
        HeadlessTestRunner.RunAsync(async _ =>
        {
            using var cultureScope = new LocalizationCultureScope("en");
            var editor = new ScheduledTaskEditor { Type = ScheduleType.Weekly, MacroFilePath = string.Empty };
            var summary = new TextBlock { DataContext = editor };
            summary.Bind(TextBlock.TextProperty, new MultiBinding
            {
                Converter = new ScheduleTaskSummaryConverter(),
                Bindings = [new Binding(nameof(ScheduledTaskEditor.Type)), new Binding(nameof(ScheduledTaskEditor.MacroFilePath))],
            });
            var window = new Window { Content = summary };
            window.Show();

            try
            {
                Assert.Equal("Weekly • No file", summary.Text);

                editor.MacroFilePath = "/macros/open-browser.json";
                editor.Type = ScheduleType.Interval;
                await Task.Yield();

                Assert.Equal("Interval (repeat) • open-browser.json", summary.Text);
            }
            finally
            {
                window.Close();
            }
        });

    [Fact]
    public void Convert_WhenValuesAreNotYetAvailable_LeavesTextUnset()
    {
        var result = new ScheduleTaskSummaryConverter().Convert(
            [AvaloniaProperty.UnsetValue, AvaloniaProperty.UnsetValue],
            typeof(string),
            parameter: null,
            CultureInfo.InvariantCulture);

        Assert.Same(AvaloniaProperty.UnsetValue, result);
    }
}
