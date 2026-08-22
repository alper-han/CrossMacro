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
        IMcpCommandPolicy? commandPolicy = null,
        IMcpCapabilityPolicy? capabilityPolicy = null,
        IMcpPathPolicy? pathPolicy = null,
        IScheduleCliService? scheduleCliService = null,
        IShortcutCliService? shortcutCliService = null,
        ITriggerCliService? triggerCliService = null)
    {
        var dependencies = CreateAuthorization(capabilityPolicy, pathPolicy, scheduleCliService, shortcutCliService, triggerCliService);
        return new McpCommandTools(macroExecutionService ?? CreateMacroExecutionService(), operationCoordinator ?? CreateOperationCoordinator(), runScriptExecutionService ?? CreateRunScriptExecutionService(), recordExecutionService ?? CreateRecordExecutionService(), cliPreflightService ?? CreatePreflightService(), cliCommandExecutor ?? CreateCliCommandExecutor(), commandPolicy ?? new McpCommandPolicy(), dependencies.Authorization, dependencies.PathAuthorizer);
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
        return new McpScreenTools(screenCliService ?? CreateScreenCliService(), screenshotCaptureService ?? CreateScreenshotCaptureService(), imageAssetCodec ?? CreateImageAssetCodec(), dependencies.Authorization, dependencies.PathAuthorizer, mousePositionProvider);
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
        return new McpTaskTools(dependencies.Schedule, dependencies.Shortcut, dependencies.Trigger, dependencies.Authorization);
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

    internal static McpProfileTools CreateProfileTools(IProfileCliService? profileCliService = null, IMcpCapabilityPolicy? capabilityPolicy = null, IMcpPathPolicy? pathPolicy = null, IScheduleCliService? scheduleCliService = null, IShortcutCliService? shortcutCliService = null, ITriggerCliService? triggerCliService = null)
    {
        var dependencies = CreateAuthorization(capabilityPolicy, pathPolicy, scheduleCliService, shortcutCliService, triggerCliService);
        return new McpProfileTools(profileCliService ?? new ProfileCliService(CreateProfileManager()), dependencies.Authorization);
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
        return new McpToolDependencies(schedule, shortcut, trigger, authorizer, new McpToolAuthorization(capabilityPolicy ?? new AllowAllMcpCapabilityPolicy(), authorizer, schedule, shortcut, trigger));
    }

    private sealed record McpToolDependencies(IScheduleCliService Schedule, IShortcutCliService Shortcut, ITriggerCliService Trigger, McpPathAuthorizer PathAuthorizer, McpToolAuthorization Authorization);
}

internal static class McpTestData
{
    internal static string GetPhysicalTemporaryRoot() =>
        OperatingSystem.IsMacOS() ? "/private/tmp" : Path.GetTempPath();

