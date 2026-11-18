namespace CrossMacro.Application.Automation;

/// <summary>Stages trigger-task changes and commits them before the runtime observes them.</summary>
public sealed class ManageTrigger(ITriggerTaskStore store, AutomationTaskMutationGate? mutationGate = null)
    : ManageAutomationTasks<TriggerTask>(mutationGate), IManageTrigger
{
    private readonly ITriggerTaskStore _store = store ?? throw new ArgumentNullException(nameof(store));

    protected override string Kind => "trigger";

    protected override IReadOnlyList<TriggerTask> StoredTasks => _store.Tasks;

    protected override Task LoadStoreAsync() => _store.LoadAsync();

    protected override Task CommitStoreAsync(IReadOnlyList<TriggerTask> tasks, CancellationToken cancellationToken) =>
        _store.CommitAsync(tasks, cancellationToken);

    protected override Guid IdOf(TriggerTask task) => task.Id;

    protected override TriggerTask Copy(TriggerTask task) => AutomationTaskSnapshots.Copy(task);

    protected override void Normalize(TriggerTask task)
    {
        if (task.IsEnabled && !task.CanBeEnabled)
        {
            task.IsEnabled = false;
        }
    }

    protected override bool IsEnabled(TriggerTask task) => task.IsEnabled;

    protected override void SetEnabled(TriggerTask task, bool enabled) => _ = task.TrySetEnabled(enabled);

    protected override void AuthorizeMacros(TriggerTask task) =>
        MutationGate.AuthorizeMacroPath(task.Action is TriggerOperation.RunMacro ? task.MacroFilePath : null);
}
