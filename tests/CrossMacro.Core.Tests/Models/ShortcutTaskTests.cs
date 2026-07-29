namespace CrossMacro.Core.Tests.Models;

public sealed class ShortcutTaskTests
{
    [Fact]
    public void ShortcutTask_IsPlainDomainModel()
    {
        _ = typeof(ShortcutTask).GetInterface(nameof(INotifyPropertyChanged)).Should().BeNull();
    }

    [Fact]
    public void TrySetEnabled_RejectsIncompleteTask()
    {
        var task = new ShortcutTask();

        _ = task.TrySetEnabled(enabled: true).Should().BeFalse();
        _ = task.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void Normalize_EnforcesLoopExclusivityAndDelayRange()
    {
        var task = new ShortcutTask
        {
            LoopEnabled = true,
            RunWhileHeld = true,
            RepeatDelayMinMs = 500,
            RepeatDelayMaxMs = 10,
        };

        task.Normalize();

        _ = task.LoopEnabled.Should().BeTrue();
        _ = task.RunWhileHeld.Should().BeFalse();
        _ = task.RepeatDelayMinMs.Should().Be(500);
        _ = task.RepeatDelayMaxMs.Should().Be(500);
    }

    [Fact]
    public void JsonRoundTrip_PreservesPersistedFields()
    {
        var task = new ShortcutTask
        {
            Name = "Shortcut",
            MacroFilePath = "macro.macro",
            HotkeyString = "Ctrl+F9",
            PlaybackSpeed = 1.25,
            LoopEnabled = true,
            RepeatCount = 4,
            RepeatDelayMs = 30,
            UseRandomRepeatDelay = true,
            RepeatDelayMinMs = 10,
            RepeatDelayMaxMs = 50,
            LastStatus = "Success",
            LastTriggeredTime = DateTime.UtcNow,
        };

        var json = System.Text.Json.JsonSerializer.Serialize(task, CrossMacro.Infrastructure.Serialization.CrossMacroJsonContext.Default.ShortcutTask);
        var roundTrip = System.Text.Json.JsonSerializer.Deserialize(json, CrossMacro.Infrastructure.Serialization.CrossMacroJsonContext.Default.ShortcutTask);

        _ = roundTrip.Should().NotBeNull();
        _ = roundTrip!.Name.Should().Be(task.Name);
        _ = roundTrip.HotkeyString.Should().Be(task.HotkeyString);
        _ = roundTrip.RepeatDelayMaxMs.Should().Be(task.RepeatDelayMaxMs);
        _ = roundTrip.LastStatus.Should().Be(task.LastStatus);
    }
}
