
namespace CrossMacro.Infrastructure.Services;

public class JsonScheduledTaskRepository(string scheduleFilePath) : IScheduledTaskRepository
{
    private string _scheduleFilePath = scheduleFilePath;

    public JsonScheduledTaskRepository() : this(PathHelper.GetConfigFilePath(ConfigFileNames.Schedules))
    {
    }

    public async Task<IReadOnlyList<ScheduledTask>> LoadAsync()
    {
        try
        {
            if (!File.Exists(_scheduleFilePath))
            {
                return [];
            }

            var tasks = await FileBackedJsonStorage.ReadAsync(_scheduleFilePath, CrossMacroJsonContext.Default.ListScheduledTask)
                .ConfigureAwait(false);

            return (IReadOnlyList<ScheduledTask>?)tasks ?? [];
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.Warning(ex, "Failed to load scheduled tasks from {Path}", _scheduleFilePath);
            return new List<ScheduledTask>();
        }
    }

    public Task ReloadAsync(string profileConfigDirectory)
    {
        _scheduleFilePath = Path.Combine(profileConfigDirectory, ConfigFileNames.Schedules);
        return LoadAsync();
    }

    public async Task SaveAsync(IEnumerable<ScheduledTask> tasks)
    {
        try
        {
            await FileBackedJsonStorage.WriteAsync(
                    _scheduleFilePath,
                    tasks.ToList(),
                    CrossMacroJsonContext.Default.ListScheduledTask)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.Warning(ex, "Failed to save scheduled tasks to {Path}", _scheduleFilePath);
            throw;
        }
    }
}
