namespace CrossMacro.Infrastructure.Tests.Serialization;

public sealed class ShortcutTaskSerializationCompatibilityTests
{
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
        task.WindowRules.Add(new ShortcutWindowRule
        {
            Field = TriggerField.WindowTitle,
            MatchMode = TriggerMatchMode.Contains,
            Value = "Firefox",
        });

        var json = JsonSerializer.Serialize(task, CrossMacroJsonContext.Default.ShortcutTask);
        var roundTrip = JsonSerializer.Deserialize(json, CrossMacroJsonContext.Default.ShortcutTask);

        _ = roundTrip.Should().NotBeNull();
        _ = roundTrip!.Name.Should().Be(task.Name);
        _ = roundTrip.HotkeyString.Should().Be(task.HotkeyString);
        _ = roundTrip.RepeatDelayMaxMs.Should().Be(task.RepeatDelayMaxMs);
        _ = roundTrip.LastStatus.Should().Be(task.LastStatus);
        _ = roundTrip.WindowRules.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(task.WindowRules.Single());
    }

    [Fact]
    public void Deserialize_WhenWindowRulesIsMissing_UsesAnEmptyCollection()
    {
        const string json = "{}";

        var task = JsonSerializer.Deserialize(json, CrossMacroJsonContext.Default.ShortcutTask);

        _ = task.Should().NotBeNull();
        _ = task!.WindowRules.Should().BeEmpty();
    }
}
