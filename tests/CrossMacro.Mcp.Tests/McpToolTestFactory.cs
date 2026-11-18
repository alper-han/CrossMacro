using CrossMacro.Application.Profiles;
using CrossMacro.Commands.Automation;
using CrossMacro.Commands.Diagnostics;
using CrossMacro.Commands.Execution;
using CrossMacro.Commands.QuickSetup;
using CrossMacro.Commands.Runtime;
using CrossMacro.Commands.Settings;
using CrossMacro.Core.Models.Profiles;
using CrossMacro.Core.Services.Clipboard;
using CrossMacro.Core.Services.Profiles;
using CrossMacro.Platform.Abstractions.Diagnostics;
using CrossMacro.Platform.Abstractions.Input.Pointer;
using CrossMacro.Platform.Abstractions.Runtime;

namespace CrossMacro.Mcp.Tests;

internal static class McpToolTestFactory
{
    internal static IDoctorService CreateDoctorService() => new TestDoctorService(new DoctorReport { Checks = [] });

    internal static IProfileManager CreateProfileManager() => new TestProfileManager(new ProfileInfo { Id = "work", Name = "Work" });

    internal static ISettingsCommandService CreateSettingsCommandService() => CreateSettingsCommandService(new AppSettings());

    internal static SettingsCommandService CreateSettingsCommandService(AppSettings settings)
    {
        var settingsService = new TestSettingsService(settings);
        return new SettingsCommandService(settingsService, new CrossMacro.Application.Settings.SettingsChangeCoordinator(settingsService));
    }

    internal static IMacroExecutionService CreateMacroExecutionService() => new TestMacroExecutionService();

    internal static IClipboardCommandService CreateClipboardCommandService() => new TestClipboardCommandService();

    internal static IWindowCommandService CreateWindowCommandService() => new TestWindowCommandService();

    internal static IScreenshotCaptureService CreateScreenshotCaptureService() => new TestScreenshotCaptureService();

    internal static IImageAssetCodec CreateImageAssetCodec() => new TestImageAssetCodec();

    internal static IImageClipboardReader CreateImageClipboardReader() => new TestImageClipboardReader { IsSupported = false };

    internal static IImageClipboardService CreateImageClipboardService() => new TestImageClipboardService();

    internal static McpOperationCoordinator CreateOperationCoordinator() => new();

    internal static IRunScriptExecutionService CreateRunScriptExecutionService() => new TestRunScriptExecutionService();

    internal static IRecordExecutionService CreateRecordExecutionService() => new TestRecordExecutionService();

    internal static IPreflightService CreatePreflightService() => new TestPreflightService();

    internal static ICommandLine CreateCommandLine(ICliCommandHandlerResolver? resolver = null) =>
        new CliCommandLine(new CliCommandExecutor(resolver ?? new TestCliCommandHandlerResolver()));

    internal static McpAutomationTools CreateAutomationTools(
        IMacroExecutionService? macroExecutionService = null,
        McpOperationCoordinator? operationCoordinator = null,
        IRunScriptExecutionService? runScriptExecutionService = null,
        IRecordExecutionService? recordExecutionService = null,
        IPreflightService? preflightService = null,
        IMcpCapabilityPolicy? capabilityPolicy = null,
        IMcpPathPolicy? pathPolicy = null,
        IRunnableTaskCommands<ScheduledTask, ScheduleCommand>? scheduleCommands = null,
        IRunnableTaskCommands<ShortcutTask, ShortcutCommand>? shortcutCommands = null,
        ITaskCommands<TriggerTask, TriggerCommand>? triggerCommands = null)
    {
        var dependencies = CreateAuthorization(capabilityPolicy, pathPolicy, scheduleCommands, shortcutCommands, triggerCommands);
        var coordinator = operationCoordinator ?? CreateOperationCoordinator();
        return new McpAutomationTools(CreateAutomation(macroExecutionService, runScriptExecutionService, recordExecutionService, preflightService), coordinator, dependencies.Authorization, dependencies.PathAuthorizer);
    }

