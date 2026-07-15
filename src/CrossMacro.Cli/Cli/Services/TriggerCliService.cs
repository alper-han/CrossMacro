using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Application.Automation;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services;
using CrossMacro.Cli.Serialization;

namespace CrossMacro.Cli.Services;

public sealed class TriggerCliService : ITriggerCliService
{
    private readonly IManageTrigger _manageTrigger;
    private IReadOnlyList<TriggerTask> _tasks = [];

    public TriggerCliService(IManageTrigger manageTrigger)
    {
        _manageTrigger = manageTrigger;
    }

    public async Task<CliCommandExecutionResult> ListAsync(CancellationToken cancellationToken)
    {
        return await TaskCliServiceHelpers.ListTasksAsync(
            taskKind: "trigger",
            cancellationToken: cancellationToken,
            loadAsync: async () => _tasks = (await _manageTrigger.ListAsync(cancellationToken).ConfigureAwait(false)).Tasks,
            getTasks: () => _tasks,
            mapTask: MapTask);
    }

    public Task<CliCommandExecutionResult> ExecuteAsync(TriggerCliOptions options, CancellationToken cancellationToken)
    {
        return options.Action switch
        {
            TriggerCliAction.Add => AddAsync(options, cancellationToken),
            TriggerCliAction.Edit => EditAsync(options, cancellationToken),
            TriggerCliAction.Remove => RemoveAsync(options.TaskId ?? string.Empty, cancellationToken),
            TriggerCliAction.Enable => SetEnabledAsync(options.TaskId ?? string.Empty, enabled: true, cancellationToken),
            TriggerCliAction.Disable => SetEnabledAsync(options.TaskId ?? string.Empty, enabled: false, cancellationToken),
            _ => Task.FromResult(CliCommandExecutionResult.Fail(CliExitCode.InvalidArguments, "Unknown trigger action.")),
        };
    }

    private async Task<CliCommandExecutionResult> AddAsync(TriggerCliOptions options, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var task = new TriggerTask
        {
            Name = options.Name ?? string.Empty,
            Field = options.Field ?? TriggerField.None,
            MatchMode = options.MatchMode ?? TriggerMatchMode.Equals,
            Value = options.Value ?? string.Empty,
            Action = options.TriggerActionVal ?? TriggerAction.SwitchProfile,
            TargetProfileId = options.TargetProfileId ?? string.Empty,
            MacroFilePath = options.MacroFilePath ?? string.Empty,
            FireMode = options.FireMode ?? TriggerFireMode.OnceOnChange,
            CooldownMs = options.CooldownMs,
            DebounceMs = options.DebounceMs,
        };

        if (options.Enabled.HasValue)
        {
            task.IsEnabled = options.Enabled.Value;
        }

        await _manageTrigger.AddAsync(task, cancellationToken).ConfigureAwait(false);
        return CliCommandExecutionResult.Ok($"Trigger task added: {task.Name}.", MapTask(task));
    }

    private async Task<CliCommandExecutionResult> EditAsync(TriggerCliOptions options, CancellationToken cancellationToken)
    {
        var parsed = await LoadAndFindAsync(options.TaskId ?? string.Empty, cancellationToken).ConfigureAwait(false);
        if (parsed.Result is not null) return parsed.Result;

        var task = parsed.Task!;
        if (!string.IsNullOrWhiteSpace(options.Name)) task.Name = options.Name;
        if (options.Field.HasValue) task.Field = options.Field.Value;
        if (options.MatchMode.HasValue) task.MatchMode = options.MatchMode.Value;
        if (options.Value is not null) task.Value = options.Value;
        if (options.TriggerActionVal.HasValue) task.Action = options.TriggerActionVal.Value;
        if (options.TargetProfileId is not null) task.TargetProfileId = options.TargetProfileId;
        if (options.MacroFilePath is not null) task.MacroFilePath = options.MacroFilePath;
        if (options.FireMode.HasValue) task.FireMode = options.FireMode.Value;
        if (options.CooldownMs.HasValue) task.CooldownMs = options.CooldownMs.Value is 0 ? null : options.CooldownMs.Value;
        if (options.DebounceMs.HasValue) task.DebounceMs = options.DebounceMs.Value is 0 ? null : options.DebounceMs.Value;
        if (options.Enabled.HasValue) task.IsEnabled = options.Enabled.Value;

        cancellationToken.ThrowIfCancellationRequested();
        await _manageTrigger.UpdateAsync(task, cancellationToken).ConfigureAwait(false);
        return CliCommandExecutionResult.Ok($"Trigger task updated: {task.Name}.", MapTask(task));
    }

    private async Task<CliCommandExecutionResult> RemoveAsync(string taskId, CancellationToken cancellationToken)
    {
        var parsed = await LoadAndFindAsync(taskId, cancellationToken).ConfigureAwait(false);
        if (parsed.Result is not null) return parsed.Result;

        var task = parsed.Task!;
        cancellationToken.ThrowIfCancellationRequested();
        await _manageTrigger.RemoveAsync(new TaskRequest(task.Id), cancellationToken).ConfigureAwait(false);
        return CliCommandExecutionResult.Ok($"Trigger task removed: {task.Name}.", MapTask(task));
    }

    private async Task<CliCommandExecutionResult> SetEnabledAsync(string taskId, bool enabled, CancellationToken cancellationToken)
    {
        var parsed = await LoadAndFindAsync(taskId, cancellationToken).ConfigureAwait(false);
        if (parsed.Result is not null) return parsed.Result;

        var task = parsed.Task!;
        if (enabled && !task.CanBeEnabled)
        {
            return CliCommandExecutionResult.Fail(
                CliExitCode.InvalidArguments,
                "Trigger task cannot be enabled.",
                ["Trigger task requires valid action configurations (Target Profile ID or Macro Path) before enabling."]);
        }

        cancellationToken.ThrowIfCancellationRequested();
        await _manageTrigger.SetEnabledAsync(new TaskRequest(task.Id, enabled), cancellationToken).ConfigureAwait(false);
        task.IsEnabled = enabled;
        var verb = enabled ? "enabled" : "disabled";
        return CliCommandExecutionResult.Ok($"Trigger task {verb}: {task.Name}.", MapTask(task));
    }

    private async Task<(TriggerTask? Task, CliCommandExecutionResult? Result)> LoadAndFindAsync(string taskId, CancellationToken cancellationToken)
    {
        return await TaskCliServiceHelpers.FindTaskAsync(
            taskId,
            "trigger",
            "Trigger",
            cancellationToken,
            async () => _tasks = (await _manageTrigger.ListAsync(cancellationToken).ConfigureAwait(false)).Tasks,
            task => task.Id);
    }

    private static TriggerTaskData MapTask(TriggerTask task)
    {
        return new TriggerTaskData(
            task.Id,
            task.Name,
            task.IsEnabled,
            task.Field.ToString(),
            task.MatchMode.ToString(),
            task.Value,
            task.Action.ToString(),
            task.Action is TriggerAction.SwitchProfile ? task.TargetProfileId : null,
            task.Action is TriggerAction.RunMacro ? task.MacroFilePath : null,
            task.FireMode.ToString(),
            task.CooldownMs,
            task.DebounceMs,
            task.LastTriggeredTime,
            task.LastStatus);
    }
}