    internal static string CreateTemporaryDirectory()
    {
        var directory = Path.Combine(GetPhysicalTemporaryRoot(), $"crossmacro-mcp-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(directory);
        return directory;
    }

    internal static string CreateTemporaryMacroFile()
    {
        var path = Path.Combine(GetPhysicalTemporaryRoot(), $"crossmacro-mcp-{Guid.NewGuid():N}.macro");
        File.WriteAllText(path, "macro");
        return path;
    }

    internal static string CreateTemporaryPngFile()
    {
        var path = Path.Combine(GetPhysicalTemporaryRoot(), $"crossmacro-mcp-{Guid.NewGuid():N}.png");
        File.WriteAllBytes(path, [137, 80, 78, 71]);
        return path;
    }

    internal static byte[] CreatePngBytes() => [137, 80, 78, 71, 13, 10, 26, 10];

    internal static async Task<JsonElement> WaitForAutomationCompletionAsync(
        McpAutomationTools tools,
        string operationId,
        int maximumAttempts = 100)
    {
        for (var attempt = 0; attempt < maximumAttempts; attempt++)
        {
            var result = tools.GetAutomation(operationId);
            var structured = Assert.IsType<JsonElement>(result.StructuredContent);
            var operation = structured.GetProperty("operation");
            if (operation.GetProperty("state").GetString() is not "running")
            {
                return structured;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(10), TimeProvider.System, CancellationToken.None).ConfigureAwait(false);
        }

        throw new TimeoutException("Automation operation did not complete.");
    }

    internal static ScreenFrame CreateImageFrame() => new(
        new ScreenRect(0, 0, 2, 1),
        stride: 6,
        ScreenPixelFormat.Rgb24,
        new byte[] { 0, 0, 0, 0, 0, 0 });

    internal static WindowInfoData CreateWindow(int index)
    {
        return new WindowInfoData(
            Address: $"0x{index.ToString("x", System.Globalization.CultureInfo.InvariantCulture)}",
            Title: $"Window {index.ToString(System.Globalization.CultureInfo.InvariantCulture)}",
            Class: "TestApp",
            Pid: index,
            Workspace: "workspace",
            IsFocused: index is 0,
            IsFullscreen: false,
            IsMaximized: false,
            IsFloating: false,
            IsPinned: false,
            IsHidden: false,
            X: index,
            Y: index,
            Width: 800,
            Height: 600);
    }
}

internal sealed class TestRuntimeContext : IRuntimeContext
    {
        public bool IsLinux => true;
        public bool IsWindows => false;
        public bool IsMacOS => false;
        public bool IsFlatpak => true;
        public string? SessionType => "wayland";
}

internal sealed class AllowAllMcpPathPolicy : IMcpPathPolicy
{
        public bool TryAuthorize(
            string path,
            McpPathKind kind,
            bool requireExisting,
            out string normalizedPath,
            out McpToolOutcome failure)
        {
            normalizedPath = Path.GetFullPath(path);
            failure = McpToolOutcomeMapper.Success(string.Empty);
            return true;
        }
    }

internal sealed class TestMousePositionProvider : IMousePositionProvider
    {
        public string ProviderName => "test-cursor";

        public bool IsSupported => true;

        public bool SupportsAbsolutePosition => true;

        public (int X, int Y)? Position { get; init; }

        public Task<(int X, int Y)?> GetAbsolutePositionAsync() => Task.FromResult(Position);

        public Task<(int Width, int Height)?> GetScreenResolutionAsync() => Task.FromResult<(int Width, int Height)?>((1920, 1080));

        public void Dispose()
        {
        }
    }

internal sealed class AllowAllMcpCapabilityPolicy : IMcpCapabilityPolicy
    {
        public bool IsRestricted => false;

        public bool IsAllowed(McpCapability capability) => true;

        public bool IsAnyAllowed(params McpCapability[] capabilities) => true;

        public McpToolOutcome Require(McpCapability capability) => McpToolOutcomeMapper.Success(string.Empty);

        public void SetRestricted(bool restricted)
        {
        }
    }

internal sealed class TestSettingsService(AppSettings settings) : ISettingsService
    {
        public AppSettings Current { get; } = settings;

        public AppSettings Load() => Current;

        public Task<AppSettings> LoadAsync() => Task.FromResult(Current);

        public void Save()
        {
        }

        public Task SaveAsync() => Task.CompletedTask;
    }

internal sealed class TestProfileCliService : IProfileCliService
    {
        public CliCommandExecutionResult? ListResult { get; init; }

        public int ListCallCount { get; private set; }

        public Task<CliCommandExecutionResult> ListAsync(CancellationToken cancellationToken)
        {
            ListCallCount++;
            return Task.FromResult(ListResult ?? CliCommandExecutionResult.Ok("0 profile(s)."));
        }

        public Task<CliCommandExecutionResult> CurrentAsync(CancellationToken cancellationToken) => Task.FromResult(CliCommandExecutionResult.Ok("Current profile."));
        public Task<CliCommandExecutionResult> CreateAsync(string name, CancellationToken cancellationToken) => Task.FromResult(CliCommandExecutionResult.Ok("Profile created."));
        public Task<CliCommandExecutionResult> SwitchAsync(string profileIdentifier, CancellationToken cancellationToken) => Task.FromResult(CliCommandExecutionResult.Ok("Profile switched."));
        public Task<CliCommandExecutionResult> RenameAsync(string profileIdentifier, string newName, CancellationToken cancellationToken) => Task.FromResult(CliCommandExecutionResult.Ok("Profile renamed."));
        public Task<CliCommandExecutionResult> DeleteAsync(string profileIdentifier, bool force, CancellationToken cancellationToken) => Task.FromResult(CliCommandExecutionResult.Ok("Profile deleted."));
    }

internal sealed class TestTextExpansionCliService : ITextExpansionCliService
    {
        public CliCommandExecutionResult? ListResult { get; init; }
        public string? LastTrigger { get; private set; }
        public string? LastReplacement { get; private set; }
        public PasteMethod LastMethod { get; private set; }
        public TextInsertionMode LastInsertionMode { get; private set; }
        public DirectTypingMethod LastDirectTypingMethod { get; private set; }

