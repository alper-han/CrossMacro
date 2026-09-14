namespace CrossMacro.Mcp.Tests;

internal static class McpToolTestFactory
{
    internal static IDoctorService CreateDoctorService() => new TestDoctorService(new DoctorReport { Checks = [] });

    internal static IProfileManager CreateProfileManager() => new TestProfileManager(new ProfileInfo { Id = "work", Name = "Work" });

    internal static ISettingsCliService CreateSettingsCliService() => new SettingsCliService(new TestSettingsService(new AppSettings()));

    internal static IMacroExecutionService CreateMacroExecutionService() => new TestMacroExecutionService();

    internal static IClipboardCliService CreateClipboardCliService() => new TestClipboardCliService();

    internal static IWindowCliService CreateWindowCliService() => new TestWindowCliService();

    internal static IScreenCliService CreateScreenCliService() => new TestScreenCliService();

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
        IScheduleCliService? scheduleCliService = null,
        IShortcutCliService? shortcutCliService = null,
        ITriggerCliService? triggerCliService = null)
    {
        var dependencies = CreateAuthorization(capabilityPolicy, pathPolicy, scheduleCliService, shortcutCliService, triggerCliService);
        return new McpAutomationTools(macroExecutionService ?? CreateMacroExecutionService(), operationCoordinator ?? CreateOperationCoordinator(), runScriptExecutionService ?? CreateRunScriptExecutionService(), recordExecutionService ?? CreateRecordExecutionService(), cliPreflightService ?? CreatePreflightService(), dependencies.Authorization, dependencies.PathAuthorizer);
    }

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
        IScheduleCliService? scheduleCliService = null,
        IShortcutCliService? shortcutCliService = null,
        ITriggerCliService? triggerCliService = null)
    {
        var dependencies = CreateAuthorization(capabilityPolicy, pathPolicy, scheduleCliService, shortcutCliService, triggerCliService);
        return new McpCommandTools(macroExecutionService ?? CreateMacroExecutionService(), operationCoordinator ?? CreateOperationCoordinator(), runScriptExecutionService ?? CreateRunScriptExecutionService(), recordExecutionService ?? CreateRecordExecutionService(), cliPreflightService ?? CreatePreflightService(), cliCommandExecutor ?? CreateCliCommandExecutor(), profileOperations ?? new ProfileOperations(new ManageProfile(CreateProfileManager())), commandPolicy ?? new McpCommandPolicy(), dependencies.Authorization, dependencies.PathAuthorizer);
    }

    internal static McpScreenTools CreateScreenTools(
        IScreenCliService? screenCliService = null,
        IScreenshotCaptureService? screenshotCaptureService = null,
        IImageAssetCodec? imageAssetCodec = null,
        IMcpCapabilityPolicy? capabilityPolicy = null,
        IMcpPathPolicy? pathPolicy = null,
        IScheduleCliService? scheduleCliService = null,
        IShortcutCliService? shortcutCliService = null,
        ITriggerCliService? triggerCliService = null,
        IMousePositionProvider? mousePositionProvider = null)
    {
        var dependencies = CreateAuthorization(capabilityPolicy, pathPolicy, scheduleCliService, shortcutCliService, triggerCliService);
        return new McpScreenTools(screenshotCaptureService ?? CreateScreenshotCaptureService(), imageAssetCodec ?? CreateImageAssetCodec(), dependencies.Authorization, dependencies.PathAuthorizer, mousePositionProvider, new ScreenPortTestAdapter(screenCliService ?? CreateScreenCliService()));
    }

    internal static McpClipboardTools CreateClipboardTools(
        IClipboardCliService? clipboardCliService = null,
        IImageAssetCodec? imageAssetCodec = null,
        IImageClipboardReader? imageClipboardReader = null,
        IImageClipboardService? imageClipboardService = null,
        IMcpCapabilityPolicy? capabilityPolicy = null,
        IMcpPathPolicy? pathPolicy = null,
        IScheduleCliService? scheduleCliService = null,
        IShortcutCliService? shortcutCliService = null,
        ITriggerCliService? triggerCliService = null)
    {
        var dependencies = CreateAuthorization(capabilityPolicy, pathPolicy, scheduleCliService, shortcutCliService, triggerCliService);
        return new McpClipboardTools(clipboardCliService ?? CreateClipboardCliService(), imageAssetCodec ?? CreateImageAssetCodec(), imageClipboardReader ?? CreateImageClipboardReader(), imageClipboardService ?? CreateImageClipboardService(), dependencies.Authorization, dependencies.PathAuthorizer);
    }

    internal static McpMacroTools CreateMacroTools(IMacroExecutionService? macroExecutionService = null, IMcpCapabilityPolicy? capabilityPolicy = null, IMcpPathPolicy? pathPolicy = null, IScheduleCliService? scheduleCliService = null, IShortcutCliService? shortcutCliService = null, ITriggerCliService? triggerCliService = null)
    {
        var dependencies = CreateAuthorization(capabilityPolicy, pathPolicy, scheduleCliService, shortcutCliService, triggerCliService);
        return new McpMacroTools(macroExecutionService ?? CreateMacroExecutionService(), dependencies.Authorization, dependencies.PathAuthorizer);
    }

    internal static McpWindowTools CreateWindowTools(IWindowCliService? windowCliService = null, IMcpCapabilityPolicy? capabilityPolicy = null, IMcpPathPolicy? pathPolicy = null, IScheduleCliService? scheduleCliService = null, IShortcutCliService? shortcutCliService = null, ITriggerCliService? triggerCliService = null)
    {
        var dependencies = CreateAuthorization(capabilityPolicy, pathPolicy, scheduleCliService, shortcutCliService, triggerCliService);
        return new McpWindowTools(windowCliService ?? CreateWindowCliService(), dependencies.Authorization);
    }

    internal static McpTaskTools CreateTaskTools(IScheduleCliService? scheduleCliService = null, IShortcutCliService? shortcutCliService = null, ITriggerCliService? triggerCliService = null, IMcpCapabilityPolicy? capabilityPolicy = null, IMcpPathPolicy? pathPolicy = null)
    {
        var dependencies = CreateAuthorization(capabilityPolicy, pathPolicy, scheduleCliService, shortcutCliService, triggerCliService);
        return new McpTaskTools(new ScheduleCommandTestAdapter(dependencies.Schedule), new ShortcutCommandTestAdapter(dependencies.Shortcut), new TriggerCommandTestAdapter(dependencies.Trigger), dependencies.Authorization);
    }

    internal static McpRuntimeTools CreateRuntimeTools(IRuntimeContext? runtimeContext = null, IDoctorService? doctorService = null, IProfileManager? profileManager = null, IQuickSetupCliService? quickSetupCliService = null, IMcpOperationCoordinator? operationCoordinator = null, IMcpCapabilityPolicy? capabilityPolicy = null, IMcpPathPolicy? pathPolicy = null, IImageClipboardReader? imageClipboardReader = null, IImageClipboardService? imageClipboardService = null, ILinuxDaemonHandshakeProbe? daemonHandshakeProbe = null, ILinuxDaemonSocketAccessProbe? daemonSocketAccessProbe = null, IScheduleCliService? scheduleCliService = null, IShortcutCliService? shortcutCliService = null, ITriggerCliService? triggerCliService = null)
    {
        var selectedCapabilityPolicy = capabilityPolicy ?? new AllowAllMcpCapabilityPolicy();
        var dependencies = CreateAuthorization(selectedCapabilityPolicy, pathPolicy, scheduleCliService, shortcutCliService, triggerCliService);
        return new McpRuntimeTools(runtimeContext ?? new TestRuntimeContext(), doctorService ?? CreateDoctorService(), profileManager ?? CreateProfileManager(), quickSetupCliService ?? new TestQuickSetupCliService(), operationCoordinator ?? CreateOperationCoordinator(), selectedCapabilityPolicy, dependencies.Authorization, imageClipboardReader ?? CreateImageClipboardReader(), imageClipboardService ?? CreateImageClipboardService(), daemonHandshakeProbe, daemonSocketAccessProbe);
    }

    internal static McpSettingsTools CreateSettingsTools(ISettingsCliService? settingsCliService = null, IMcpCapabilityPolicy? capabilityPolicy = null, IMcpPathPolicy? pathPolicy = null, IScheduleCliService? scheduleCliService = null, IShortcutCliService? shortcutCliService = null, ITriggerCliService? triggerCliService = null)
    {
        var dependencies = CreateAuthorization(capabilityPolicy, pathPolicy, scheduleCliService, shortcutCliService, triggerCliService);
        return new McpSettingsTools(settingsCliService ?? CreateSettingsCliService(), dependencies.Authorization);
    }

    internal static McpProfileTools CreateProfileTools(IProfileOperations? profileOperations = null, IMcpCapabilityPolicy? capabilityPolicy = null, IMcpPathPolicy? pathPolicy = null, IScheduleCliService? scheduleCliService = null, IShortcutCliService? shortcutCliService = null, ITriggerCliService? triggerCliService = null)
    {
        var dependencies = CreateAuthorization(capabilityPolicy, pathPolicy, scheduleCliService, shortcutCliService, triggerCliService);
        return new McpProfileTools(profileOperations ?? new ProfileOperations(new ManageProfile(CreateProfileManager())), dependencies.Authorization);
    }

    internal static McpTextExpansionTools CreateTextExpansionTools(ITextExpansionCliService? textExpansionCliService = null, IMcpCapabilityPolicy? capabilityPolicy = null, IMcpPathPolicy? pathPolicy = null, IScheduleCliService? scheduleCliService = null, IShortcutCliService? shortcutCliService = null, ITriggerCliService? triggerCliService = null)
    {
        var dependencies = CreateAuthorization(capabilityPolicy, pathPolicy, scheduleCliService, shortcutCliService, triggerCliService);
        return new McpTextExpansionTools(textExpansionCliService ?? new TestTextExpansionCliService(), dependencies.Authorization);
    }

    private static McpToolDependencies CreateAuthorization(IMcpCapabilityPolicy? capabilityPolicy, IMcpPathPolicy? pathPolicy, IScheduleCliService? scheduleCliService, IShortcutCliService? shortcutCliService, ITriggerCliService? triggerCliService)
    {
        var schedule = scheduleCliService ?? new TestScheduleCliService();
        var shortcut = shortcutCliService ?? new TestShortcutCliService();
        var trigger = triggerCliService ?? new TestTriggerCliService();
        var authorizer = new McpPathAuthorizer(pathPolicy ?? new AllowAllMcpPathPolicy());
        return new McpToolDependencies(schedule, shortcut, trigger, authorizer, new McpToolAuthorization(capabilityPolicy ?? new AllowAllMcpCapabilityPolicy(), authorizer, new ScheduleCommandTestAdapter(schedule), new ShortcutCommandTestAdapter(shortcut), new TriggerCommandTestAdapter(trigger), new AutomationTaskAuthorization()));
    }

    private sealed record McpToolDependencies(IScheduleCliService Schedule, IShortcutCliService Shortcut, ITriggerCliService Trigger, McpPathAuthorizer PathAuthorizer, McpToolAuthorization Authorization);
}
