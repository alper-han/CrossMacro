namespace CrossMacro.Infrastructure.Services.Automation.Scheduling;

public sealed class JsonScheduledTaskRepository(string scheduleFilePath) : IScheduledTaskRepository
{
    private readonly JsonTaskFile<ScheduledTask> _file = new(
        scheduleFilePath,
        ConfigFileNames.Schedules,
        CrossMacroJsonContext.Default.ListScheduledTask);

    public async Task<IReadOnlyList<ScheduledTask>> LoadAsync() =>
        await _file.LoadAsync(CancellationToken.None).ConfigureAwait(false) ?? [];

    public Task ReloadAsync(string profileConfigDirectory)
    {
        _file.SetProfileDirectory(profileConfigDirectory);
        return LoadAsync();
    }

    public Task SaveAsync(IEnumerable<ScheduledTask> tasks) => SaveAsync(tasks, CancellationToken.None);

    public Task SaveAsync(IEnumerable<ScheduledTask> tasks, CancellationToken cancellationToken) =>
        _file.SaveAsync(tasks, cancellationToken);
}
