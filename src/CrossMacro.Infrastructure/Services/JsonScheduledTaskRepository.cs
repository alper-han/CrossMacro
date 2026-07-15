
namespace CrossMacro.Infrastructure.Services;

public class JsonScheduledTaskRepository : IScheduledTaskRepository
{
    private string _scheduleFilePath;

    public JsonScheduledTaskRepository() : this(PathHelper.GetConfigFilePath(ConfigFileNames.Schedules))
    {
    }

    public JsonScheduledTaskRepository(string scheduleFilePath)
    {
        _scheduleFilePath = scheduleFilePath;
    }

    public async Task<IReadOnlyList<ScheduledTask>> LoadAsync()
    {
        try
        {
            if (!File.Exists(_scheduleFilePath))
            {
                return Array.Empty<ScheduledTask>();
            }

            var tasks = await FileBackedJsonStorage.ReadAsync(_scheduleFilePath, CrossMacroJsonContext.Default.ListScheduledTask)
                .ConfigureAwait(false);

            return (IReadOnlyList<ScheduledTask>?)tasks ?? Array.Empty<ScheduledTask>();
        }
        catch (Exception ex)
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
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to save scheduled tasks to {Path}", _scheduleFilePath);
            throw;
        }
    }
}