        public Task<CliCommandExecutionResult> ListAsync(string? profileIdentifier, CancellationToken cancellationToken) =>
            Task.FromResult(ListResult ?? CliCommandExecutionResult.Ok("0 text expansion(s).", new TextExpansionListData([], profileIdentifier ?? string.Empty, 0)));

        public Task<CliCommandExecutionResult> AddAsync(string trigger, string replacement, PasteMethod method, TextInsertionMode insertionMode, DirectTypingMethod directTypingMethod, string? profileIdentifier, CancellationToken cancellationToken)
        {
            LastTrigger = trigger;
            LastReplacement = replacement;
            LastMethod = method;
            LastInsertionMode = insertionMode;
            LastDirectTypingMethod = directTypingMethod;
            return Task.FromResult(CliCommandExecutionResult.Ok("Text expansion added.", new TextExpansionData(trigger, replacement, true, method.ToString(), insertionMode.ToString(), directTypingMethod.ToString())));
        }

        public Task<CliCommandExecutionResult> RemoveAsync(string trigger, string? profileIdentifier, CancellationToken cancellationToken) => Task.FromResult(CliCommandExecutionResult.Ok("Text expansion removed."));
        public Task<CliCommandExecutionResult> EnableAsync(string trigger, string? profileIdentifier, CancellationToken cancellationToken) => Task.FromResult(CliCommandExecutionResult.Ok("Text expansion enabled."));
        public Task<CliCommandExecutionResult> DisableAsync(string trigger, string? profileIdentifier, CancellationToken cancellationToken) => Task.FromResult(CliCommandExecutionResult.Ok("Text expansion disabled."));
        public Task<CliCommandExecutionResult> TestAsync(string trigger, string? profileIdentifier, CancellationToken cancellationToken) => Task.FromResult(CliCommandExecutionResult.Ok("Text expansion tested.", new TextExpansionTestData(true, new TextExpansionData(trigger, "replacement", true, "CtrlV", "Paste", "FastBatch"))));
    }

internal sealed class TestScheduleCliService : IScheduleCliService
    {
        public CliCommandExecutionResult ListResult { get; init; } = CliCommandExecutionResult.Ok("Loaded 0 schedule task(s).", new TaskListData<ScheduleTaskData>(0, []));
        public CliCommandExecutionResult ExecuteResult { get; init; } = CliCommandExecutionResult.Ok("Schedule task updated.");
        public int RunCallCount { get; private set; }

        public Task<CliCommandExecutionResult> ListAsync(CancellationToken cancellationToken) => Task.FromResult(ListResult);
        public Task<CliCommandExecutionResult> RunAsync(string taskId, CancellationToken cancellationToken)
        {
            RunCallCount++;
            return Task.FromResult(ExecuteResult);
        }
        public Task<CliCommandExecutionResult> ExecuteAsync(ScheduleCliOptions options, CancellationToken cancellationToken) => Task.FromResult(ExecuteResult);
    }

internal sealed class TestShortcutCliService : IShortcutCliService
    {
        public CliCommandExecutionResult ListResult { get; init; } = CliCommandExecutionResult.Ok("Loaded 0 shortcut task(s).", new TaskListData<ShortcutTaskData>(0, []));
        public CliCommandExecutionResult ExecuteResult { get; init; } = CliCommandExecutionResult.Ok("Shortcut task updated.");

        public Task<CliCommandExecutionResult> ListAsync(CancellationToken cancellationToken) => Task.FromResult(ListResult);
        public Task<CliCommandExecutionResult> RunAsync(string taskId, CancellationToken cancellationToken) => Task.FromResult(ExecuteResult);
        public Task<CliCommandExecutionResult> ExecuteAsync(ShortcutCliOptions options, CancellationToken cancellationToken) => Task.FromResult(ExecuteResult);
    }

internal sealed class TestTriggerCliService : ITriggerCliService
    {
        public CliCommandExecutionResult ListResult { get; init; } = CliCommandExecutionResult.Ok("Loaded 0 trigger task(s).", new TaskListData<TriggerTaskData>(0, []));
        public CliCommandExecutionResult ExecuteResult { get; init; } = CliCommandExecutionResult.Ok("Trigger task updated.");
        public int ExecuteCallCount { get; private set; }

