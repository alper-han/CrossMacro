namespace CrossMacro.Mcp.Tests;

internal static class McpToolTestFactory
{
    internal static IDoctorService CreateDoctorService() => new TestDoctorService(new DoctorReport { Checks = [] });

    internal static IProfileManager CreateProfileManager() => new TestProfileManager(new ProfileInfo { Id = "work", Name = "Work" });

    internal static ISettingsCliService CreateSettingsCliService() => CreateSettingsCliService(new AppSettings());

    internal static SettingsCliService CreateSettingsCliService(AppSettings settings)
    {
        var settingsService = new TestSettingsService(settings);
        return new SettingsCliService(settingsService, new CrossMacro.Application.Settings.SettingsChangeCoordinator(settingsService));
    }

    internal static IMacroExecutionService CreateMacroExecutionService() => new TestMacroExecutionService();

    internal static IClipboardCliService CreateClipboardCliService() => new TestClipboardCliService();

    internal static IWindowCliService CreateWindowCliService() => new TestWindowCliService();

    internal static IScreenshotCaptureService CreateScreenshotCaptureService() => new TestScreenshotCaptureService();

    internal static IImageAssetCodec CreateImageAssetCodec() => new TestImageAssetCodec();

    internal static IImageClipboardReader CreateImageClipboardReader() => new TestImageClipboardReader { IsSupported = false };

    internal static IImageClipboardService CreateImageClipboardService() => new TestImageClipboardService();

    internal static IMcpOperationCoordinator CreateOperationCoordinator() => new McpOperationCoordinator();

    internal static IRunScriptExecutionService CreateRunScriptExecutionService() => new TestRunScriptExecutionService();

    internal static IRecordExecutionService CreateRecordExecutionService() => new TestRecordExecutionService();

    internal static ICliPreflightService CreatePreflightService() => new TestCliPreflightService();

    internal static CliCommandExecutor CreateCliCommandExecutor(ICliCommandHandlerResolver? resolver = null) =>
        new(resolver ?? new TestCliCommandHandlerResolver());

    internal static McpAutomationTools CreateAutomationTools(
        IMacroExecutionService? macroExecutionService = null,
        IMcpOperationCoordinator? operationCoordinator = null,
        IRunScriptExecutionService? runScriptExecutionService = null,
        IRecordExecutionService? recordExecutionService = null,
        ICliPreflightService? cliPreflightService = null,
        IMcpCapabilityPolicy? capabilityPolicy = null,
        IMcpPathPolicy? pathPolicy = null,
        IRunnableTaskCommands<ScheduledTask, ScheduleCommand>? scheduleCommands = null,
        IRunnableTaskCommands<ShortcutTask, ShortcutCommand>? shortcutCommands = null,
        ITaskCommands<TriggerTask, TriggerCommand>? triggerCommands = null)
    {
        var dependencies = CreateAuthorization(capabilityPolicy, pathPolicy, scheduleCommands, shortcutCommands, triggerCommands);
        var coordinator = operationCoordinator ?? CreateOperationCoordinator();
        return new McpAutomationTools(CreateExecution(macroExecutionService, runScriptExecutionService, recordExecutionService, coordinator), coordinator, cliPreflightService ?? CreatePreflightService(), dependencies.Authorization, dependencies.PathAuthorizer);
    }

    internal static McpAutomationExecution CreateExecution(
        IMacroExecutionService? macroExecutionService = null,
        IRunScriptExecutionService? runScriptExecutionService = null,
        IRecordExecutionService? recordExecutionService = null,
        IMcpOperationCoordinator? operationCoordinator = null) =>
        new(macroExecutionService ?? CreateMacroExecutionService(), runScriptExecutionService ?? CreateRunScriptExecutionService(), recordExecutionService ?? CreateRecordExecutionService(), operationCoordinator ?? CreateOperationCoordinator());

