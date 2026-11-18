namespace CrossMacro.Application.Automation;

/// <summary>Stages scheduled-task changes and commits them before the runtime observes them.</summary>
public sealed class ManageSchedule(IScheduledTaskOperations operations, IScheduledTaskStore store, AutomationTaskMutationGate? mutationGate = null)
    : ManageAutomationTasks<ScheduledTask>(mutationGate), IManageSchedule
{
    private readonly IScheduledTaskOperations _operations = operations ?? throw new ArgumentNullException(nameof(operations));
    private readonly IScheduledTaskStore _store = store ?? throw new ArgumentNullException(nameof(store));

    protected override string Kind => "schedule";

    protected override IReadOnlyList<ScheduledTask> StoredTasks => _store.Tasks;

    protected override Task LoadStoreAsync() => _store.LoadAsync();

    protected override Task CommitStoreAsync(IReadOnlyList<ScheduledTask> tasks, CancellationToken cancellationToken) =>
        _store.CommitAsync(tasks, cancellationToken);

    protected override Guid IdOf(ScheduledTask task) => task.Id;

    protected override ScheduledTask Copy(ScheduledTask task) => AutomationTaskSnapshots.Copy(task);

    protected override void Normalize(ScheduledTask task) => task.Normalize();

    protected override bool IsEnabled(ScheduledTask task) => task.IsEnabled;

    protected override void SetEnabled(ScheduledTask task, bool enabled) => _ = task.TrySetEnabled(enabled);

    protected override void AuthorizeMacros(ScheduledTask task) => MutationGate.AuthorizeMacroPath(task.MacroFilePath);

    public Task RunAsync(TaskRequest request, CancellationToken cancellationToken = default) =>
        RunBoundAsync(request, _operations.RunTaskAsync, cancellationToken);
}
