namespace CrossMacro.Infrastructure.Services.Automation.Triggers;

/// <summary>Owns the trigger file path and atomic JSON persistence.</summary>
public sealed class JsonTriggerTaskRepository(string filePath) : ITriggerTaskRepository
{
    private readonly JsonTaskFile<TriggerTask> _file = new(
        filePath,
        ConfigFileNames.Triggers,
        CrossMacroJsonContext.Default.ListTriggerTask);

    public async Task<IReadOnlyList<TriggerTask>?> LoadAsync(CancellationToken cancellationToken = default) =>
        await _file.LoadAsync(cancellationToken).ConfigureAwait(false);

    public Task SaveAsync(IReadOnlyList<TriggerTask> tasks, CancellationToken cancellationToken = default) =>
        _file.SaveAsync(tasks, cancellationToken);

    public void SetProfileDirectory(string profileConfigDirectory) => _file.SetProfileDirectory(profileConfigDirectory);
}