    internal static AutomationCommandService CreateAutomation(
        IMacroExecutionService? macroExecutionService = null,
        IRunScriptExecutionService? runScriptExecutionService = null,
        IRecordExecutionService? recordExecutionService = null,
        IPreflightService? preflightService = null) =>
        new(macroExecutionService ?? CreateMacroExecutionService(), runScriptExecutionService ?? CreateRunScriptExecutionService(), recordExecutionService ?? CreateRecordExecutionService(), preflightService ?? CreatePreflightService());

    internal static McpCommandTools CreateCommandTools(
        IMacroExecutionService? macroExecutionService = null,
        McpOperationCoordinator? operationCoordinator = null,
        IRunScriptExecutionService? runScriptExecutionService = null,
        IRecordExecutionService? recordExecutionService = null,
        IPreflightService? preflightService = null,
        ICommandLine? commandLine = null,
        IProfileOperations? profileOperations = null,
        IMcpCapabilityPolicy? capabilityPolicy = null,
        IMcpPathPolicy? pathPolicy = null,
        IRunnableTaskCommands<ScheduledTask, ScheduleCommand>? scheduleCommands = null,
        IRunnableTaskCommands<ShortcutTask, ShortcutCommand>? shortcutCommands = null,
        ITaskCommands<TriggerTask, TriggerCommand>? triggerCommands = null)
    {
        var dependencies = CreateAuthorization(capabilityPolicy, pathPolicy, scheduleCommands, shortcutCommands, triggerCommands);
        var automation = CreateAutomation(macroExecutionService, runScriptExecutionService, recordExecutionService, preflightService);
        return new McpCommandTools(automation, operationCoordinator ?? CreateOperationCoordinator(), commandLine ?? CreateCommandLine(), profileOperations ?? new ProfileOperations(new ManageProfile(CreateProfileManager())), dependencies.Authorization, dependencies.PathAuthorizer);
    }

    internal static McpScreenTools CreateScreenTools(
        FakeScreenReader? screenReader = null,
        IScreenshotCaptureService? screenshotCaptureService = null,
        IImageAssetCodec? imageAssetCodec = null,
        IMcpCapabilityPolicy? capabilityPolicy = null,
        IMcpPathPolicy? pathPolicy = null,
        IRunnableTaskCommands<ScheduledTask, ScheduleCommand>? scheduleCommands = null,
        IRunnableTaskCommands<ShortcutTask, ShortcutCommand>? shortcutCommands = null,
        ITaskCommands<TriggerTask, TriggerCommand>? triggerCommands = null,
        IMousePositionProvider? mousePositionProvider = null)
    {
        var dependencies = CreateAuthorization(capabilityPolicy, pathPolicy, scheduleCommands, shortcutCommands, triggerCommands);
        return new McpScreenTools(screenshotCaptureService ?? CreateScreenshotCaptureService(), imageAssetCodec ?? CreateImageAssetCodec(), dependencies.Authorization, dependencies.PathAuthorizer, mousePositionProvider, screenReader ?? new FakeScreenReader());
    }

    internal static McpClipboardTools CreateClipboardTools(
        IClipboardCommandService? clipboardCommandService = null,
        IImageAssetCodec? imageAssetCodec = null,
        IImageClipboardReader? imageClipboardReader = null,
        IImageClipboardService? imageClipboardService = null,
        IMcpCapabilityPolicy? capabilityPolicy = null,
        IMcpPathPolicy? pathPolicy = null,
        IRunnableTaskCommands<ScheduledTask, ScheduleCommand>? scheduleCommands = null,
        IRunnableTaskCommands<ShortcutTask, ShortcutCommand>? shortcutCommands = null,
        ITaskCommands<TriggerTask, TriggerCommand>? triggerCommands = null)
    {
        var dependencies = CreateAuthorization(capabilityPolicy, pathPolicy, scheduleCommands, shortcutCommands, triggerCommands);
        return new McpClipboardTools(clipboardCommandService ?? CreateClipboardCommandService(), imageAssetCodec ?? CreateImageAssetCodec(), imageClipboardReader ?? CreateImageClipboardReader(), imageClipboardService ?? CreateImageClipboardService(), dependencies.Authorization, dependencies.PathAuthorizer);
    }