        public Task<CliCommandExecutionResult> ListAsync(CancellationToken cancellationToken) => Task.FromResult(ListResult);
        public Task<CliCommandExecutionResult> ExecuteAsync(TriggerCliOptions options, CancellationToken cancellationToken)
        {
            ExecuteCallCount++;
            return Task.FromResult(ExecuteResult);
        }
    }

internal sealed class TestQuickSetupCliService : IQuickSetupCliService
    {
        public QuickSetupStatus Status { get; init; } = new(true, "flatpak", false);
        public QuickSetupResult Result { get; init; } = new(true, "Quick setup completed.");

        public QuickSetupStatus GetStatus() => Status;
        public Task<QuickSetupCliResult> RunAsync(CancellationToken cancellationToken) => Task.FromResult(new QuickSetupCliResult(Status.Applicable, Status.Provider, Result));
    }

internal sealed class TestDoctorService(DoctorReport report) : IDoctorService
{
        private readonly DoctorReport _report = report;

        public bool WasRun { get; private set; }

        public Task<DoctorReport> RunAsync(bool verbose, CancellationToken cancellationToken)
        {
            Assert.False(verbose);
            cancellationToken.ThrowIfCancellationRequested();
            WasRun = true;
            return Task.FromResult(_report);
        }
    }

internal sealed class TestMacroExecutionService : IMacroExecutionService
    {
        public MacroExecutionResult? InfoResult { get; init; }

        public MacroExecutionResult? ValidationResult { get; init; }

        public int GetInfoCallCount { get; private set; }

        public string? LastMacroPath { get; private set; }

        public MacroExecutionResult? ExecutionResult { get; init; }

        public Func<MacroExecutionRequest, CancellationToken, Task<MacroExecutionResult>>? ExecutionHandler { get; init; }

        public MacroExecutionRequest? LastExecutionRequest { get; private set; }

        public Task<MacroExecutionResult> ValidateAsync(string macroFilePath, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastMacroPath = macroFilePath;
            return Task.FromResult(ValidationResult ?? throw new InvalidOperationException("Validation result was not configured."));
        }

        public Task<MacroExecutionResult> GetInfoAsync(string macroFilePath, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            GetInfoCallCount++;
            LastMacroPath = macroFilePath;
            return Task.FromResult(InfoResult ?? throw new InvalidOperationException("Info result was not configured."));
        }

        public Task<MacroExecutionResult> ExecuteAsync(MacroExecutionRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastExecutionRequest = request;
            return ExecutionHandler is { } handler
                ? handler(request, cancellationToken)
                : Task.FromResult(ExecutionResult ?? throw new InvalidOperationException("Execution result was not configured."));
        }
    }

internal sealed class WaitingMacroExecutionService : IMacroExecutionService
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Cancelled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<MacroExecutionResult> ValidateAsync(string macroFilePath, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<MacroExecutionResult> GetInfoAsync(string macroFilePath, CancellationToken cancellationToken) => throw new NotSupportedException();

        public async Task<MacroExecutionResult> ExecuteAsync(MacroExecutionRequest request, CancellationToken cancellationToken)
        {
            Started.SetResult();
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, TimeProvider.System, cancellationToken).ConfigureAwait(false);
                throw new InvalidOperationException("The operation should have been cancelled.");
            }
            catch (OperationCanceledException)
            {
                Cancelled.SetResult();
                throw;
            }
        }
    }

internal sealed class TestRunScriptExecutionService : IRunScriptExecutionService
    {
        public MacroExecutionResult? Result { get; init; }

        public RunCliExecutionRequest? LastRequest { get; private set; }

        public Task<MacroExecutionResult> ExecuteAsync(RunCliExecutionRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastRequest = request;
            return Task.FromResult(Result ?? throw new InvalidOperationException("Run result was not configured."));
        }
    }

internal sealed class TestRecordExecutionService : IRecordExecutionService
    {
        public RecordExecutionResult? Result { get; init; }

        public RecordExecutionRequest? LastRequest { get; private set; }

        public Task<RecordExecutionResult> ExecuteAsync(RecordExecutionRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastRequest = request;
            return Task.FromResult(Result ?? throw new InvalidOperationException("Record result was not configured."));
        }
    }

