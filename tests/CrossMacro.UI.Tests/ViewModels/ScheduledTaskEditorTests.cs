namespace CrossMacro.UI.Tests.ViewModels;

public sealed class ScheduledTaskEditorTests
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
            ScheduledDateTime = new DateTime(2030, 1, 2, 3, 4, 5, DateTimeKind.Unspecified),
            WeeklyDays = ScheduleDays.Monday | ScheduleDays.Friday,
            WeeklyTime = new TimeSpan(8, 30, 0),
            LastRunTime = new DateTime(2030, 1, 1, 1, 0, 0, DateTimeKind.Utc),
            NextRunTime = new DateTime(2030, 1, 3, 1, 0, 0, DateTimeKind.Utc),
            LastStatus = "Success",
        };
        var editor = new ScheduledTaskEditor();

        editor.Load(source);
        var target = editor.ToCore();

        _ = target.Should().BeEquivalentTo(source);
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

        _ = editor.MacroFilePath.Should().Be("original.macro");
        _ = editor.IntervalValue.Should().Be(30);
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

        _ = task.IntervalValue.Should().Be(1);
        _ = task.IntervalMinValue.Should().Be(1);
        _ = task.IntervalMaxValue.Should().Be(1);
        _ = task.WeeklyTime.Should().BeLessThan(TimeSpan.FromDays(1));
    }

    [Fact]
    public void SyncRuntimeStatus_UpdatesRuntimeFieldsByEditorId()
    {
        var editor = new ScheduledTaskEditor { Id = Guid.NewGuid() };
        var last = new DateTime(2030, 1, 1, 1, 0, 0, DateTimeKind.Utc);
        var next = last.AddMinutes(30);

        editor.SyncRuntimeStatus(last, next, "Running...", isEnabled: true);

        _ = editor.LastRunTime.Should().Be(last);
        _ = editor.NextRunTime.Should().Be(next);
        _ = editor.LastStatus.Should().Be("Running...");
        _ = editor.IsEnabled.Should().BeTrue();
    }
}
