
namespace CrossMacro.Infrastructure.Services.Automation.Scheduling;

public class JsonScheduledTaskRepository(string scheduleFilePath) : IScheduledTaskRepository
{
    private string _scheduleFilePath = ValidateScheduleFilePath(scheduleFilePath);

    public JsonScheduledTaskRepository() : this(ApplicationPathsEnvironment.CaptureCurrent().GetConfigFilePath(ConfigFileNames.Schedules))
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
        ArgumentException.ThrowIfNullOrWhiteSpace(profileConfigDirectory);
        _scheduleFilePath = Path.Combine(profileConfigDirectory, ConfigFileNames.Schedules);
        return LoadAsync();
    }

    public Task SaveAsync(IEnumerable<ScheduledTask> tasks) => SaveAsync(tasks, CancellationToken.None);

    public async Task SaveAsync(IEnumerable<ScheduledTask> tasks, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tasks);
        try
        {
            await FileBackedJsonStorage.WriteAsync(
                    _scheduleFilePath,
                    tasks.ToList(),
                    CrossMacroJsonContext.Default.ListScheduledTask,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.Warning(ex, "Failed to save scheduled tasks to {Path}", _scheduleFilePath);
            throw;
        }
    }

    private static string ValidateScheduleFilePath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return path;
    }
}
