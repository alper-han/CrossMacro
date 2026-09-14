namespace CrossMacro.Infrastructure.Services.Automation.Shortcuts;

/// <summary>Owns the shortcut file path and atomic JSON persistence.</summary>
public sealed class JsonShortcutTaskRepository(string filePath) : IShortcutTaskRepository
{
    private string _filePath = ValidateFilePath(filePath);

    public async Task<IReadOnlyList<ShortcutTask>?> LoadAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!File.Exists(_filePath))
        {
            return null;
        }
        var tasks = await FileBackedJsonStorage.ReadAsync(_filePath, CrossMacroJsonContext.Default.ListShortcutTask).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return tasks;
    }

    public Task SaveAsync(IReadOnlyList<ShortcutTask> tasks, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tasks);
        cancellationToken.ThrowIfCancellationRequested();
        return FileBackedJsonStorage.WriteAsync(_filePath, tasks.ToList(), CrossMacroJsonContext.Default.ListShortcutTask, cancellationToken);
    }

    public void SetProfileDirectory(string profileConfigDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileConfigDirectory);
        _filePath = Path.Combine(profileConfigDirectory, ConfigFileNames.Shortcuts);
    }

    private static string ValidateFilePath(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        return filePath;
    }

}
