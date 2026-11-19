namespace CrossMacro.UI.Tests.ViewModels;

public sealed class ScheduledTaskEditorTests
{
    [Fact]
    public void Load_WhenSourceIsNull_ThrowsArgumentNullException()
    {
        var editor = new ScheduledTaskEditor();

        _ = Assert.Throws<ArgumentNullException>(() => editor.Load(null!));
    }

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
    public void IsDirty_TracksUserEditsButNotLoadOrRuntimeStatus()
    {
        var editor = new ScheduledTaskEditor();

        editor.Load(new ScheduledTask { MacroFilePath = "original.macro", IntervalValue = 30 });
        Assert.False(editor.IsDirty);

        editor.SyncRuntimeStatus(DateTime.UtcNow, DateTime.UtcNow.AddMinutes(1), "Running...", isEnabled: true);
        editor.CalculateNextRunTime(DateTime.UtcNow);
        Assert.False(editor.IsDirty);

        editor.IntervalValue = 99;
        Assert.True(editor.IsDirty);

        editor.Load(new ScheduledTask { MacroFilePath = "original.macro", IntervalValue = 30 });
        Assert.False(editor.IsDirty);
        Assert.Equal(30, editor.IntervalValue);
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
