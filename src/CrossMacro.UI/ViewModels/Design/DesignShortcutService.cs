using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Application.Automation;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services;
using CrossMacro.UI.Localization;
using CrossMacro.UI.Services;

namespace CrossMacro.UI.ViewModels;

internal sealed class DesignShortcutService : IShortcutService
{
    public DesignShortcutService()
    {
        Tasks = new ObservableCollection<ShortcutTask>(DesignPreviewSamples.CreateShortcutTasks());
    }

    public ObservableCollection<ShortcutTask> Tasks { get; }

    public bool IsListening { get; private set; }

    public event EventHandler<ShortcutExecutedEventArgs>? ShortcutExecuted;

    public event EventHandler<ShortcutTask>? ShortcutStarting;

    public void AddTask(ShortcutTask task) => Tasks.Add(task);

    public void RemoveTask(Guid id)
    {
        var task = Tasks.FirstOrDefault(item => item.Id == id);
        if (task is not null)
        {
            Tasks.Remove(task);
        }
    }

    public void UpdateTask(ShortcutTask task)
    {
    }

    public void SetTaskEnabled(Guid id, bool enabled)
    {
        var task = Tasks.FirstOrDefault(item => item.Id == id);
        if (task is not null)
        {
            task.IsEnabled = enabled;
        }
    }

    public void Start() => IsListening = true;

    public void Stop() => IsListening = false;

    public Task SaveAsync() => Task.CompletedTask;

    public Task RunTaskAsync(Guid taskId, CancellationToken cancellationToken = default)
    {
        var task = Tasks.FirstOrDefault(item => item.Id == taskId);
        if (task is not null)
        {
            ShortcutStarting?.Invoke(this, task);
            ShortcutExecuted?.Invoke(this, new ShortcutExecutedEventArgs(task, success: true, message: "Preview macro run completed"));
        }

        return Task.CompletedTask;
    }

    public Task LoadAsync() => Task.CompletedTask;

    public void Dispose()
    {
    }
}