internal sealed class TestCliPreflightService : ICliPreflightService
    {
        public CliPreflightResult Result { get; init; } = CliPreflightResult.Ok();

        public List<CliPreflightTarget> Targets { get; } = [];

        public Task<CliPreflightResult> CheckAsync(CliPreflightTarget target, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Targets.Add(target);
            return Task.FromResult(Result);
        }
    }

internal sealed class TestClipboardCliService : IClipboardCliService
    {
        public CliCommandExecutionResult? GetResult { get; init; }

        public CliCommandExecutionResult? SetResult { get; init; }

        public int GetCallCount { get; private set; }

        public int SetCallCount { get; private set; }

        public string? LastSetText { get; private set; }

        public Task<CliCommandExecutionResult> GetAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            GetCallCount++;
            return Task.FromResult(GetResult ?? throw new InvalidOperationException("Get result was not configured."));
        }

        public Task<CliCommandExecutionResult> SetTextAsync(string text, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SetCallCount++;
            LastSetText = text;
            return Task.FromResult(SetResult ?? throw new InvalidOperationException("Set result was not configured."));
        }

        public Task<CliCommandExecutionResult> SetFileAsync(string filePath, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<CliCommandExecutionResult> ClearAsync(CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }

internal sealed class TestWindowCliService : IWindowCliService
    {
        public CliCommandExecutionResult? Result { get; init; }

        public int CallCount { get; private set; }

        public WindowCliOptions? LastOptions { get; private set; }

        public Task<CliCommandExecutionResult> ExecuteAsync(WindowCliOptions options, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            LastOptions = options;
            return Task.FromResult(Result ?? throw new InvalidOperationException("Window result was not configured."));
        }
    }

internal sealed class TestCliCommandHandlerResolver(ICliCommandHandler? handler = null) : ICliCommandHandlerResolver
    {
        private readonly ICliCommandHandler? _handler = handler;

        public int ResolveCallCount { get; private set; }

        public ICliCommandHandler? Resolve(CliCommandOptions options)
        {
            ResolveCallCount++;
            return _handler;
        }
    }

internal sealed class TestCliCommandHandler<TOptions>(CliCommandExecutionResult result) : CliCommandHandlerBase<TOptions>
        where TOptions : CliCommandOptions
    {
        private readonly CliCommandExecutionResult _result = result;

        public TOptions? LastOptions { get; private set; }

        protected override Task<CliCommandExecutionResult> ExecuteAsync(TOptions options, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastOptions = options;
            return Task.FromResult(_result);
        }
    }

internal sealed class ThrowingCliCommandHandler(string detail) : ICliCommandHandler
    {
        private readonly string _detail = detail;

        public bool CanHandle(CliCommandOptions options) => options is DoctorCliOptions;

        public Task<CliCommandExecutionResult> ExecuteAsync(CliCommandOptions options, CancellationToken cancellationToken) =>
            Task.FromException<CliCommandExecutionResult>(new InvalidOperationException(_detail));
    }

internal sealed class RecordingCliCommandHandler : ICliCommandHandler
    {
        public CliCommandOptions? LastOptions { get; private set; }

        public bool CanHandle(CliCommandOptions options) => true;

        public Task<CliCommandExecutionResult> ExecuteAsync(CliCommandOptions options, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastOptions = options;
            return Task.FromResult(CliCommandExecutionResult.Ok("Compatibility command completed."));
        }
    }

internal sealed class TestScreenCliService : IScreenCliService
    {
        public CliCommandExecutionResult? Result { get; init; }

        public int CallCount { get; private set; }

        public ScreenCliOptions? LastOptions { get; private set; }

        public Task<CliCommandExecutionResult> ExecuteAsync(ScreenCliOptions options, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            LastOptions = options;
            return Task.FromResult(Result ?? throw new InvalidOperationException("Screen result was not configured."));
        }
    }

internal sealed class TestScreenshotCaptureService : IScreenshotCaptureService
    {
        public ScreenshotPngCaptureResult? Result { get; init; }

        public int CallCount { get; private set; }

        public ScreenshotPngCaptureRequest? LastRequest { get; private set; }

        public Task<ScreenshotPngCaptureResult> CapturePngAsync(ScreenshotPngCaptureRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            LastRequest = request;
            return Task.FromResult(Result ?? throw new InvalidOperationException("Screenshot result was not configured."));
        }

        public Task<ScreenshotCaptureResult> CaptureAsync(
            string? outputPath,
            bool copyToClipboard,
            ScreenRect? region,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }

internal enum TestImageAssetFailure
    {
        None,
        Validation,
        File,
    }

internal sealed class TestImageAssetCodec : IImageAssetCodec
    {
        public byte[]? PngBytes { get; init; }

        public ScreenFrame? Frame { get; init; }

        public TestImageAssetFailure Failure { get; init; }

        public int ReadCallCount { get; private set; }

        public Task<byte[]> ReadFileAsync(string filePath, string? assetName = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ReadCallCount++;
            return Failure switch
            {
                TestImageAssetFailure.Validation => Task.FromException<byte[]>(new InvalidDataException("invalid png")),
                TestImageAssetFailure.File => Task.FromException<byte[]>(new IOException("file read failed")),
                TestImageAssetFailure.None => Task.FromResult(PngBytes ?? throw new InvalidOperationException("PNG bytes were not configured.")),
                _ => throw new ArgumentException("Image asset failure is invalid.", nameof(filePath)),
            };
        }

        public Task<ScreenFrame> DecodeFileAsync(string filePath, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public ScreenFrame DecodePng(ReadOnlySpan<byte> pngBytes, string? assetName = null) =>
            Frame ?? throw new InvalidOperationException("Image frame was not configured.");

        public Task<ScreenFrame> DecodePngAsync(ReadOnlyMemory<byte> pngBytes, string? assetName = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Frame ?? throw new InvalidOperationException("Image frame was not configured."));
        }

        public ScreenFrame DecodeBase64Png(string encoded, string? assetName = null) => throw new NotSupportedException();

        public Task<ScreenFrame> DecodeBase64PngAsync(string encoded, string? assetName = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public void ValidateBase64Png(string encoded, string? assetName = null) => throw new NotSupportedException();

        public Task ValidateBase64PngAsync(string encoded, string? assetName = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public void ValidateMacroBudget(long totalEncodedBytes) => throw new NotSupportedException();

        public void EncodePng(ScreenFrame frame, Stream output) => throw new NotSupportedException();

        public Task EncodePngAsync(ScreenFrame frame, Stream output, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

internal sealed class TestImageClipboardReader : IImageClipboardReader
    {
        public bool IsSupported { get; init; } = true;

        public byte[]? PngBytes { get; init; }

        public Exception? Exception { get; init; }

        public int CallCount { get; private set; }

        public int? LastMaximumBytes { get; private set; }

        public Task<byte[]?> GetPngAsync(int maximumBytes, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            LastMaximumBytes = maximumBytes;
            return Exception is null
                ? Task.FromResult(PngBytes)
                : Task.FromException<byte[]?>(Exception);
        }
    }

internal sealed class TestImageClipboardService : IImageClipboardService
    {
        public bool IsSupported { get; init; } = true;

        public int SetCallCount { get; private set; }

        public byte[]? LastPngBytes { get; private set; }

        public Task SetPngAsync(ReadOnlyMemory<byte> pngBytes, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SetCallCount++;
            LastPngBytes = pngBytes.ToArray();
            return Task.CompletedTask;
        }
    }

internal sealed class TestProfileManager(ProfileInfo activeProfile) : IProfileManager
    {
        public ProfileInfo ActiveProfile { get; } = activeProfile;

        public IReadOnlyList<ProfileInfo> Profiles { get; } = [activeProfile];

        public event EventHandler<ProfileChangedEventArgs>? ProfileChanged
        {
            add => ArgumentNullException.ThrowIfNull(value);
            remove => ArgumentNullException.ThrowIfNull(value);
        }

        public Task InitializeAsync() => Task.CompletedTask;

        public Task SwitchProfileAsync(string profileId) => throw new NotSupportedException();

        public Task<ProfileInfo> CreateProfileAsync(string displayName) => throw new NotSupportedException();

        public Task RenameProfileAsync(string profileId, string newDisplayName) => throw new NotSupportedException();

        public Task DeleteProfileAsync(string profileId) => throw new NotSupportedException();

        public string GetProfileDirectory(string profileId) => throw new NotSupportedException();
    }