    internal static McpCommandTools CreateCommandTools(
        IMacroExecutionService? macroExecutionService = null,
        IMcpOperationCoordinator? operationCoordinator = null,
        IRunScriptExecutionService? runScriptExecutionService = null,
        IRecordExecutionService? recordExecutionService = null,
        ICliPreflightService? cliPreflightService = null,
        CliCommandExecutor? cliCommandExecutor = null,
        IProfileOperations? profileOperations = null,
        IMcpCommandPolicy? commandPolicy = null,
        IMcpCapabilityPolicy? capabilityPolicy = null,
        IMcpPathPolicy? pathPolicy = null,
        IRunnableTaskCommands<ScheduledTask, ScheduleCommand>? scheduleCommands = null,
        IRunnableTaskCommands<ShortcutTask, ShortcutCommand>? shortcutCommands = null,
        ITaskCommands<TriggerTask, TriggerCommand>? triggerCommands = null)
    {
        var dependencies = CreateAuthorization(capabilityPolicy, pathPolicy, scheduleCommands, shortcutCommands, triggerCommands);
        var execution = CreateExecution(macroExecutionService, runScriptExecutionService, recordExecutionService, operationCoordinator ?? CreateOperationCoordinator());
        return new McpCommandTools(execution, cliPreflightService ?? CreatePreflightService(), cliCommandExecutor ?? CreateCliCommandExecutor(), profileOperations ?? new ProfileOperations(new ManageProfile(CreateProfileManager())), commandPolicy ?? new McpCommandPolicy(), dependencies.Authorization, dependencies.PathAuthorizer);
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
        IClipboardCliService? clipboardCliService = null,
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
        return new McpClipboardTools(clipboardCliService ?? CreateClipboardCliService(), imageAssetCodec ?? CreateImageAssetCodec(), imageClipboardReader ?? CreateImageClipboardReader(), imageClipboardService ?? CreateImageClipboardService(), dependencies.Authorization, dependencies.PathAuthorizer);
    }

    internal static McpMacroTools CreateMacroTools(IMacroExecutionService? macroExecutionService = null, IMcpCapabilityPolicy? capabilityPolicy = null, IMcpPathPolicy? pathPolicy = null, IRunnableTaskCommands<ScheduledTask, ScheduleCommand>? scheduleCommands = null, IRunnableTaskCommands<ShortcutTask, ShortcutCommand>? shortcutCommands = null, ITaskCommands<TriggerTask, TriggerCommand>? triggerCommands = null)
    {
        var dependencies = CreateAuthorization(capabilityPolicy, pathPolicy, scheduleCommands, shortcutCommands, triggerCommands);
        return new McpMacroTools(macroExecutionService ?? CreateMacroExecutionService(), dependencies.Authorization, dependencies.PathAuthorizer);
    }

    internal static McpWindowTools CreateWindowTools(IWindowCliService? windowCliService = null, IMcpCapabilityPolicy? capabilityPolicy = null, IMcpPathPolicy? pathPolicy = null, IRunnableTaskCommands<ScheduledTask, ScheduleCommand>? scheduleCommands = null, IRunnableTaskCommands<ShortcutTask, ShortcutCommand>? shortcutCommands = null, ITaskCommands<TriggerTask, TriggerCommand>? triggerCommands = null)
    {
        var dependencies = CreateAuthorization(capabilityPolicy, pathPolicy, scheduleCommands, shortcutCommands, triggerCommands);
        return new McpWindowTools(windowCliService ?? CreateWindowCliService(), dependencies.Authorization);
    }

    internal static McpTaskTools CreateTaskTools(IRunnableTaskCommands<ScheduledTask, ScheduleCommand>? scheduleCommands = null, IRunnableTaskCommands<ShortcutTask, ShortcutCommand>? shortcutCommands = null, ITaskCommands<TriggerTask, TriggerCommand>? triggerCommands = null, IMcpCapabilityPolicy? capabilityPolicy = null, IMcpPathPolicy? pathPolicy = null)
    {
        var dependencies = CreateAuthorization(capabilityPolicy, pathPolicy, scheduleCommands, shortcutCommands, triggerCommands);
        return new McpTaskTools(dependencies.Schedule, dependencies.Shortcut, dependencies.Trigger, dependencies.Authorization);
    }

