namespace CrossMacro.Application.Automation;

/// <summary>Stages shortcut-task changes and commits them before the runtime observes them.</summary>
public sealed class ManageShortcut(IShortcutTaskOperations operations, IShortcutTaskStore store, AutomationTaskMutationGate? mutationGate = null)
    : ManageAutomationTasks<ShortcutTask>(mutationGate), IManageShortcut
{
    private readonly IShortcutTaskOperations _operations = operations ?? throw new ArgumentNullException(nameof(operations));
    private readonly IShortcutTaskStore _store = store ?? throw new ArgumentNullException(nameof(store));

    protected override string Kind => "shortcut";

    protected override IReadOnlyList<ShortcutTask> StoredTasks => _store.Tasks;

    protected override Task LoadStoreAsync() => _store.LoadAsync();

    protected override Task CommitStoreAsync(IReadOnlyList<ShortcutTask> tasks, CancellationToken cancellationToken) =>
        _store.CommitAsync(tasks, cancellationToken);

    protected override Guid IdOf(ShortcutTask task) => task.Id;

    protected override ShortcutTask Copy(ShortcutTask task) => AutomationTaskSnapshots.Copy(task);

    protected override void Normalize(ShortcutTask task) => task.Normalize();

    protected override bool IsEnabled(ShortcutTask task) => task.IsEnabled;

    protected override void SetEnabled(ShortcutTask task, bool enabled) => _ = task.TrySetEnabled(enabled);

    protected override void AuthorizeMacros(ShortcutTask task)
    {
        MutationGate.AuthorizeMacroPath(task.MacroFilePath);
        if (task.RunOnPressAndRelease)
        {
            MutationGate.AuthorizeMacroPath(task.ReleaseMacroFilePath);
        }
    }

    protected override void ValidateFiles(ShortcutTask task)
    {
        if (task.RunOnPressAndRelease && (!File.Exists(task.MacroFilePath) || !File.Exists(task.ReleaseMacroFilePath)))
        {
            throw new FileNotFoundException("Press/release mode requires available press and release macro files.");
        }
    }

    public Task RunAsync(TaskRequest request, CancellationToken cancellationToken = default) =>
        RunBoundAsync(request, _operations.RunTaskAsync, cancellationToken);
}
