using CrossMacro.Application.Automation;

namespace CrossMacro.Mcp.Services.Security;

/// <summary>Which capabilities and existing-task checks a task command needs, shared by the typed task tools and the CLI command tool.</summary>
internal static class McpTaskAdmission
{
    public static bool RequiresInputAutomation(ScheduleCommand command) =>
        command.Action is TaskCommandAction.Add or TaskCommandAction.Edit or TaskCommandAction.Enable;

    public static bool RequiresInputAutomation(ShortcutCommand command) =>
        command.Action is TaskCommandAction.Add or TaskCommandAction.Edit or TaskCommandAction.Enable;

    public static bool RequiresInputAutomation(TriggerCommand command) =>
        command.Action is TaskCommandAction.Add or TaskCommandAction.Edit or TaskCommandAction.Enable
            || command.Operation is TriggerOperation.RunMacro;

    public static bool RequiresInputAutomation(ScheduleOptions options) => RequiresInputAutomation(TaskCommandOptionsMapper.ToApplication(options));

    public static bool RequiresInputAutomation(ShortcutOptions options) => RequiresInputAutomation(TaskCommandOptionsMapper.ToApplication(options));

    public static bool RequiresInputAutomation(TriggerOptions options) => RequiresInputAutomation(TaskCommandOptionsMapper.ToApplication(options));

    public static bool RequiresMacroRead(TriggerCommand command) =>
        command.Operation is TriggerOperation.RunMacro
            || !string.IsNullOrWhiteSpace(command.MacroFilePath);

    public static bool RequiresMacroRead(TriggerOptions options) => RequiresMacroRead(TaskCommandOptionsMapper.ToApplication(options));

    /// <summary>Whether the stored task's macro files must be authorized because the command may enable it.</summary>
    public static bool EnablesStoredTask(ScheduleCommand command) =>
        command.Action is TaskCommandAction.Enable
            || command is { Action: TaskCommandAction.Edit, Enabled: true, MacroFilePath: null };

    public static bool EnablesStoredTask(ShortcutCommand command) =>
        command.Action is TaskCommandAction.Enable;

    public static bool EnablesStoredTask(TriggerCommand command) =>
        command.Action is TaskCommandAction.Enable
            || command is { Action: TaskCommandAction.Edit, Enabled: true, MacroFilePath: null, Operation: null };

    public static bool EnablesStoredTask(ScheduleOptions options) => EnablesStoredTask(TaskCommandOptionsMapper.ToApplication(options));

    public static bool EnablesStoredTask(ShortcutOptions options) => EnablesStoredTask(TaskCommandOptionsMapper.ToApplication(options));

    public static bool EnablesStoredTask(TriggerOptions options) => EnablesStoredTask(TaskCommandOptionsMapper.ToApplication(options));
}
