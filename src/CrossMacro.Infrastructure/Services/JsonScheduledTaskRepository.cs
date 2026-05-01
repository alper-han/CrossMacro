using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CrossMacro.Core.Logging;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services;
using CrossMacro.Infrastructure.Helpers;
using CrossMacro.Infrastructure.Serialization;

namespace CrossMacro.Infrastructure.Services;

public class JsonScheduledTaskRepository : IScheduledTaskRepository
{
    private readonly string _scheduleFilePath;

    public JsonScheduledTaskRepository() : this(PathHelper.GetConfigFilePath(ConfigFileNames.Schedules))
    {
    }

    public JsonScheduledTaskRepository(string scheduleFilePath)
    {
        _scheduleFilePath = scheduleFilePath;
    }

    public async Task<List<ScheduledTask>> LoadAsync()
    {
        try
        {
            if (!File.Exists(_scheduleFilePath)) 
                return new List<ScheduledTask>();
            
            var tasks = await FileBackedJsonStorage.ReadAsync(_scheduleFilePath, CrossMacroJsonContext.Default.ListScheduledTask)
                .ConfigureAwait(false);
            
            return tasks ?? new List<ScheduledTask>();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to load scheduled tasks from {Path}", _scheduleFilePath);
            return new List<ScheduledTask>();
        }
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
