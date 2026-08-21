namespace CrossMacro.Mcp;

public sealed partial class CrossMacroMcpTools
{
    private McpToolOutcome? RequireCapability(McpCapability capability) =>
        _capabilityPolicy.IsAllowed(capability) ? null : _capabilityPolicy.Require(capability);

    private McpToolOutcome? RequireTaskManagementCapability(
        bool requiresInputAutomation,
        bool requiresMacroRead = false)
    {
        var taskManagementCapability = RequireCapability(McpCapability.TaskManage);
        if (taskManagementCapability is not null)
        {
            return taskManagementCapability;
        }

        var inputAutomationCapability = requiresInputAutomation
            ? RequireCapability(McpCapability.InputAutomation)
            : null;
        return inputAutomationCapability ?? (requiresMacroRead
            ? RequireCapability(McpCapability.MacroRead)
            : null);
    }

    private McpToolOutcome? RequireAutomationCapability(string? operation)
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
            var failure = RequireCapability(capability);
            if (failure is not null)
            {
                return failure;
            }
        }

        return null;
    }

    private bool IsToolEnabled(McpToolDefinition definition)
    {
        if (definition.OperationCapabilities.Count > 0)
        {
            return definition.OperationCapabilities.Any(
                operation => operation.Capabilities.All(_capabilityPolicy.IsAllowed));
        }

        return definition.CapabilityRequirement is McpCapabilityRequirement.Any
            ? _capabilityPolicy.IsAnyAllowed([.. definition.Capabilities])
            : definition.Capabilities.All(_capabilityPolicy.IsAllowed);
    }

    private IReadOnlyList<McpToolCapabilityStatus> GetOperationCapabilityStatuses(McpToolDefinition definition) =>
        definition.OperationCapabilities
            .Select(operation => CreateCapabilityStatus(operation.Operation, operation.Capabilities))
            .ToArray();

    private McpToolCapabilityStatus CreateCapabilityStatus(
        string operation,
        IReadOnlyList<McpCapability> capabilities) =>
        new(
            operation,
            capabilities.Select(static capability => capability.ToString()).ToArray(),
            capabilities.All(_capabilityPolicy.IsAllowed));

    private McpToolOutcome? RequireShellCapability(IReadOnlyList<string> steps) =>
        steps.Any(RunScriptSyntax.IsShellStep)
            ? RequireCapability(McpCapability.ShellExecute)
            : null;

    private McpToolOutcome? RequireCommandCapability(CliCommandOptions options)
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
            var failure = RequireCapability(capability);
            if (failure is not null)
            {
                return failure;
            }
        }

        return null;
    }

    private bool TryAuthorizeCommandOptions(
        CliCommandOptions options,
        out CliCommandOptions authorizedOptions,
        out McpToolOutcome error)
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
                if (!TryNormalizeMacroPath(macroInfo.MacroFilePath, out var normalizedInfoPath, out error))
                {
                    return false;
                }

                authorizedOptions = macroInfo with { MacroFilePath = normalizedInfoPath };
                return true;
            case MacroValidateCliOptions macroValidate:
                if (!TryNormalizeMacroPath(macroValidate.MacroFilePath, out var normalizedValidatePath, out error))
                {
                    return false;
                }

                authorizedOptions = macroValidate with { MacroFilePath = normalizedValidatePath };
                return true;
            case ClipboardCliOptions { FilePath: not null } clipboard:
                var fileReadCapability = RequireCapability(McpCapability.FileRead);
                if (fileReadCapability is not null)
                {
                    error = fileReadCapability;
                    return false;
                }

                if (!_pathPolicy.TryAuthorize(clipboard.FilePath, McpPathKind.FileRead, requireExisting: true, out var normalizedClipboardPath, out error))
                {
                    return false;
                }

                authorizedOptions = clipboard with { FilePath = normalizedClipboardPath };
                return true;
            case ScreenCliOptions { ImagePath: not null } screen:
                var imageReadCapability = RequireCapability(McpCapability.FileRead);
                if (imageReadCapability is not null)
                {
                    error = imageReadCapability;
                    return false;
                }

                if (screen.Action is ScreenCliAction.ImageClick)
                {
                    var inputAutomationCapability = RequireCapability(McpCapability.InputAutomation);
                    if (inputAutomationCapability is not null)
                    {
                        error = inputAutomationCapability;
                        return false;
                    }
                }

                if (!TryNormalizeScreenImagePath(screen.ImagePath, out var normalizedImagePath, out error))
                {
                    return false;
                }

                authorizedOptions = screen with { ImagePath = normalizedImagePath };
                return true;
            case ScreenCliOptions { Action: ScreenCliAction.ImageClick }:
                var imageClickCapability = RequireCapability(McpCapability.InputAutomation);
                if (imageClickCapability is not null)
                {
                    error = imageClickCapability;
                    return false;
                }

                return true;
            case ScreenshotCliOptions screenshot:
                if (screenshot.Clipboard)
                {
                    var clipboardWriteCapability = RequireCapability(McpCapability.ClipboardWrite);
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

                var fileWriteCapability = RequireCapability(McpCapability.FileWrite);
                if (fileWriteCapability is not null)
                {
                    error = fileWriteCapability;
                    return false;
                }

                if (!TryNormalizeScreenshotOutputPath(screenshot.OutputPath, out var normalizedOutputPath, out error))
                {
                    return false;
                }

                authorizedOptions = screenshot with { OutputPath = normalizedOutputPath };
                return true;
            case ScheduleCliOptions schedule:
                if (!TryNormalizeOptionalMacroPath(schedule.MacroFilePath, out var normalizedSchedulePath, out error))
                {
                    return false;
                }

                authorizedOptions = schedule with { MacroFilePath = normalizedSchedulePath };
                return true;
            case ShortcutCliOptions shortcut:
                if (!TryNormalizeOptionalMacroPath(shortcut.MacroFilePath, out var normalizedShortcutPath, out error))
                {
                    return false;
                }

                authorizedOptions = shortcut with { MacroFilePath = normalizedShortcutPath };
                return true;
            case TriggerCliOptions trigger:
                if (!TryNormalizeOptionalMacroPath(trigger.MacroFilePath, out var normalizedTriggerPath, out error))
                {
                    return false;
                }

                authorizedOptions = trigger with { MacroFilePath = normalizedTriggerPath };
                return true;
            default:
                return true;
        }
    }

    private bool TryNormalizeOptionalMacroPath(
        string? macroPath,
        out string? normalizedMacroPath,
        out McpToolOutcome error)
    {
        normalizedMacroPath = macroPath;
        if (string.IsNullOrWhiteSpace(macroPath))
        {
            error = McpToolOutcomeMapper.Success(string.Empty);
            return true;
        }

        if (!TryNormalizeMacroPath(macroPath, out var normalizedPath, out error))
        {
            normalizedMacroPath = null;
            return false;
        }

        normalizedMacroPath = normalizedPath;
        return true;
    }

    private static bool RequiresInputAutomation(ScheduleCliOptions options) =>
        options.Action is ScheduleCliAction.Add or ScheduleCliAction.Edit or ScheduleCliAction.Enable;

    private static bool RequiresInputAutomation(ShortcutCliOptions options) =>
        options.Action is ShortcutCliAction.Add or ShortcutCliAction.Edit or ShortcutCliAction.Enable;

    private static bool RequiresInputAutomation(TriggerCliOptions options) =>
        options.Action is TriggerCliAction.Add or TriggerCliAction.Edit or TriggerCliAction.Enable
            || options.TriggerActionVal is TriggerOperation.RunMacro;

    private static bool RequiresMacroRead(TriggerCliOptions options) =>
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

        var macroReadCapability = RequireCapability(McpCapability.MacroRead);
        if (macroReadCapability is not null)
        {
            return macroReadCapability;
        }

        return TryNormalizeMacroPath(macroPath, out _, out var pathError)
            ? null
            : pathError;
    }

    private Task<McpToolOutcome?> TryAuthorizeScheduleTaskMacroAsync(string taskId, CancellationToken cancellationToken) =>
        TryAuthorizeExistingTaskMacroAsync<ScheduleTaskData>(
            taskId,
            _scheduleCliService.ListAsync,
            static task => task.Id,
            static task => task.MacroFilePath,
            "Schedule",
            cancellationToken);

    private Task<McpToolOutcome?> TryAuthorizeShortcutTaskMacroAsync(string taskId, CancellationToken cancellationToken) =>
        TryAuthorizeExistingTaskMacroAsync<ShortcutTaskData>(
            taskId,
            _shortcutCliService.ListAsync,
            static task => task.Id,
            static task => task.MacroFilePath,
            "Shortcut",
            cancellationToken);

    private async Task<McpToolOutcome?> TryAuthorizeTriggerTaskMacroAsync(string taskId, CancellationToken cancellationToken)
    {
        var authorization = await TryAuthorizeExistingTaskMacroAsync<TriggerTaskData>(
            taskId,
            _triggerCliService.ListAsync,
            static task => task.Id,
            static task => string.Equals(task.Action, nameof(TriggerOperation.RunMacro), StringComparison.Ordinal)
                ? task.MacroFilePath
                : null,
            "Trigger",
            cancellationToken).ConfigureAwait(false);
        return authorization;
    }

    private Task<McpToolOutcome?> TryAuthorizeParsedCommandTaskMacroAsync(
        CliCommandOptions options,
        CancellationToken cancellationToken) => options switch
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
}
