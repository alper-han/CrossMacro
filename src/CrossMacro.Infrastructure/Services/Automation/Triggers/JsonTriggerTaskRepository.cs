namespace CrossMacro.Infrastructure.Services.Automation.Triggers;

/// <summary>Owns the trigger file path and atomic JSON persistence.</summary>
public sealed class JsonTriggerTaskRepository(string filePath) : ITriggerTaskRepository
{
    private string _filePath = ValidateFilePath(filePath);

    public async Task<IReadOnlyList<TriggerTask>?> LoadAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!File.Exists(_filePath))
        {
            return null;
        }
        var tasks = await FileBackedJsonStorage.ReadAsync(_filePath, CrossMacroJsonContext.Default.ListTriggerTask).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return tasks;
    }

    public Task SaveAsync(IReadOnlyList<TriggerTask> tasks, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tasks);
        cancellationToken.ThrowIfCancellationRequested();
        return FileBackedJsonStorage.WriteAsync(_filePath, tasks.ToList(), CrossMacroJsonContext.Default.ListTriggerTask, cancellationToken);
    }

    public void SetProfileDirectory(string profileConfigDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileConfigDirectory);
        _filePath = Path.Combine(profileConfigDirectory, ConfigFileNames.Triggers);
    }

    private static string ValidateFilePath(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        return filePath;
    }

}