    internal static McpMacroTools CreateMacroTools(IMacroExecutionService? macroExecutionService = null, IMcpCapabilityPolicy? capabilityPolicy = null, IMcpPathPolicy? pathPolicy = null, IRunnableTaskCommands<ScheduledTask, ScheduleCommand>? scheduleCommands = null, IRunnableTaskCommands<ShortcutTask, ShortcutCommand>? shortcutCommands = null, ITaskCommands<TriggerTask, TriggerCommand>? triggerCommands = null)
    {
        var dependencies = CreateAuthorization(capabilityPolicy, pathPolicy, scheduleCommands, shortcutCommands, triggerCommands);
        return new McpMacroTools(macroExecutionService ?? CreateMacroExecutionService(), dependencies.Authorization, dependencies.PathAuthorizer);
    }

    internal static McpWindowTools CreateWindowTools(IWindowCommandService? windowCommandService = null, IMcpCapabilityPolicy? capabilityPolicy = null, IMcpPathPolicy? pathPolicy = null, IRunnableTaskCommands<ScheduledTask, ScheduleCommand>? scheduleCommands = null, IRunnableTaskCommands<ShortcutTask, ShortcutCommand>? shortcutCommands = null, ITaskCommands<TriggerTask, TriggerCommand>? triggerCommands = null)
    {
        var dependencies = CreateAuthorization(capabilityPolicy, pathPolicy, scheduleCommands, shortcutCommands, triggerCommands);
        return new McpWindowTools(windowCommandService ?? CreateWindowCommandService(), dependencies.Authorization);
    }

    internal static McpTaskTools CreateTaskTools(IRunnableTaskCommands<ScheduledTask, ScheduleCommand>? scheduleCommands = null, IRunnableTaskCommands<ShortcutTask, ShortcutCommand>? shortcutCommands = null, ITaskCommands<TriggerTask, TriggerCommand>? triggerCommands = null, IMcpCapabilityPolicy? capabilityPolicy = null, IMcpPathPolicy? pathPolicy = null)
    {
        var dependencies = CreateAuthorization(capabilityPolicy, pathPolicy, scheduleCommands, shortcutCommands, triggerCommands);
        return new McpTaskTools(dependencies.Schedule, dependencies.Shortcut, dependencies.Trigger, dependencies.Authorization);
    }

    internal static McpRuntimeTools CreateRuntimeTools(IRuntimeContext? runtimeContext = null, IDoctorService? doctorService = null, IProfileManager? profileManager = null, IQuickSetupCommandService? quickSetupCommandService = null, McpOperationCoordinator? operationCoordinator = null, IMcpCapabilityPolicy? capabilityPolicy = null, IMcpPathPolicy? pathPolicy = null, IImageClipboardReader? imageClipboardReader = null, IImageClipboardService? imageClipboardService = null, ILinuxInputDiagnostics? linuxInputDiagnostics = null, IRunnableTaskCommands<ScheduledTask, ScheduleCommand>? scheduleCommands = null, IRunnableTaskCommands<ShortcutTask, ShortcutCommand>? shortcutCommands = null, ITaskCommands<TriggerTask, TriggerCommand>? triggerCommands = null)
    {
        var selectedCapabilityPolicy = capabilityPolicy ?? new AllowAllMcpCapabilityPolicy();
        var selectedPathPolicy = pathPolicy ?? new AllowAllMcpPathPolicy();
        var dependencies = CreateAuthorization(selectedCapabilityPolicy, selectedPathPolicy, scheduleCommands, shortcutCommands, triggerCommands);
        return new McpRuntimeTools(runtimeContext ?? new TestRuntimeContext(), doctorService ?? CreateDoctorService(), profileManager ?? CreateProfileManager(), quickSetupCommandService ?? new TestQuickSetupCommandService(), operationCoordinator ?? CreateOperationCoordinator(), selectedCapabilityPolicy, dependencies.Authorization, selectedPathPolicy, imageClipboardReader ?? CreateImageClipboardReader(), imageClipboardService ?? CreateImageClipboardService(), linuxInputDiagnostics);
    }

