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
            Hotkeys = { "F9" },
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
            Hotkeys = { "F9" },
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
    public void IsDirty_TracksUserEditsIncludingHotkeysAndWindowRulesButNotLoadOrRuntimeStatus()
    {
        var source = new ShortcutTask
        {
            Name = "Original",
            MacroFilePath = "macro",
            Hotkeys = { "F9" },
            WindowRules = { new ShortcutWindowRule { Value = "firefox" } },
        };
        var editor = new ShortcutTaskEditor();
        editor.Load(source);
        Assert.False(editor.IsDirty);

        editor.SyncRuntimeStatus(DateTime.UtcNow, "Running");
        Assert.False(editor.IsDirty);

        _ = editor.AddHotkey("F10");
        Assert.True(editor.IsDirty);
        editor.Load(source);
        Assert.False(editor.IsDirty);

        editor.WindowRules[0].Value = "chrome";
        Assert.True(editor.IsDirty);
        editor.Load(source);
        Assert.False(editor.IsDirty);

        editor.PlaybackSpeed = 2.0;
        Assert.True(editor.IsDirty);
        editor.Load(source);

        Assert.False(editor.IsDirty);
        Assert.Equal(1.0, editor.PlaybackSpeed);
    }

    [Fact]
    public void RuntimeStatusSync_UsesEditorStateWithoutChangingConfiguration()
    {
        var source = new ShortcutTask { Name = "Macro", MacroFilePath = "file", Hotkeys = { "F9" } };
        var editor = new ShortcutTaskEditor();
        editor.Load(source);

        var timestamp = new DateTime(2030, 1, 2, 2, 0, 0, DateTimeKind.Utc);
        editor.SyncRuntimeStatus(timestamp, "Running");

        _ = editor.Name.Should().Be("Macro");
        _ = editor.LastTriggeredTime.Should().Be(timestamp);
        _ = editor.LastStatus.Should().Be("Running");
    }

    [Fact]
    public void Load_DeduplicatesAliasesAndRemovingTheLastAliasLeavesEditorInvalid()
    {
        var source = new ShortcutTask
        {
            MacroFilePath = "macro",
            Hotkeys = { "F9", "Ctrl+F9", "f9" },
        };
        var editor = new ShortcutTaskEditor();

        editor.Load(source);

        _ = editor.Hotkeys.Should().Equal("F9", "Ctrl+F9");
        _ = editor.RemoveHotkey("ctrl+f9").Should().BeTrue();
        _ = editor.RemoveHotkey("F9").Should().BeTrue();
        _ = editor.Hotkeys.Should().BeEmpty();
        _ = editor.CanBeEnabled.Should().BeFalse();
    }

    [Fact]
    public void PressRelease_MissingReleasePath_CannotBeEnabled()
    {
        var editor = new ShortcutTaskEditor();
        editor.Load(new ShortcutTask { MacroFilePath = "press.macro", RunOnPressAndRelease = true, Hotkeys = { "F9" } });
        Assert.False(editor.CanBeEnabled);
        editor.IsEnabled = true;
        Assert.False(editor.IsEnabled);
    }
}
