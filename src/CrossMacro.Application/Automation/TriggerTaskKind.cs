namespace CrossMacro.Application.Automation;

public sealed class TriggerTaskKind : AutomationTaskKind<TriggerTask>
{
    public static TriggerTaskKind Instance { get; } = new();

    private TriggerTaskKind()
    {
    }

    public override string Name => "trigger";

    public override string CannotEnableReason => "Trigger task requires valid action configurations (Target Profile ID or Macro Path) before enabling.";

    public override Guid IdOf(TriggerTask task) => task.Id;

    public override string NameOf(TriggerTask task) => task.Name;

    public override TriggerTask Copy(TriggerTask task) => AutomationTaskSnapshots.Copy(task);

    public override void Normalize(TriggerTask task)
    {
        if (task.IsEnabled && !task.CanBeEnabled)
        {
            task.IsEnabled = false;
        }
    }

    public override bool IsEnabled(TriggerTask task) => task.IsEnabled;

    public override bool CanBeEnabled(TriggerTask task) => task.CanBeEnabled;

    public override bool TrySetEnabled(TriggerTask task, bool enabled) => task.TrySetEnabled(enabled);

    public override IEnumerable<string?> MacroPaths(TriggerTask task)
    {
        yield return task.Action is TriggerOperation.RunMacro ? task.MacroFilePath : null;
    }
}
