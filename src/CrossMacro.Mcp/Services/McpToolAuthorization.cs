namespace CrossMacro.Mcp.Services;

public sealed class McpToolAuthorization(
    IMcpCapabilityPolicy capabilityPolicy,
    McpPathAuthorizer pathAuthorizer,
    IScheduleCliService scheduleCliService,
    IShortcutCliService shortcutCliService,
    ITriggerCliService triggerCliService)
{
    private readonly IMcpCapabilityPolicy _capabilityPolicy = capabilityPolicy;
    private readonly McpPathAuthorizer _pathAuthorizer = pathAuthorizer;
    private readonly IScheduleCliService _scheduleCliService = scheduleCliService;
    private readonly IShortcutCliService _shortcutCliService = shortcutCliService;
    private readonly ITriggerCliService _triggerCliService = triggerCliService;

    public McpToolOutcome? Require(McpCapability capability) =>
        _capabilityPolicy.IsAllowed(capability) ? null : _capabilityPolicy.Require(capability);

    public bool IsAnyAllowed(params McpCapability[] capabilities) =>
        _capabilityPolicy.IsAnyAllowed(capabilities);

    public McpToolOutcome? RequireTaskManagement(bool requiresInputAutomation, bool requiresMacroRead = false)
    {
        var taskManagementCapability = Require(McpCapability.TaskManage);
        if (taskManagementCapability is not null)
        {
            return taskManagementCapability;
        }

        var inputAutomationCapability = requiresInputAutomation
            ? Require(McpCapability.InputAutomation)
            : null;
        return inputAutomationCapability ?? (requiresMacroRead
            ? Require(McpCapability.MacroRead)
            : null);
    }

    public McpToolOutcome? RequireAutomation(string? operation)
    {
        var definition = CrossMacroMcpToolCatalog.V1.First(static tool => tool.Name is "automation.start");
        var required = definition.OperationCapabilities.FirstOrDefault(
            candidate => string.Equals(candidate.Operation, operation, StringComparison.Ordinal));
        if (required is null)
        {
            return McpToolOutcomeMapper.InvalidArguments("Automation kind must be play, run, or record.");
        }

        foreach (var capability in required.Capabilities)
        {
            var failure = Require(capability);
            if (failure is not null)
            {
                return failure;
            }
        }

        return null;
    }

    public McpToolOutcome? RequireShell(IReadOnlyList<string> steps) =>
        steps.Any(RunScriptSyntax.IsShellStep)
            ? Require(McpCapability.ShellExecute)
            : null;

    public McpToolOutcome? RequireCommand(CliCommandOptions options)
    {
        IReadOnlyList<McpCapability> capabilities = options switch
        {
            ClipboardCliOptions { Action: ClipboardCliAction.Get } => [McpCapability.ClipboardRead],
            ClipboardCliOptions => [McpCapability.ClipboardWrite],
            WindowCliOptions { Action: WindowCliAction.Active or WindowCliAction.List or WindowCliAction.Search or WindowCliAction.Wait or WindowCliAction.WorkspaceGet } => [McpCapability.WindowRead],
            WindowCliOptions => [McpCapability.WindowControl],
            ScreenCliOptions => [McpCapability.ScreenRead],
            ScreenshotCliOptions => [McpCapability.ScreenRead],
            InputCliOptions => [McpCapability.InputAutomation],
            PlayCliOptions => [McpCapability.InputAutomation],
            RunCliOptions => [McpCapability.InputAutomation],
            RecordCliOptions => [McpCapability.Recording],
            MacroInfoCliOptions or MacroValidateCliOptions => [McpCapability.MacroRead],
            SettingsGetCliOptions or SettingsListKeysCliOptions => [McpCapability.SettingsRead],
            SettingsSetCliOptions or SettingsResetCliOptions => [McpCapability.SettingsWrite],
            ProfileCliOptions => [McpCapability.ProfileManage],
            TextExpansionCliOptions { Action: TextExpansionCliAction.List or TextExpansionCliAction.Test } => [McpCapability.TextExpansionRead],
            TextExpansionCliOptions => [McpCapability.TextExpansionWrite],
            ScheduleCliOptions schedule when RequiresInputAutomation(schedule) => [McpCapability.TaskManage, McpCapability.InputAutomation, McpCapability.MacroRead],
            ShortcutCliOptions shortcut when RequiresInputAutomation(shortcut) => [McpCapability.TaskManage, McpCapability.InputAutomation, McpCapability.MacroRead],
            TriggerCliOptions trigger when RequiresMacroRead(trigger) => [McpCapability.TaskManage, McpCapability.InputAutomation, McpCapability.MacroRead],
            TriggerCliOptions trigger when RequiresInputAutomation(trigger) => [McpCapability.TaskManage, McpCapability.InputAutomation],
            ScheduleRunCliOptions or ShortcutRunCliOptions => [McpCapability.TaskManage, McpCapability.InputAutomation, McpCapability.MacroRead],
            ScheduleCliOptions or ShortcutCliOptions or TriggerCliOptions or ScheduleListCliOptions or ShortcutListCliOptions or TriggerListCliOptions => [McpCapability.TaskManage],
            _ => [],
        };

        foreach (var capability in capabilities)
        {
            var failure = Require(capability);
            if (failure is not null)
            {
                return failure;
            }
        }

        return null;
    }

    public bool TryAuthorizeCommandOptions(CliCommandOptions options, out CliCommandOptions authorizedOptions, out McpToolOutcome error)
    {
        authorizedOptions = options;
        error = McpToolOutcomeMapper.Success(string.Empty);
        switch (options)
        {
            case SettingsSetCliOptions settingSet when McpSettingsKeys.IsPolicyKey(settingSet.Key):
            case SettingsResetCliOptions settingReset when McpSettingsKeys.IsPolicyKey(settingReset.Key):
                error = McpToolOutcomeMapper.Denied("MCP security settings can only be changed outside an MCP session.");
                return false;
            case MacroInfoCliOptions macroInfo:
                if (!_pathAuthorizer.TryNormalizeMacroPath(macroInfo.MacroFilePath, out var normalizedInfoPath, out error))
                {
                    return false;
                }

                authorizedOptions = macroInfo with { MacroFilePath = normalizedInfoPath };
                return true;
            case MacroValidateCliOptions macroValidate:
                if (!_pathAuthorizer.TryNormalizeMacroPath(macroValidate.MacroFilePath, out var normalizedValidatePath, out error))
                {
                    return false;
                }

                authorizedOptions = macroValidate with { MacroFilePath = normalizedValidatePath };
                return true;
            case ClipboardCliOptions { FilePath: not null } clipboard:
                var fileReadCapability = Require(McpCapability.FileRead);
                if (fileReadCapability is not null)
                {
                    error = fileReadCapability;
                    return false;
                }

                if (!_pathAuthorizer.TryAuthorizeFileReadPath(clipboard.FilePath, out var normalizedClipboardPath, out error))
                {
                    return false;
                }

                authorizedOptions = clipboard with { FilePath = normalizedClipboardPath };
                return true;
            case ScreenCliOptions { ImagePath: not null } screen:
                var imageReadCapability = Require(McpCapability.FileRead);
                if (imageReadCapability is not null)
                {
                    error = imageReadCapability;
                    return false;
                }

                if (screen.Action is ScreenCliAction.ImageClick)
                {
                    var inputAutomationCapability = Require(McpCapability.InputAutomation);
                    if (inputAutomationCapability is not null)
                    {
                        error = inputAutomationCapability;
                        return false;
                    }
                }

                if (!_pathAuthorizer.TryNormalizeScreenImagePath(screen.ImagePath, out var normalizedImagePath, out error))
                {
                    return false;
                }

                authorizedOptions = screen with { ImagePath = normalizedImagePath };
                return true;
            case ScreenCliOptions { Action: ScreenCliAction.ImageClick }:
                var imageClickCapability = Require(McpCapability.InputAutomation);
                if (imageClickCapability is not null)
                {
                    error = imageClickCapability;
                    return false;
                }

                return true;
            case ScreenshotCliOptions screenshot:
                if (screenshot.Clipboard)
                {
                    var clipboardWriteCapability = Require(McpCapability.ClipboardWrite);
                    if (clipboardWriteCapability is not null)
                    {
                        error = clipboardWriteCapability;
                        return false;
                    }
                }

                if (screenshot.OutputPath is null)
                {
                    return true;
                }

                var fileWriteCapability = Require(McpCapability.FileWrite);
                if (fileWriteCapability is not null)
                {
                    error = fileWriteCapability;
                    return false;
                }

                if (!_pathAuthorizer.TryNormalizeScreenshotOutputPath(screenshot.OutputPath, out var normalizedOutputPath, out error))
                {
                    return false;
                }

                authorizedOptions = screenshot with { OutputPath = normalizedOutputPath };
                return true;
            case ScheduleCliOptions schedule:
                if (!_pathAuthorizer.TryNormalizeOptionalMacroPath(schedule.MacroFilePath, out var normalizedSchedulePath, out error))
                {
                    return false;
                }

                authorizedOptions = schedule with { MacroFilePath = normalizedSchedulePath };
                return true;
            case ShortcutCliOptions shortcut:
                if (!_pathAuthorizer.TryNormalizeOptionalMacroPath(shortcut.MacroFilePath, out var normalizedShortcutPath, out error))
                {
                    return false;
                }

                authorizedOptions = shortcut with { MacroFilePath = normalizedShortcutPath };
                return true;
            case TriggerCliOptions trigger:
                if (!_pathAuthorizer.TryNormalizeOptionalMacroPath(trigger.MacroFilePath, out var normalizedTriggerPath, out error))
                {
                    return false;
                }

                authorizedOptions = trigger with { MacroFilePath = normalizedTriggerPath };
                return true;
            default:
                return true;
        }
    }

    public Task<McpToolOutcome?> TryAuthorizeParsedCommandTaskMacroAsync(CliCommandOptions options, CancellationToken cancellationToken) => options switch
    {
        ScheduleRunCliOptions scheduleRun => TryAuthorizeScheduleTaskMacroAsync(scheduleRun.TaskId, cancellationToken),
        ScheduleCliOptions { Action: ScheduleCliAction.Enable } scheduleEnable => TryAuthorizeScheduleTaskMacroAsync(scheduleEnable.TaskId ?? string.Empty, cancellationToken),
        ScheduleCliOptions { Action: ScheduleCliAction.Edit, Enabled: true, MacroFilePath: null } scheduleEdit => TryAuthorizeScheduleTaskMacroAsync(scheduleEdit.TaskId ?? string.Empty, cancellationToken),
        ShortcutRunCliOptions shortcutRun => TryAuthorizeShortcutTaskMacroAsync(shortcutRun.TaskId, cancellationToken),
        ShortcutCliOptions { Action: ShortcutCliAction.Enable } shortcutEnable => TryAuthorizeShortcutTaskMacroAsync(shortcutEnable.TaskId ?? string.Empty, cancellationToken),
        ShortcutCliOptions { Action: ShortcutCliAction.Edit, Enabled: true, MacroFilePath: null } shortcutEdit => TryAuthorizeShortcutTaskMacroAsync(shortcutEdit.TaskId ?? string.Empty, cancellationToken),
        TriggerCliOptions { Action: TriggerCliAction.Enable } triggerEnable => TryAuthorizeTriggerTaskMacroAsync(triggerEnable.TaskId ?? string.Empty, cancellationToken),
        TriggerCliOptions { Action: TriggerCliAction.Edit, Enabled: true, MacroFilePath: null, TriggerActionVal: null } triggerEdit => TryAuthorizeTriggerTaskMacroAsync(triggerEdit.TaskId ?? string.Empty, cancellationToken),
        _ => Task.FromResult<McpToolOutcome?>(null),
    };

    public Task<McpToolOutcome?> TryAuthorizeScheduleTaskMacroAsync(string taskId, CancellationToken cancellationToken) =>
        TryAuthorizeExistingTaskMacroAsync<ScheduleTaskData>(taskId, _scheduleCliService.ListAsync, static task => task.Id, static task => task.MacroFilePath, "Schedule", cancellationToken);

    public Task<McpToolOutcome?> TryAuthorizeShortcutTaskMacroAsync(string taskId, CancellationToken cancellationToken) =>
        TryAuthorizeExistingTaskMacroAsync<ShortcutTaskData>(taskId, _shortcutCliService.ListAsync, static task => task.Id, static task => task.MacroFilePath, "Shortcut", cancellationToken);

    public Task<McpToolOutcome?> TryAuthorizeTriggerTaskMacroAsync(string taskId, CancellationToken cancellationToken) =>
        TryAuthorizeExistingTaskMacroAsync<TriggerTaskData>(
            taskId,
            _triggerCliService.ListAsync,
            static task => task.Id,
            static task => string.Equals(task.Action, nameof(TriggerOperation.RunMacro), StringComparison.Ordinal) ? task.MacroFilePath : null,
            "Trigger",
            cancellationToken);

    internal static bool RequiresInputAutomation(ScheduleCliOptions options) =>
        options.Action is ScheduleCliAction.Add or ScheduleCliAction.Edit or ScheduleCliAction.Enable;

    internal static bool RequiresInputAutomation(ShortcutCliOptions options) =>
        options.Action is ShortcutCliAction.Add or ShortcutCliAction.Edit or ShortcutCliAction.Enable;

    internal static bool RequiresInputAutomation(TriggerCliOptions options) =>
        options.Action is TriggerCliAction.Add or TriggerCliAction.Edit or TriggerCliAction.Enable
            || options.TriggerActionVal is TriggerOperation.RunMacro;

    internal static bool RequiresMacroRead(TriggerCliOptions options) =>
        options.TriggerActionVal is TriggerOperation.RunMacro
            || !string.IsNullOrWhiteSpace(options.MacroFilePath);

    private async Task<McpToolOutcome?> TryAuthorizeExistingTaskMacroAsync<TTask>(
        string taskId,
        Func<CancellationToken, Task<CliCommandExecutionResult>> listAsync,
        Func<TTask, Guid> getId,
        Func<TTask, string?> getMacroPath,
        string taskKind,
        CancellationToken cancellationToken)
        where TTask : class
    {
        if (!Guid.TryParse(taskId, out var parsedTaskId))
        {
            return McpToolOutcomeMapper.InvalidArguments($"Invalid {taskKind} task id format.");
        }

        var listResult = await listAsync(cancellationToken).ConfigureAwait(false);
        if (!listResult.Success)
        {
            return McpToolOutcomeMapper.FromCliResultRedactingErrorDetails(listResult);
        }

        if (listResult.Data is not TaskListData<TTask> taskList)
        {
            return McpToolOutcomeMapper.RuntimeError($"{taskKind} tasks could not be loaded.");
        }

        var task = taskList.Tasks.FirstOrDefault(candidate => getId(candidate) == parsedTaskId);
        if (task is null)
        {
            return McpToolOutcomeMapper.InvalidArguments($"{taskKind} task was not found.");
        }

        var macroPath = getMacroPath(task);
        if (string.IsNullOrWhiteSpace(macroPath))
        {
            return null;
        }

        var macroReadCapability = Require(McpCapability.MacroRead);
        return macroReadCapability ?? (_pathAuthorizer.TryNormalizeMacroPath(macroPath, out _, out var pathError) ? null : pathError);
    }
}
