namespace CrossMacro.UI.Tests.ViewModels;

public sealed class TriggerTaskEditorTests
{
    [Fact]
    public void LoadAndRollback_PreserveBufferedEditBoundary()
    {
        var source = new TriggerTask { Name = "Original", Value = "firefox", TargetProfileId = "dev" };
        var editor = new TriggerTaskEditor();
        editor.Load(source);
        editor.Name = "Draft";

        editor.Rollback();

        _ = editor.Name.Should().Be("Original");
        _ = source.Name.Should().Be("Original");
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

        var timestamp = DateTime.UtcNow;
        editor.SyncRuntimeStatus(timestamp, "Switched");

        _ = editor.LastTriggeredTime.Should().Be(timestamp);
        _ = editor.LastStatus.Should().Be("Switched");
        _ = editor.ToCore().Value.Should().Be("firefox");
    }
}
