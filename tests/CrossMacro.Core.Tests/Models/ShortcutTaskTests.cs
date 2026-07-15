namespace CrossMacro.Core.Tests.Models;

public class ShortcutTaskTests
{
    [Fact]
    public void ShortcutTask_IsPlainDomainModel()
    {
        typeof(ShortcutTask).GetInterface(nameof(INotifyPropertyChanged)).Should().BeNull();
    }

    [Fact]
    public void TrySetEnabled_RejectsIncompleteTask()
    {
        var task = new ShortcutTask();

        task.TrySetEnabled(true).Should().BeFalse();
        task.IsEnabled.Should().BeFalse();
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

        task.LoopEnabled.Should().BeTrue();
        task.RunWhileHeld.Should().BeFalse();
        task.RepeatDelayMinMs.Should().Be(500);
        task.RepeatDelayMaxMs.Should().Be(500);
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

        var roundTrip = System.Text.Json.JsonSerializer.Deserialize<ShortcutTask>(
            System.Text.Json.JsonSerializer.Serialize(task));

        roundTrip.Should().NotBeNull();
        roundTrip!.Name.Should().Be(task.Name);
        roundTrip.HotkeyString.Should().Be(task.HotkeyString);
        roundTrip.RepeatDelayMaxMs.Should().Be(task.RepeatDelayMaxMs);
        roundTrip.LastStatus.Should().Be(task.LastStatus);
    }
}
