using CrossMacro.Application.Automation;
namespace CrossMacro.Mcp.Services.Security;

public sealed class McpToolAuthorization(
    IMcpCapabilityPolicy capabilityPolicy,
    McpPathAuthorizer pathAuthorizer,
    IScheduleCommands scheduleCommands,
    IShortcutCommands shortcutCommands,
    ITriggerCommands triggerCommands,
    AutomationTaskAuthorization taskAuthorization)
{
    private readonly IMcpCapabilityPolicy _capabilityPolicy = capabilityPolicy;
    private readonly McpPathAuthorizer _pathAuthorizer = pathAuthorizer;
    private readonly IScheduleCommands _scheduleCommands = scheduleCommands;
    private readonly IShortcutCommands _shortcutCommands = shortcutCommands;
    private readonly ITriggerCommands _triggerCommands = triggerCommands;
    private readonly AutomationTaskAuthorization _taskAuthorization = taskAuthorization;

    internal async Task<T> RunWithTaskAuthorizationAsync<T>(Func<Task<T>> operation, Func<McpToolOutcome, T> deniedResult)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(deniedResult);
        McpToolOutcome? denial = null;
        try
        {
            var result = await _taskAuthorization.RunAsync(path =>
            {
                if (string.IsNullOrWhiteSpace(path))
                {
                    return;
                }
                try
                {
                    denial = Require(McpCapability.MacroRead)
                        ?? (_pathAuthorizer.TryNormalizeMacroPath(path, out _, out var error) ? null : error);
                }
                catch (Exception exception) when (exception is not OutOfMemoryException and not OperationCanceledException)
                {
                    denial = McpToolOutcomeMapper.FromException(exception);
                }
                if (denial is not null)
                {
                    throw new UnauthorizedAccessException(denial.Message);
                }
            }, operation).ConfigureAwait(false);
            // A CLI host may map the exception itself; keep the original authorization outcome.
            return denial is null ? result : deniedResult(denial);
        }
        catch (UnauthorizedAccessException) when (denial is not null)
        {
            return deniedResult(denial);
        }
    }

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
        ArgumentNullException.ThrowIfNull(options);
        IReadOnlyList<McpCapability>? capabilities = options switch
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
            DoctorCliOptions => [],
            _ => null,
        };

        if (capabilities is null)
        {
            return McpToolOutcomeMapper.Denied("This command has no MCP capability policy.");
        }

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
                if (!TryAuthorizeTaskMacroPath(schedule.MacroFilePath, out var normalizedSchedulePath, out error))
                {
                    return false;
                }

                authorizedOptions = schedule with { MacroFilePath = normalizedSchedulePath };
                return true;
            case ShortcutCliOptions shortcut:
                if (!TryAuthorizeTaskMacroPath(shortcut.MacroFilePath, out var normalizedShortcutPath, out error))
                {
                    return false;
                }

                authorizedOptions = shortcut with { MacroFilePath = normalizedShortcutPath };
                return true;
            case TriggerCliOptions trigger:
                if (!TryAuthorizeTaskMacroPath(trigger.MacroFilePath, out var normalizedTriggerPath, out error))
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
        TryAuthorizeExistingTaskMacroAsync<ScheduledTask>(taskId, _scheduleCommands.ListAsync, static task => task.Id, static task => task.MacroFilePath, "Schedule", cancellationToken);

    public Task<McpToolOutcome?> TryAuthorizeShortcutTaskMacroAsync(string taskId, CancellationToken cancellationToken) =>
        TryAuthorizeExistingTaskMacroAsync<ShortcutTask>(taskId, _shortcutCommands.ListAsync, static task => task.Id, static task => task.MacroFilePath, "Shortcut", cancellationToken);

    public Task<McpToolOutcome?> TryAuthorizeTriggerTaskMacroAsync(string taskId, CancellationToken cancellationToken) =>
        TryAuthorizeExistingTaskMacroAsync<TriggerTask>(
            taskId,
            _triggerCommands.ListAsync,
            static task => task.Id,
            static task => task.Action is TriggerOperation.RunMacro ? task.MacroFilePath : null,
            "Trigger",
            cancellationToken);

    public bool TryAuthorizeTaskMacroPath(string? macroPath, out string? normalizedPath, out McpToolOutcome error) =>
        _pathAuthorizer.TryNormalizeOptionalMacroPath(macroPath, out normalizedPath, out error);

    internal static bool RequiresInputAutomation(ScheduleCliOptions options) => RequiresInputAutomation(TaskCommandOptionsMapper.ToApplication(options));
    internal static bool RequiresInputAutomation(ShortcutCliOptions options) => RequiresInputAutomation(TaskCommandOptionsMapper.ToApplication(options));
    internal static bool RequiresInputAutomation(TriggerCliOptions options) => RequiresInputAutomation(TaskCommandOptionsMapper.ToApplication(options));

    internal static bool RequiresInputAutomation(ScheduleCommand options) =>
        options.Action is ScheduleCommandAction.Add or ScheduleCommandAction.Edit or ScheduleCommandAction.Enable;

    internal static bool RequiresInputAutomation(ShortcutCommand options) =>
        options.Action is ShortcutCommandAction.Add or ShortcutCommandAction.Edit or ShortcutCommandAction.Enable;

    internal static bool RequiresInputAutomation(TriggerCommand options) =>
        options.Action is TriggerCommandAction.Add or TriggerCommandAction.Edit or TriggerCommandAction.Enable
            || options.TriggerActionVal is TriggerOperation.RunMacro;

    internal static bool RequiresMacroRead(TriggerCliOptions options) => RequiresMacroRead(TaskCommandOptionsMapper.ToApplication(options));

    internal static bool RequiresMacroRead(TriggerCommand options) =>
        options.TriggerActionVal is TriggerOperation.RunMacro
            || !string.IsNullOrWhiteSpace(options.MacroFilePath);

    private async Task<McpToolOutcome?> TryAuthorizeExistingTaskMacroAsync<TTask>(
        string taskId,
        Func<CancellationToken, Task<TaskCommandResult<TTask>>> listAsync,
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
            return McpToolOutcomeMapper.InvalidArguments(listResult.Message);
        }

        if (listResult.Tasks is not { } tasks)
        {
            return McpToolOutcomeMapper.RuntimeError($"{taskKind} tasks could not be loaded.");
        }

        var task = tasks.FirstOrDefault(candidate => getId(candidate) == parsedTaskId);
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
