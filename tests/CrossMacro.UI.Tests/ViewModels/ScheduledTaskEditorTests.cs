namespace CrossMacro.UI.Tests.ViewModels;

public class ScheduledTaskEditorTests
{
    [Fact]
    public void LoadAndApplyToCore_PreservesEveryPersistedField()
    {
        var source = new ScheduledTask
        {
            Name = "Weekly report",
            MacroFilePath = "report.macro",
            Type = ScheduleType.Weekly,
            PlaybackSpeed = 1.5,
            IsEnabled = true,
            IntervalValue = 12,
            IntervalUnit = IntervalUnit.Minutes,
            UseRandomIntervalDelay = true,
            IntervalMinValue = 2,
            IntervalMaxValue = 8,
            ScheduledDateTime = new DateTime(2030, 1, 2, 3, 4, 5),
            WeeklyDays = ScheduleDays.Monday | ScheduleDays.Friday,
            WeeklyTime = new TimeSpan(8, 30, 0),
            LastRunTime = new DateTime(2030, 1, 1, 1, 0, 0, DateTimeKind.Utc),
            NextRunTime = new DateTime(2030, 1, 3, 1, 0, 0, DateTimeKind.Utc),
            LastStatus = "Success",
        };
        var editor = new ScheduledTaskEditor();

        editor.Load(source);
        var target = editor.ToCore();

        target.Should().BeEquivalentTo(source);
    }

    [Fact]
    public void Rollback_RestoresBufferedScheduleChanges()
    {
        var source = new ScheduledTask { MacroFilePath = "original.macro", IntervalValue = 30 };
        var editor = new ScheduledTaskEditor();
        editor.Load(source);

        editor.MacroFilePath = "draft.macro";
        editor.IntervalValue = 99;
        editor.Rollback();

        editor.MacroFilePath.Should().Be("original.macro");
        editor.IntervalValue.Should().Be(30);
    }

    [Fact]
    public void InvalidScheduleInputs_AreNormalizedBeforeMapping()
    {
        var editor = new ScheduledTaskEditor
        {
            MacroFilePath = "task.macro",
            IntervalValue = 0,
            IntervalMinValue = -2,
            IntervalMaxValue = -1,
            WeeklyTime = TimeSpan.FromDays(2),
        };

        var task = editor.ToCore();

        task.IntervalValue.Should().Be(1);
        task.IntervalMinValue.Should().Be(1);
        task.IntervalMaxValue.Should().Be(1);
        task.WeeklyTime.Should().BeLessThan(TimeSpan.FromDays(1));
    }

    [Fact]
    public void SyncRuntimeStatus_UpdatesRuntimeFieldsByEditorId()
    {
        var editor = new ScheduledTaskEditor { Id = Guid.NewGuid() };
        var last = new DateTime(2030, 1, 1, 1, 0, 0, DateTimeKind.Utc);
        var next = last.AddMinutes(30);

        editor.SyncRuntimeStatus(last, next, "Running...", isEnabled: true);

        editor.LastRunTime.Should().Be(last);
        editor.NextRunTime.Should().Be(next);
        editor.LastStatus.Should().Be("Running...");
        editor.IsEnabled.Should().BeTrue();
    }
}
