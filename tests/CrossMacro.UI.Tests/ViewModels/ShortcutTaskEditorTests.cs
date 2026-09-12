namespace CrossMacro.UI.Tests.ViewModels;

public sealed class ShortcutTaskEditorTests
{
    [Fact]
    public void Load_WhenSourceIsNull_ThrowsArgumentNullException()
    {
        var editor = new ShortcutTaskEditor();

        _ = Assert.Throws<ArgumentNullException>(() => editor.Load(null!));
    }

    [Fact]
    public void LoadAndApplyToCore_PreservesAllPersistedFields()
    {
        var source = new ShortcutTask
        {
            Name = "Original",
            MacroFilePath = "macro",
            HotkeyString = "F9",
            PlaybackSpeed = 1.5,
            IsEnabled = true,
            LoopEnabled = true,
            RepeatCount = 3,
            RepeatDelayMs = 100,
            UseRandomRepeatDelay = true,
            RepeatDelayMinMs = 10,
            RepeatDelayMaxMs = 200,
            LastStatus = "Success",
            LastTriggeredTime = new DateTime(2030, 1, 1, 1, 0, 0, DateTimeKind.Utc),
        };
        var editor = new ShortcutTaskEditor();
        editor.Load(source);

        var saved = editor.ToCore();

        _ = saved.Should().BeEquivalentTo(source);
    }

    [Fact]
    public void WindowRules_RoundTripAndInvalidRuleDisablesTheShortcut()
    {
        var source = new ShortcutTask
        {
            MacroFilePath = "macro",
            HotkeyString = "F9",
            IsEnabled = true,
        };
        source.WindowRules.Add(new ShortcutWindowRule
        {
            Field = TriggerField.WindowClass,
            MatchMode = TriggerMatchMode.Contains,
            Value = "firefox",
        });
        var editor = new ShortcutTaskEditor();
        editor.Load(source);

        var saved = editor.ToCore();
        _ = saved.WindowRules.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(source.WindowRules.Single());

        editor.AddWindowRule();

        _ = editor.IsEnabled.Should().BeFalse();
        _ = editor.CanBeEnabled.Should().BeFalse();
    }

    [Fact]
    public void Rollback_RestoresBufferedShortcutChanges()
    {
        var source = new ShortcutTask
        {
            Name = "Original",
            MacroFilePath = "macro",
            HotkeyString = "F9",
            PlaybackSpeed = 1.5,
            LastStatus = "Success",
        };
        var editor = new ShortcutTaskEditor();
        editor.Load(source);

        editor.Name = "Changed";
        editor.PlaybackSpeed = 2.0;

        editor.Rollback();

        _ = editor.Name.Should().Be("Original");
        _ = editor.PlaybackSpeed.Should().Be(1.5);
        _ = source.Name.Should().Be("Original");
        _ = source.PlaybackSpeed.Should().Be(1.5);
    }

    [Fact]
    public void RuntimeStatusSync_UsesEditorStateWithoutChangingConfiguration()
    {
        var source = new ShortcutTask { Name = "Macro", MacroFilePath = "file", HotkeyString = "F9" };
        var editor = new ShortcutTaskEditor();
        editor.Load(source);

        var timestamp = new DateTime(2030, 1, 2, 2, 0, 0, DateTimeKind.Utc);
        editor.SyncRuntimeStatus(timestamp, "Running");

        _ = editor.Name.Should().Be("Macro");
        _ = editor.LastTriggeredTime.Should().Be(timestamp);
        _ = editor.LastStatus.Should().Be("Running");
    }

    [Fact]
    public void ApplyToCore_WhenTargetIsNull_ThrowsArgumentNullException()
    {
        var editor = new ShortcutTaskEditor();

        _ = Assert.Throws<ArgumentNullException>(() => editor.ApplyToCore(null!));
    }
}