    internal static McpRuntimeTools CreateRuntimeTools(IRuntimeContext? runtimeContext = null, IDoctorService? doctorService = null, IProfileManager? profileManager = null, IQuickSetupCliService? quickSetupCliService = null, IMcpOperationCoordinator? operationCoordinator = null, IMcpCapabilityPolicy? capabilityPolicy = null, IMcpPathPolicy? pathPolicy = null, IImageClipboardReader? imageClipboardReader = null, IImageClipboardService? imageClipboardService = null, ILinuxDaemonHandshakeProbe? daemonHandshakeProbe = null, ILinuxDaemonSocketAccessProbe? daemonSocketAccessProbe = null, IRunnableTaskCommands<ScheduledTask, ScheduleCommand>? scheduleCommands = null, IRunnableTaskCommands<ShortcutTask, ShortcutCommand>? shortcutCommands = null, ITaskCommands<TriggerTask, TriggerCommand>? triggerCommands = null)
    {
        var selectedCapabilityPolicy = capabilityPolicy ?? new AllowAllMcpCapabilityPolicy();
        var selectedPathPolicy = pathPolicy ?? new AllowAllMcpPathPolicy();
        var dependencies = CreateAuthorization(selectedCapabilityPolicy, selectedPathPolicy, scheduleCommands, shortcutCommands, triggerCommands);
        return new McpRuntimeTools(runtimeContext ?? new TestRuntimeContext(), doctorService ?? CreateDoctorService(), profileManager ?? CreateProfileManager(), quickSetupCliService ?? new TestQuickSetupCliService(), operationCoordinator ?? CreateOperationCoordinator(), selectedCapabilityPolicy, dependencies.Authorization, selectedPathPolicy, imageClipboardReader ?? CreateImageClipboardReader(), imageClipboardService ?? CreateImageClipboardService(), daemonHandshakeProbe, daemonSocketAccessProbe);
    }

    internal static McpSettingsTools CreateSettingsTools(ISettingsCliService? settingsCliService = null, IMcpCapabilityPolicy? capabilityPolicy = null, IMcpPathPolicy? pathPolicy = null, IRunnableTaskCommands<ScheduledTask, ScheduleCommand>? scheduleCommands = null, IRunnableTaskCommands<ShortcutTask, ShortcutCommand>? shortcutCommands = null, ITaskCommands<TriggerTask, TriggerCommand>? triggerCommands = null)
    {
        var dependencies = CreateAuthorization(capabilityPolicy, pathPolicy, scheduleCommands, shortcutCommands, triggerCommands);
        return new McpSettingsTools(settingsCliService ?? CreateSettingsCliService(), dependencies.Authorization);
    }

    internal static McpProfileTools CreateProfileTools(IProfileOperations? profileOperations = null, IMcpCapabilityPolicy? capabilityPolicy = null, IMcpPathPolicy? pathPolicy = null, IRunnableTaskCommands<ScheduledTask, ScheduleCommand>? scheduleCommands = null, IRunnableTaskCommands<ShortcutTask, ShortcutCommand>? shortcutCommands = null, ITaskCommands<TriggerTask, TriggerCommand>? triggerCommands = null)
    {
        var dependencies = CreateAuthorization(capabilityPolicy, pathPolicy, scheduleCommands, shortcutCommands, triggerCommands);
        return new McpProfileTools(profileOperations ?? new ProfileOperations(new ManageProfile(CreateProfileManager())), dependencies.Authorization);
    }

    internal static McpTextExpansionTools CreateTextExpansionTools(ITextExpansionCliService? textExpansionCliService = null, IMcpCapabilityPolicy? capabilityPolicy = null, IMcpPathPolicy? pathPolicy = null, IRunnableTaskCommands<ScheduledTask, ScheduleCommand>? scheduleCommands = null, IRunnableTaskCommands<ShortcutTask, ShortcutCommand>? shortcutCommands = null, ITaskCommands<TriggerTask, TriggerCommand>? triggerCommands = null)
    {
        var dependencies = CreateAuthorization(capabilityPolicy, pathPolicy, scheduleCommands, shortcutCommands, triggerCommands);
        return new McpTextExpansionTools(textExpansionCliService ?? new TestTextExpansionCliService(), dependencies.Authorization);
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