    internal static McpSettingsTools CreateSettingsTools(ISettingsCommandService? settingsCommandService = null, IMcpCapabilityPolicy? capabilityPolicy = null, IMcpPathPolicy? pathPolicy = null, IRunnableTaskCommands<ScheduledTask, ScheduleCommand>? scheduleCommands = null, IRunnableTaskCommands<ShortcutTask, ShortcutCommand>? shortcutCommands = null, ITaskCommands<TriggerTask, TriggerCommand>? triggerCommands = null)
    {
        var dependencies = CreateAuthorization(capabilityPolicy, pathPolicy, scheduleCommands, shortcutCommands, triggerCommands);
        return new McpSettingsTools(settingsCommandService ?? CreateSettingsCommandService(), dependencies.Authorization);
    }

    internal static McpProfileTools CreateProfileTools(IProfileOperations? profileOperations = null, IMcpCapabilityPolicy? capabilityPolicy = null, IMcpPathPolicy? pathPolicy = null, IRunnableTaskCommands<ScheduledTask, ScheduleCommand>? scheduleCommands = null, IRunnableTaskCommands<ShortcutTask, ShortcutCommand>? shortcutCommands = null, ITaskCommands<TriggerTask, TriggerCommand>? triggerCommands = null)
    {
        var dependencies = CreateAuthorization(capabilityPolicy, pathPolicy, scheduleCommands, shortcutCommands, triggerCommands);
        return new McpProfileTools(profileOperations ?? new ProfileOperations(new ManageProfile(CreateProfileManager())), dependencies.Authorization);
    }

    internal static McpTextExpansionTools CreateTextExpansionTools(ITextExpansionCommandService? textExpansionCommandService = null, IMcpCapabilityPolicy? capabilityPolicy = null, IMcpPathPolicy? pathPolicy = null, IRunnableTaskCommands<ScheduledTask, ScheduleCommand>? scheduleCommands = null, IRunnableTaskCommands<ShortcutTask, ShortcutCommand>? shortcutCommands = null, ITaskCommands<TriggerTask, TriggerCommand>? triggerCommands = null)
    {
        var dependencies = CreateAuthorization(capabilityPolicy, pathPolicy, scheduleCommands, shortcutCommands, triggerCommands);
        return new McpTextExpansionTools(textExpansionCommandService ?? new TestTextExpansionCommandService(), dependencies.Authorization);
    }

    private static McpToolDependencies CreateAuthorization(IMcpCapabilityPolicy? capabilityPolicy, IMcpPathPolicy? pathPolicy, IRunnableTaskCommands<ScheduledTask, ScheduleCommand>? scheduleCommands, IRunnableTaskCommands<ShortcutTask, ShortcutCommand>? shortcutCommands, ITaskCommands<TriggerTask, TriggerCommand>? triggerCommands)
    {
        var schedule = scheduleCommands ?? new TestScheduleCommands();
        var shortcut = shortcutCommands ?? new TestShortcutCommands();
        var trigger = triggerCommands ?? new TestTriggerCommands();
        var authorizer = new McpPathAuthorizer(pathPolicy ?? new AllowAllMcpPathPolicy());
        return new McpToolDependencies(schedule, shortcut, trigger, authorizer, new McpToolAuthorization(capabilityPolicy ?? new AllowAllMcpCapabilityPolicy(), authorizer, schedule, shortcut, trigger, new AutomationTaskAuthorization()));
    }

    private sealed record McpToolDependencies(IRunnableTaskCommands<ScheduledTask, ScheduleCommand> Schedule, IRunnableTaskCommands<ShortcutTask, ShortcutCommand> Shortcut, ITaskCommands<TriggerTask, TriggerCommand> Trigger, McpPathAuthorizer PathAuthorizer, McpToolAuthorization Authorization);
}
