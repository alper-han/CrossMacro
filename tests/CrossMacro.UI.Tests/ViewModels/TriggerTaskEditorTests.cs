namespace CrossMacro.UI.Tests.ViewModels;

public sealed class TriggerTaskEditorTests
{
    [Fact]
    public void Load_WhenSourceIsNull_ThrowsArgumentNullException()
    {
        var editor = new TriggerTaskEditor();

        _ = Assert.Throws<ArgumentNullException>(() => editor.Load(null!));
    }

    [Fact]
    public void LoadAndToCore_PreservesPersistedAndRuntimeFields()
    {
        var source = new TriggerTask
        {
            Name = "Window trigger",
            Field = TriggerField.WindowTitle,
            MatchMode = TriggerMatchMode.Regex,
            Value = "Editor",
            Action = TriggerOperation.RunMacro,
            MacroFilePath = "macro.json",
            FireMode = TriggerFireMode.EveryMatch,
            CooldownMs = 500,
            DebounceMs = 100,
            IsEnabled = true,
            LastTriggeredTime = new DateTime(2030, 1, 1, 1, 0, 0, DateTimeKind.Utc),
            LastStatus = "Running",
        };
        var editor = new TriggerTaskEditor();
        editor.Load(source);

        var target = editor.ToCore();

        _ = target.Should().BeEquivalentTo(source);
    }

    [Fact]
    public void IsDirty_TracksUserEditsButNotLoadOrRuntimeStatus()
    {
        var source = new TriggerTask { Name = "Original", Value = "firefox", TargetProfileId = "dev" };
        var editor = new TriggerTaskEditor();
        editor.Load(source);
        Assert.False(editor.IsDirty);

        editor.SyncRuntimeStatus(DateTime.UtcNow, "Switched");
        Assert.False(editor.IsDirty);

        editor.Name = "Draft";
        Assert.True(editor.IsDirty);
        editor.Load(source);

        Assert.False(editor.IsDirty);
        Assert.Equal("Original", editor.Name);
    }

    [Fact]
    public void DependentConfigurationChangesNotifyCanBeEnabled()
    {
        var editor = new TriggerTaskEditor();
        var changed = new List<string>();
        editor.PropertyChanged += (_, args) => changed.Add(args.PropertyName!);

        editor.Value = "firefox";

        _ = editor.CanBeEnabled.Should().BeFalse();
        _ = changed.Should().Contain(nameof(TriggerTaskEditor.CanBeEnabled));
        editor.TargetProfileId = "dev";
        _ = editor.CanBeEnabled.Should().BeTrue();
    }

    [Fact]
    public void RuntimeStatusSync_DoesNotChangeConfiguration()
    {
        var editor = new TriggerTaskEditor();
        editor.Load(new TriggerTask { Value = "firefox", TargetProfileId = "dev" });

        var timestamp = new DateTime(2030, 1, 2, 2, 0, 0, DateTimeKind.Utc);
        editor.SyncRuntimeStatus(timestamp, "Switched");

        _ = editor.LastTriggeredTime.Should().Be(timestamp);
        _ = editor.LastStatus.Should().Be("Switched");
        _ = editor.ToCore().Value.Should().Be("firefox");
    }
}
