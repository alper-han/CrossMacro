using CrossMacro.Application.Automation;
namespace CrossMacro.Mcp.Services.Security;

public sealed class McpToolAuthorization(
    IMcpCapabilityPolicy capabilityPolicy,
    McpPathAuthorizer pathAuthorizer,
    ITaskCommands<ScheduledTask, ScheduleCommand> scheduleCommands,
    ITaskCommands<ShortcutTask, ShortcutCommand> shortcutCommands,
    ITaskCommands<TriggerTask, TriggerCommand> triggerCommands,
    AutomationTaskAuthorization taskAuthorization)
{
    private readonly IMcpCapabilityPolicy _capabilityPolicy = capabilityPolicy;
    private readonly McpPathAuthorizer _pathAuthorizer = pathAuthorizer;
    private readonly ITaskCommands<ScheduledTask, ScheduleCommand> _scheduleCommands = scheduleCommands;
    private readonly ITaskCommands<ShortcutTask, ShortcutCommand> _shortcutCommands = shortcutCommands;
    private readonly ITaskCommands<TriggerTask, TriggerCommand> _triggerCommands = triggerCommands;
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

        return RequireAll(required.Capabilities);
    }

    private McpToolOutcome? RequireAll(IReadOnlyList<McpCapability> capabilities)
    {
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
            PlayCliOptions => McpAutomationCapabilities.For(McpAutomationOperationKind.Play),
            RunCliOptions => McpAutomationCapabilities.For(McpAutomationOperationKind.Run),
            RecordCliOptions => McpAutomationCapabilities.For(McpAutomationOperationKind.Record),
            MacroInfoCliOptions or MacroValidateCliOptions => [McpCapability.MacroRead],
            SettingsGetCliOptions or SettingsListKeysCliOptions => [McpCapability.SettingsRead],
            SettingsSetCliOptions or SettingsResetCliOptions => [McpCapability.SettingsWrite],
            ProfileCliOptions => [McpCapability.ProfileManage],
            TextExpansionCliOptions { Action: TextExpansionCliAction.List or TextExpansionCliAction.Test } => [McpCapability.TextExpansionRead],
            TextExpansionCliOptions => [McpCapability.TextExpansionWrite],
            ScheduleCliOptions schedule when McpTaskAdmission.RequiresInputAutomation(schedule) => [McpCapability.TaskManage, McpCapability.InputAutomation, McpCapability.MacroRead],
            ShortcutCliOptions shortcut when McpTaskAdmission.RequiresInputAutomation(shortcut) => [McpCapability.TaskManage, McpCapability.InputAutomation, McpCapability.MacroRead],
            TriggerCliOptions trigger when McpTaskAdmission.RequiresMacroRead(trigger) => [McpCapability.TaskManage, McpCapability.InputAutomation, McpCapability.MacroRead],
            TriggerCliOptions trigger when McpTaskAdmission.RequiresInputAutomation(trigger) => [McpCapability.TaskManage, McpCapability.InputAutomation],
            ScheduleRunCliOptions or ShortcutRunCliOptions => [McpCapability.TaskManage, McpCapability.InputAutomation, McpCapability.MacroRead],
            ScheduleCliOptions or ShortcutCliOptions or TriggerCliOptions or ScheduleListCliOptions or ShortcutListCliOptions or TriggerListCliOptions => [McpCapability.TaskManage],
            DoctorCliOptions => [],
            _ => null,
        };

        if (capabilities is null)
        {
            return McpToolOutcomeMapper.Denied("This command has no MCP capability policy.");
        }

        return RequireAll(capabilities);
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

                if (!TryAuthorizeTaskMacroPath(shortcut.ReleaseMacroFilePath, out var normalizedReleasePath, out error))
                {
                    return false;
                }
                authorizedOptions = shortcut with { MacroFilePath = normalizedShortcutPath, ReleaseMacroFilePath = normalizedReleasePath };
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
        ScheduleRunCliOptions scheduleRun => TryAuthorizeTaskMacroAsync(scheduleRun.TaskId, _scheduleCommands, ScheduleTaskKind.Instance, cancellationToken),
        ScheduleCliOptions schedule when McpTaskAdmission.EnablesStoredTask(schedule) => TryAuthorizeTaskMacroAsync(schedule.TaskId ?? string.Empty, _scheduleCommands, ScheduleTaskKind.Instance, cancellationToken),
        ShortcutRunCliOptions shortcutRun => TryAuthorizeTaskMacroAsync(shortcutRun.TaskId, _shortcutCommands, ShortcutTaskKind.Instance, cancellationToken),
        ShortcutCliOptions shortcut when McpTaskAdmission.EnablesStoredTask(shortcut) => TryAuthorizeTaskMacroAsync(shortcut.TaskId ?? string.Empty, _shortcutCommands, ShortcutTaskKind.Instance, cancellationToken),
        TriggerCliOptions trigger when McpTaskAdmission.EnablesStoredTask(trigger) => TryAuthorizeTaskMacroAsync(trigger.TaskId ?? string.Empty, _triggerCommands, TriggerTaskKind.Instance, cancellationToken),
        _ => Task.FromResult<McpToolOutcome?>(null),
    };

    public bool TryAuthorizeTaskMacroPath(string? macroPath, out string? normalizedPath, out McpToolOutcome error) =>
        _pathAuthorizer.TryNormalizeOptionalMacroPath(macroPath, out normalizedPath, out error);

    /// <summary>Authorizes the macro files of an existing task, for commands that run it or may enable it.</summary>
    internal async Task<McpToolOutcome?> TryAuthorizeTaskMacroAsync<TTask, TCommand>(
        string taskId,
        ITaskCommands<TTask, TCommand> commands,
        AutomationTaskKind<TTask> kind,
        CancellationToken cancellationToken)
        where TTask : class
        where TCommand : ITaskCommand
    {
        if (!Guid.TryParse(taskId, out var parsedTaskId))
        {
            return McpToolOutcomeMapper.InvalidArguments($"Invalid {kind.Title} task id format.");
        }

        var listResult = await commands.ListAsync(cancellationToken).ConfigureAwait(false);
        if (!listResult.Success)
        {
            return McpToolOutcomeMapper.InvalidArguments(listResult.Message);
        }

        if (listResult.Tasks is not { } tasks)
        {
            return McpToolOutcomeMapper.RuntimeError($"{kind.Title} tasks could not be loaded.");
        }

        var task = tasks.FirstOrDefault(candidate => kind.IdOf(candidate) == parsedTaskId);
        if (task is null)
        {
            return McpToolOutcomeMapper.InvalidArguments($"{kind.Title} task was not found.");
        }

        var macroPaths = kind.MacroPaths(task).OfType<string>().Where(static path => !string.IsNullOrWhiteSpace(path)).ToArray();
        if (macroPaths.Length is 0)
        {
            return null;
        }

        var macroReadCapability = Require(McpCapability.MacroRead);
        if (macroReadCapability is not null)
        {
            return macroReadCapability;
        }

        foreach (var macroPath in macroPaths)
        {
            if (!_pathAuthorizer.TryNormalizeMacroPath(macroPath, out _, out var pathError))
            {
                return pathError;
            }
        }

        return null;
    }
}
