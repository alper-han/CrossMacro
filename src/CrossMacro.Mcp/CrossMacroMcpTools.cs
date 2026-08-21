namespace CrossMacro.Mcp;

/// <summary>
/// Implements the explicit CrossMacro MCP v1 tool surface.
/// </summary>
public sealed partial class CrossMacroMcpTools(
    IRuntimeContext runtimeContext,
    IDoctorService doctorService,
    IProfileManager profileManager,
    ISettingsCliService settingsCliService,
    IProfileCliService profileCliService,
    ITextExpansionCliService textExpansionCliService,
    IScheduleCliService scheduleCliService,
    IShortcutCliService shortcutCliService,
    ITriggerCliService triggerCliService,
    IQuickSetupCliService quickSetupCliService,
    IMacroExecutionService macroExecutionService,
    IClipboardCliService clipboardCliService,
    IWindowCliService windowCliService,
    IScreenCliService screenCliService,
    IScreenshotCaptureService screenshotCaptureService,
    IImageAssetCodec imageAssetCodec,
    IImageClipboardReader imageClipboardReader,
    IImageClipboardService imageClipboardService,
    IMcpOperationCoordinator operationCoordinator,
    IRunScriptExecutionService runScriptExecutionService,
    IRecordExecutionService recordExecutionService,
    ICliPreflightService cliPreflightService,
    CliCommandExecutor cliCommandExecutor,
    IMcpCommandPolicy commandPolicy,
    IMcpCapabilityPolicy capabilityPolicy,
    IMcpPathPolicy pathPolicy,
    ILinuxDaemonHandshakeProbe? daemonHandshakeProbe = null,
    ILinuxDaemonSocketAccessProbe? daemonSocketAccessProbe = null,
    IMousePositionProvider? mousePositionProvider = null)
{
    private const int MaximumMacroListCount = 100;
    private const int MaximumClipboardTextCharacters = 65_536;
    private const int MaximumWindowResultCount = 100;
    private const int MaximumWindowSelectorCharacters = 1_024;
    private const int DefaultWindowWaitTimeoutMs = 5_000;
    private const int MaximumWindowWaitTimeoutMs = 30_000;
    private const int DefaultScreenTimeoutMs = 5_000;
    private const int MaximumScreenTimeoutMs = 30_000;
    private const int MaximumScreenRegionPixels = 16_777_216;
    private const long MaximumScreenImageBytes = ScreenshotPngCaptureLimits.MaximumEncodedBytes;
    private const int MaximumClipboardImageBytes = ScreenshotPngCaptureLimits.MaximumEncodedBytes;
    private const int MaximumInlineScreenshotBytes = 8 * 1024 * 1024;
    private const int MaximumAutomationTimeoutSeconds = 3_600;
    private const int DefaultAutomationTimeoutSeconds = MaximumAutomationTimeoutSeconds;
    private const int MaximumAutomationRepeatDelayMs = 3_600_000;
    private const int MaximumAutomationRecordDurationSeconds = 3_600;
    private const int MaximumAutomationStepCount = 100;
    private const int MaximumAutomationStepCharacters = 16_384;
    private const int MaximumAutomationStepPayloadCharacters = 262_144;

    private static readonly string[] AvailableToolNames = [.. CrossMacroMcpToolCatalog.V1.Select(static tool => tool.Name)];

    private readonly IRuntimeContext _runtimeContext = runtimeContext;
    private readonly IDoctorService _doctorService = doctorService;
    private readonly IProfileManager _profileManager = profileManager;
    private readonly ISettingsCliService _settingsCliService = settingsCliService;
    private readonly IProfileCliService _profileCliService = profileCliService;
    private readonly ITextExpansionCliService _textExpansionCliService = textExpansionCliService;
    private readonly IScheduleCliService _scheduleCliService = scheduleCliService;
    private readonly IShortcutCliService _shortcutCliService = shortcutCliService;
    private readonly ITriggerCliService _triggerCliService = triggerCliService;
    private readonly IQuickSetupCliService _quickSetupCliService = quickSetupCliService;
    private readonly ILinuxDaemonHandshakeProbe? _daemonHandshakeProbe = daemonHandshakeProbe;
    private readonly ILinuxDaemonSocketAccessProbe? _daemonSocketAccessProbe = daemonSocketAccessProbe;
    private readonly IMacroExecutionService _macroExecutionService = macroExecutionService;
    private readonly IClipboardCliService _clipboardCliService = clipboardCliService;
    private readonly IWindowCliService _windowCliService = windowCliService;
    private readonly IScreenCliService _screenCliService = screenCliService;
    private readonly IScreenshotCaptureService _screenshotCaptureService = screenshotCaptureService;
    private readonly IImageAssetCodec _imageAssetCodec = imageAssetCodec;
    private readonly IImageClipboardReader _imageClipboardReader = imageClipboardReader;
    private readonly IImageClipboardService _imageClipboardService = imageClipboardService;
    private readonly IMcpOperationCoordinator _operationCoordinator = operationCoordinator;
    private readonly IRunScriptExecutionService _runScriptExecutionService = runScriptExecutionService;
    private readonly IRecordExecutionService _recordExecutionService = recordExecutionService;
    private readonly ICliPreflightService _cliPreflightService = cliPreflightService;
    private readonly CliCommandExecutor _cliCommandExecutor = cliCommandExecutor;
    private readonly IMcpCommandPolicy _commandPolicy = commandPolicy;
    private readonly IMcpCapabilityPolicy _capabilityPolicy = capabilityPolicy;
    private readonly IMcpPathPolicy _pathPolicy = pathPolicy;
    private readonly IMousePositionProvider? _mousePositionProvider = mousePositionProvider;

    [McpServerTool(
        Name = "status.get",
        Title = "Get CrossMacro status",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        UseStructuredContent = true,
        OutputSchemaType = typeof(McpStatusResult))]
    [Description("Gets CrossMacro runtime and desktop-session status without changing the desktop.")]
    public async Task<McpStatusResult> GetStatusAsync(CancellationToken cancellationToken)
    {
        var capability = _capabilityPolicy.Require(McpCapability.StatusRead);
        if (!capability.Success)
        {
            return new McpStatusResult(
                Runtime: "mcp",
                ProductVersion: typeof(CrossMacroMcpTools).Assembly.GetName().Version?.ToString() ?? "unknown",
                OperatingSystem: GetOperatingSystem(),
                SessionType: _runtimeContext.SessionType,
                IsFlatpak: _runtimeContext.IsFlatpak,
                ActiveProfile: new McpActiveProfile(Id: "unknown", Name: "unknown"),
                Capabilities: new McpCapabilitySummary(HasFailures: true, HasWarnings: false, Checks: []),
                ImageClipboard: new McpImageClipboardCapability(ReadSupported: false, WriteSupported: false),
                ActiveOperation: null,
                Policy: "capability-policy-v1",
                IsRestricted: _capabilityPolicy.IsRestricted,
                EnabledCapabilities: [],
                AvailableTools: []);
        }

        var doctorReport = await _doctorService.RunAsync(verbose: false, cancellationToken).ConfigureAwait(false);
        var activeProfile = _profileManager.ActiveProfile;

        return new McpStatusResult(
            Runtime: "mcp",
            ProductVersion: typeof(CrossMacroMcpTools).Assembly.GetName().Version?.ToString() ?? "unknown",
            OperatingSystem: GetOperatingSystem(),
            SessionType: _runtimeContext.SessionType,
            IsFlatpak: _runtimeContext.IsFlatpak,
            ActiveProfile: new McpActiveProfile(activeProfile.Id, activeProfile.Name),
            Capabilities: new McpCapabilitySummary(
                HasFailures: doctorReport.HasFailures,
                HasWarnings: doctorReport.HasWarnings,
                Checks: doctorReport.Checks
                    .Select(static check => new McpCapabilityStatus(
                        Name: check.Name,
                        Status: check.Status.ToString().ToLowerInvariant(),
                        Message: GetCapabilityMessage(check.Status)))
                    .ToArray()),
            ImageClipboard: new McpImageClipboardCapability(
                ReadSupported: _imageClipboardReader.IsSupported,
                WriteSupported: _imageClipboardService.IsSupported),
            ActiveOperation: _operationCoordinator.GetActive(),
            Policy: "capability-policy-v1",
            IsRestricted: _capabilityPolicy.IsRestricted,
            EnabledCapabilities: Enum.GetValues<McpCapability>()
                .Where(_capabilityPolicy.IsAllowed)
                .Select(static capability => capability.ToString())
                .ToArray(),
            AvailableTools: AvailableToolNames);
    }

    [McpServerTool(
        Name = "settings.get",
        Title = "Get settings",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        UseStructuredContent = true,
        OutputSchemaType = typeof(McpSettingsResult))]
    [Description("Reads one setting or all supported settings. Sensitive values are redacted.")]
    public async Task<McpSettingsResult> GetSettingsAsync(
        string? key = null,
        bool all = false,
        CancellationToken cancellationToken = default)
    {
        var capability = _capabilityPolicy.Require(McpCapability.SettingsRead);
        if (!capability.Success)
        {
            return CreateSettingsResult("get", capability, data: null);
        }

        var result = await _settingsCliService.GetAsync(all ? null : key, cancellationToken).ConfigureAwait(false);
        return CreateSettingsResult("get", McpToolOutcomeMapper.FromSettingsResult(result), result.Data);
    }

    [McpServerTool(
        Name = "settings.set",
        Title = "Set a setting",
        ReadOnly = false,
        Destructive = true,
        Idempotent = true,
        UseStructuredContent = true,
        OutputSchemaType = typeof(McpSettingsResult))]
    [Description("Updates one supported CrossMacro setting.")]
    public async Task<McpSettingsResult> SetSettingsAsync(
        string key,
        string value,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(value);
        var capability = _capabilityPolicy.Require(McpCapability.SettingsWrite);
        if (!capability.Success)
        {
            return CreateSettingsResult("set", capability, data: null);
        }

        if (McpSettingsKeys.IsPolicyKey(key))
        {
            return CreateSettingsResult(
                "set",
                McpToolOutcomeMapper.Denied("MCP security settings can only be changed outside an MCP session."),
                data: null);
        }

        var result = await _settingsCliService.SetAsync(key, value, cancellationToken).ConfigureAwait(false);
        return CreateSettingsResult("set", McpToolOutcomeMapper.FromSettingsResult(result), result.Data);
    }

    [McpServerTool(
        Name = "settings.list_keys",
        Title = "List setting keys",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        UseStructuredContent = true,
        OutputSchemaType = typeof(McpSettingsResult))]
    [Description("Lists supported CrossMacro settings keys.")]
    public async Task<McpSettingsResult> ListSettingsKeysAsync(CancellationToken cancellationToken = default)
    {
        var capability = _capabilityPolicy.Require(McpCapability.SettingsRead);
        if (!capability.Success)
        {
            return CreateSettingsResult("list_keys", capability, data: null);
        }

        var result = await _settingsCliService.ListKeysAsync(cancellationToken).ConfigureAwait(false);
        return CreateSettingsResult("list_keys", McpToolOutcomeMapper.FromSettingsResult(result), result.Data);
    }

    [McpServerTool(
        Name = "settings.reset",
        Title = "Reset a setting",
        ReadOnly = false,
        Destructive = true,
        Idempotent = true,
        UseStructuredContent = true,
        OutputSchemaType = typeof(McpSettingsResult))]
    [Description("Resets one supported CrossMacro setting to its default value.")]
    public async Task<McpSettingsResult> ResetSettingsAsync(
        string key,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        var capability = _capabilityPolicy.Require(McpCapability.SettingsWrite);
        if (!capability.Success)
        {
            return CreateSettingsResult("reset", capability, data: null);
        }

        if (McpSettingsKeys.IsPolicyKey(key))
        {
            return CreateSettingsResult(
                "reset",
                McpToolOutcomeMapper.Denied("MCP security settings can only be changed outside an MCP session."),
                data: null);
        }

        var result = await _settingsCliService.ResetAsync(key, cancellationToken).ConfigureAwait(false);
        return CreateSettingsResult("reset", McpToolOutcomeMapper.FromSettingsResult(result), result.Data);
    }

    [McpServerTool(Name = "profile.list", Title = "List profiles", ReadOnly = true, Destructive = false, Idempotent = true, UseStructuredContent = true, OutputSchemaType = typeof(McpProfilesResult))]
    public async Task<McpProfilesResult> ListProfilesAsync(CancellationToken cancellationToken = default)
    {
        var capability = _capabilityPolicy.Require(McpCapability.ProfileManage);
        return capability.Success
            ? CreateProfilesResult("list", await _profileCliService.ListAsync(cancellationToken).ConfigureAwait(false))
            : CreateProfilesResult("list", capability);
    }

    [McpServerTool(Name = "profile.current", Title = "Get current profile", ReadOnly = true, Destructive = false, Idempotent = true, UseStructuredContent = true, OutputSchemaType = typeof(McpProfilesResult))]
    public async Task<McpProfilesResult> GetCurrentProfileAsync(CancellationToken cancellationToken = default)
    {
        var capability = _capabilityPolicy.Require(McpCapability.ProfileManage);
        return capability.Success
            ? CreateProfilesResult("current", await _profileCliService.CurrentAsync(cancellationToken).ConfigureAwait(false))
            : CreateProfilesResult("current", capability);
    }

    [McpServerTool(Name = "profile.create", Title = "Create a profile", ReadOnly = false, Destructive = true, Idempotent = false, UseStructuredContent = true, OutputSchemaType = typeof(McpProfilesResult))]
    public async Task<McpProfilesResult> CreateProfileAsync(string name, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(name);
        var capability = _capabilityPolicy.Require(McpCapability.ProfileManage);
        return capability.Success
            ? CreateProfilesResult("create", await _profileCliService.CreateAsync(name, cancellationToken).ConfigureAwait(false))
            : CreateProfilesResult("create", capability);
    }

    [McpServerTool(Name = "profile.switch", Title = "Switch profile", ReadOnly = false, Destructive = true, Idempotent = true, UseStructuredContent = true, OutputSchemaType = typeof(McpProfilesResult))]
    public async Task<McpProfilesResult> SwitchProfileAsync(string profile, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var capability = _capabilityPolicy.Require(McpCapability.ProfileManage);
        return capability.Success
            ? CreateProfilesResult("switch", await _profileCliService.SwitchAsync(profile, cancellationToken).ConfigureAwait(false))
            : CreateProfilesResult("switch", capability);
    }

    [McpServerTool(Name = "profile.rename", Title = "Rename a profile", ReadOnly = false, Destructive = true, Idempotent = true, UseStructuredContent = true, OutputSchemaType = typeof(McpProfilesResult))]
    public async Task<McpProfilesResult> RenameProfileAsync(string profile, string newName, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(newName);
        var capability = _capabilityPolicy.Require(McpCapability.ProfileManage);
        return capability.Success
            ? CreateProfilesResult("rename", await _profileCliService.RenameAsync(profile, newName, cancellationToken).ConfigureAwait(false))
            : CreateProfilesResult("rename", capability);
    }

    [McpServerTool(Name = "profile.delete", Title = "Delete a profile", ReadOnly = false, Destructive = true, Idempotent = true, UseStructuredContent = true, OutputSchemaType = typeof(McpProfilesResult))]
    public async Task<McpProfilesResult> DeleteProfileAsync(string profile, bool force = false, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var capability = _capabilityPolicy.Require(McpCapability.ProfileManage);
        return capability.Success
            ? CreateProfilesResult("delete", await _profileCliService.DeleteAsync(profile, force, cancellationToken).ConfigureAwait(false))
            : CreateProfilesResult("delete", capability);
    }

    [McpServerTool(Name = "text_expansion.list", Title = "List text expansions", ReadOnly = true, Destructive = false, Idempotent = true, UseStructuredContent = true, OutputSchemaType = typeof(McpTextExpansionsResult))]
    public async Task<McpTextExpansionsResult> ListTextExpansionsAsync(string? profile = null, CancellationToken cancellationToken = default)
    {
        var capability = _capabilityPolicy.Require(McpCapability.TextExpansionRead);
        return capability.Success
            ? CreateTextExpansionsResult("list", await _textExpansionCliService.ListAsync(profile, cancellationToken).ConfigureAwait(false))
            : CreateTextExpansionsResult("list", capability);
    }

    [McpServerTool(Name = "text_expansion.add", Title = "Add a text expansion", ReadOnly = false, Destructive = true, Idempotent = false, UseStructuredContent = true, OutputSchemaType = typeof(McpTextExpansionsResult))]
    public async Task<McpTextExpansionsResult> AddTextExpansionAsync(string trigger, string replacement, string? method = null, string? insertionMode = null, string? directTypingMethod = null, string? profile = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(trigger);
        ArgumentNullException.ThrowIfNull(replacement);
        var capability = _capabilityPolicy.Require(McpCapability.TextExpansionWrite);
        if (!capability.Success)
        {
            return CreateTextExpansionsResult("add", capability);
        }

        if (!Enum.TryParse(method ?? nameof(PasteMethod.CtrlV), ignoreCase: true, out PasteMethod pasteMethod)
            || !Enum.TryParse(insertionMode ?? nameof(TextInsertionMode.Paste), ignoreCase: true, out TextInsertionMode insertion)
            || !Enum.TryParse(directTypingMethod ?? nameof(DirectTypingMethod.FastBatch), ignoreCase: true, out DirectTypingMethod directTyping))
        {
            return CreateTextExpansionsResult("add", McpToolOutcomeMapper.InvalidArguments("Text expansion method options are invalid."));
        }

        return CreateTextExpansionsResult("add", await _textExpansionCliService.AddAsync(trigger, replacement, pasteMethod, insertion, directTyping, profile, cancellationToken).ConfigureAwait(false));
    }

    [McpServerTool(Name = "text_expansion.remove", Title = "Remove a text expansion", ReadOnly = false, Destructive = true, Idempotent = true, UseStructuredContent = true, OutputSchemaType = typeof(McpTextExpansionsResult))]
    public async Task<McpTextExpansionsResult> RemoveTextExpansionAsync(string trigger, string? profile = null, CancellationToken cancellationToken = default)
    {
        var capability = _capabilityPolicy.Require(McpCapability.TextExpansionWrite);
        return capability.Success
            ? CreateTextExpansionsResult("remove", await _textExpansionCliService.RemoveAsync(trigger, profile, cancellationToken).ConfigureAwait(false))
            : CreateTextExpansionsResult("remove", capability);
    }

    [McpServerTool(Name = "text_expansion.enable", Title = "Enable a text expansion", ReadOnly = false, Destructive = true, Idempotent = true, UseStructuredContent = true, OutputSchemaType = typeof(McpTextExpansionsResult))]
    public async Task<McpTextExpansionsResult> EnableTextExpansionAsync(string trigger, string? profile = null, CancellationToken cancellationToken = default)
    {
        var capability = _capabilityPolicy.Require(McpCapability.TextExpansionWrite);
        return capability.Success
            ? CreateTextExpansionsResult("enable", await _textExpansionCliService.EnableAsync(trigger, profile, cancellationToken).ConfigureAwait(false))
            : CreateTextExpansionsResult("enable", capability);
    }

    [McpServerTool(Name = "text_expansion.disable", Title = "Disable a text expansion", ReadOnly = false, Destructive = true, Idempotent = true, UseStructuredContent = true, OutputSchemaType = typeof(McpTextExpansionsResult))]
    public async Task<McpTextExpansionsResult> DisableTextExpansionAsync(string trigger, string? profile = null, CancellationToken cancellationToken = default)
    {
        var capability = _capabilityPolicy.Require(McpCapability.TextExpansionWrite);
        return capability.Success
            ? CreateTextExpansionsResult("disable", await _textExpansionCliService.DisableAsync(trigger, profile, cancellationToken).ConfigureAwait(false))
            : CreateTextExpansionsResult("disable", capability);
    }

    [McpServerTool(Name = "text_expansion.test", Title = "Test a text expansion", ReadOnly = true, Destructive = false, Idempotent = true, UseStructuredContent = true, OutputSchemaType = typeof(McpTextExpansionsResult))]
    public async Task<McpTextExpansionsResult> TestTextExpansionAsync(string trigger, string? profile = null, CancellationToken cancellationToken = default)
    {
        var capability = _capabilityPolicy.Require(McpCapability.TextExpansionRead);
        return capability.Success
            ? CreateTextExpansionsResult("test", await _textExpansionCliService.TestAsync(trigger, profile, cancellationToken).ConfigureAwait(false))
            : CreateTextExpansionsResult("test", capability);
    }

    [McpServerTool(Name = "setup.status", Title = "Get setup status", ReadOnly = true, Destructive = false, Idempotent = true, UseStructuredContent = true, OutputSchemaType = typeof(McpSetupResult))]
    public McpSetupResult GetSetupStatus()
    {
        var status = _quickSetupCliService.GetStatus();
        return CreateSetupResult("status", status, McpToolOutcomeMapper.Success("Setup status retrieved."), executed: false);
    }

    [McpServerTool(Name = "daemon.status", Title = "Get Linux daemon status", ReadOnly = true, Destructive = false, Idempotent = true, UseStructuredContent = true, OutputSchemaType = typeof(McpDaemonResult))]
    public async Task<McpDaemonResult> GetDaemonStatusAsync(CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsLinux() || _daemonHandshakeProbe is null || _daemonSocketAccessProbe is null)
        {
            return new(
                Action: "status",
                Outcome: McpToolOutcomeMapper.EnvironmentError("Linux daemon diagnostics are unavailable on this platform."),
                SocketPath: null,
                HandshakeStatus: "unavailable",
                SocketAccessStatus: "unavailable",
                Message: null,
                LinuxOnly: true);
        }

        var socketPath = IpcProtocol.DefaultSocketPath;
        var handshake = _daemonHandshakeProbe.Probe(socketPath, TimeSpan.FromSeconds(2));
        var access = await _daemonSocketAccessProbe.ProbeAsync(new LinuxDaemonSocketProbeOptions(socketPath, "crossmacro"), cancellationToken).ConfigureAwait(false);
        var success = handshake.Succeeded && access.IsAccessible;
        return new(
            "status",
            success ? McpToolOutcomeMapper.Success("Linux daemon status retrieved.") : McpToolOutcomeMapper.EnvironmentError("Linux daemon is not ready."),
            socketPath,
            handshake.Status.ToString(),
            access.Status.ToString(),
            handshake.Message ?? access.Message,
            LinuxOnly: true);
    }

    [McpServerTool(Name = "setup.run", Title = "Run temporary setup", ReadOnly = false, Destructive = true, Idempotent = true, UseStructuredContent = true, OutputSchemaType = typeof(McpSetupResult))]
    public async Task<McpSetupResult> RunSetupAsync(CancellationToken cancellationToken = default)
    {
        var status = _quickSetupCliService.GetStatus();
        var capability = _capabilityPolicy.Require(McpCapability.PrivilegeElevation);
        if (!capability.Success)
        {
            return CreateSetupResult("run", status, capability, executed: false);
        }

        if (!status.Applicable)
        {
            return CreateSetupResult("run", status, McpToolOutcomeMapper.EnvironmentError("Temporary input setup is not applicable in this session."), executed: false);
        }

        var result = await _quickSetupCliService.RunAsync(cancellationToken).ConfigureAwait(false);
        var outcome = result.Result.Success
            ? McpToolOutcomeMapper.Success("Temporary input setup completed.")
            : McpToolOutcomeMapper.EnvironmentError("Temporary input setup failed.");
        return CreateSetupResult("run", new QuickSetupStatus(result.Applicable, result.Provider, status.ShouldPrompt), outcome, executed: true);
    }


    [McpServerTool(Name = "schedule.list", Title = "List schedules", ReadOnly = true, Destructive = false, Idempotent = true, UseStructuredContent = true, OutputSchemaType = typeof(McpScheduleResult))]
    public async Task<McpScheduleResult> ListSchedulesAsync(CancellationToken cancellationToken = default)
    {
        var capability = _capabilityPolicy.Require(McpCapability.TaskManage);
        return capability.Success
            ? CreateScheduleResult("list", await _scheduleCliService.ListAsync(cancellationToken).ConfigureAwait(false))
            : CreateScheduleResult("list", capability);
    }

    [McpServerTool(Name = "schedule.run", Title = "Run a schedule", ReadOnly = false, Destructive = true, Idempotent = false, UseStructuredContent = true, OutputSchemaType = typeof(McpScheduleResult))]
    public async Task<McpScheduleResult> RunScheduleAsync(string taskId, CancellationToken cancellationToken = default)
    {
        var capability = RequireTaskManagementCapability(requiresInputAutomation: true, requiresMacroRead: true);
        if (capability is not null)
        {
            return CreateScheduleResult("run", capability);
        }

        var taskAuthorization = await TryAuthorizeScheduleTaskMacroAsync(taskId, cancellationToken).ConfigureAwait(false);
        return taskAuthorization is null
            ? CreateScheduleResult("run", await _scheduleCliService.RunAsync(taskId, cancellationToken).ConfigureAwait(false))
            : CreateScheduleResult("run", taskAuthorization);
    }

    [McpServerTool(Name = "schedule.add", Title = "Add a schedule", ReadOnly = false, Destructive = true, Idempotent = false, UseStructuredContent = true, OutputSchemaType = typeof(McpScheduleResult))]
    public Task<McpScheduleResult> AddScheduleAsync(string name, string macroPath, string? interval = null, string? at = null, string? weekly = null, string? time = null, double? speed = null, bool? enabled = null, CancellationToken cancellationToken = default) =>
        ExecuteScheduleAsync("add", new ScheduleCliOptions(ScheduleCliAction.Add, Name: name, MacroFilePath: macroPath, Interval: interval, At: at, Weekly: weekly, Time: time, Speed: speed, Enabled: enabled), cancellationToken);

    [McpServerTool(Name = "schedule.edit", Title = "Edit a schedule", ReadOnly = false, Destructive = true, Idempotent = true, UseStructuredContent = true, OutputSchemaType = typeof(McpScheduleResult))]
    public Task<McpScheduleResult> EditScheduleAsync(string taskId, string? name = null, string? macroPath = null, string? interval = null, string? at = null, string? weekly = null, string? time = null, double? speed = null, bool? enabled = null, CancellationToken cancellationToken = default) =>
        ExecuteScheduleAsync("edit", new ScheduleCliOptions(ScheduleCliAction.Edit, TaskId: taskId, Name: name, MacroFilePath: macroPath, Interval: interval, At: at, Weekly: weekly, Time: time, Speed: speed, Enabled: enabled), cancellationToken);

    [McpServerTool(Name = "schedule.remove", Title = "Remove a schedule", ReadOnly = false, Destructive = true, Idempotent = true, UseStructuredContent = true, OutputSchemaType = typeof(McpScheduleResult))]
    public Task<McpScheduleResult> RemoveScheduleAsync(string taskId, CancellationToken cancellationToken = default) => ExecuteScheduleAsync("remove", new ScheduleCliOptions(ScheduleCliAction.Remove, TaskId: taskId), cancellationToken);

    [McpServerTool(Name = "schedule.enable", Title = "Enable a schedule", ReadOnly = false, Destructive = true, Idempotent = true, UseStructuredContent = true, OutputSchemaType = typeof(McpScheduleResult))]
    public Task<McpScheduleResult> EnableScheduleAsync(string taskId, CancellationToken cancellationToken = default) => ExecuteScheduleAsync("enable", new ScheduleCliOptions(ScheduleCliAction.Enable, TaskId: taskId), cancellationToken);

    [McpServerTool(Name = "schedule.disable", Title = "Disable a schedule", ReadOnly = false, Destructive = true, Idempotent = true, UseStructuredContent = true, OutputSchemaType = typeof(McpScheduleResult))]
    public Task<McpScheduleResult> DisableScheduleAsync(string taskId, CancellationToken cancellationToken = default) => ExecuteScheduleAsync("disable", new ScheduleCliOptions(ScheduleCliAction.Disable, TaskId: taskId), cancellationToken);

    [McpServerTool(Name = "schedule.next", Title = "Get next schedule run", ReadOnly = true, Destructive = false, Idempotent = true, UseStructuredContent = true, OutputSchemaType = typeof(McpScheduleResult))]
    public Task<McpScheduleResult> NextScheduleAsync(string taskId, CancellationToken cancellationToken = default) => ExecuteScheduleAsync("next", new ScheduleCliOptions(ScheduleCliAction.Next, TaskId: taskId), cancellationToken);

    [McpServerTool(Name = "shortcut.list", Title = "List shortcuts", ReadOnly = true, Destructive = false, Idempotent = true, UseStructuredContent = true, OutputSchemaType = typeof(McpShortcutResult))]
    public async Task<McpShortcutResult> ListShortcutsAsync(CancellationToken cancellationToken = default)
    {
        var capability = _capabilityPolicy.Require(McpCapability.TaskManage);
        return capability.Success
            ? CreateShortcutResult("list", await _shortcutCliService.ListAsync(cancellationToken).ConfigureAwait(false))
            : CreateShortcutResult("list", capability);
    }

    [McpServerTool(Name = "shortcut.run", Title = "Run a shortcut", ReadOnly = false, Destructive = true, Idempotent = false, UseStructuredContent = true, OutputSchemaType = typeof(McpShortcutResult))]
    public async Task<McpShortcutResult> RunShortcutAsync(string taskId, CancellationToken cancellationToken = default)
    {
        var capability = RequireTaskManagementCapability(requiresInputAutomation: true, requiresMacroRead: true);
        if (capability is not null)
        {
            return CreateShortcutResult("run", capability);
        }

        var taskAuthorization = await TryAuthorizeShortcutTaskMacroAsync(taskId, cancellationToken).ConfigureAwait(false);
        return taskAuthorization is null
            ? CreateShortcutResult("run", await _shortcutCliService.RunAsync(taskId, cancellationToken).ConfigureAwait(false))
            : CreateShortcutResult("run", taskAuthorization);
    }

    [McpServerTool(Name = "shortcut.add", Title = "Add a shortcut", ReadOnly = false, Destructive = true, Idempotent = false, UseStructuredContent = true, OutputSchemaType = typeof(McpShortcutResult))]
    public Task<McpShortcutResult> AddShortcutAsync(string name, string macroPath, string hotkey, double? speed = null, bool? loop = null, int? repeatCount = null, int? repeatDelayMs = null, int? repeatDelayMinMs = null, int? repeatDelayMaxMs = null, bool runWhileHeld = false, bool? enabled = null, IReadOnlyList<ShortcutWindowRule>? windowRules = null, bool clearWindowRules = false, CancellationToken cancellationToken = default) =>
        ExecuteShortcutAsync("add", new ShortcutCliOptions(ShortcutCliAction.Add, Name: name, MacroFilePath: macroPath, Hotkey: hotkey, Speed: speed, Loop: loop, RepeatCount: repeatCount, RepeatDelayMs: repeatDelayMs, RepeatDelayMinMs: repeatDelayMinMs, RepeatDelayMaxMs: repeatDelayMaxMs, RunWhileHeld: runWhileHeld, Enabled: enabled, WindowRules: windowRules, ClearWindowRules: clearWindowRules), cancellationToken);

    [McpServerTool(Name = "shortcut.edit", Title = "Edit a shortcut", ReadOnly = false, Destructive = true, Idempotent = true, UseStructuredContent = true, OutputSchemaType = typeof(McpShortcutResult))]
    public Task<McpShortcutResult> EditShortcutAsync(string taskId, string? name = null, string? macroPath = null, string? hotkey = null, double? speed = null, bool? loop = null, int? repeatCount = null, int? repeatDelayMs = null, int? repeatDelayMinMs = null, int? repeatDelayMaxMs = null, bool runWhileHeld = false, bool? enabled = null, IReadOnlyList<ShortcutWindowRule>? windowRules = null, bool clearWindowRules = false, CancellationToken cancellationToken = default) =>
        ExecuteShortcutAsync("edit", new ShortcutCliOptions(ShortcutCliAction.Edit, TaskId: taskId, Name: name, MacroFilePath: macroPath, Hotkey: hotkey, Speed: speed, Loop: loop, RepeatCount: repeatCount, RepeatDelayMs: repeatDelayMs, RepeatDelayMinMs: repeatDelayMinMs, RepeatDelayMaxMs: repeatDelayMaxMs, RunWhileHeld: runWhileHeld, Enabled: enabled, WindowRules: windowRules, ClearWindowRules: clearWindowRules), cancellationToken);

    [McpServerTool(Name = "shortcut.remove", Title = "Remove a shortcut", ReadOnly = false, Destructive = true, Idempotent = true, UseStructuredContent = true, OutputSchemaType = typeof(McpShortcutResult))]
    public Task<McpShortcutResult> RemoveShortcutAsync(string taskId, CancellationToken cancellationToken = default) => ExecuteShortcutAsync("remove", new ShortcutCliOptions(ShortcutCliAction.Remove, TaskId: taskId), cancellationToken);

    [McpServerTool(Name = "shortcut.enable", Title = "Enable a shortcut", ReadOnly = false, Destructive = true, Idempotent = true, UseStructuredContent = true, OutputSchemaType = typeof(McpShortcutResult))]
    public Task<McpShortcutResult> EnableShortcutAsync(string taskId, CancellationToken cancellationToken = default) => ExecuteShortcutAsync("enable", new ShortcutCliOptions(ShortcutCliAction.Enable, TaskId: taskId), cancellationToken);

    [McpServerTool(Name = "shortcut.disable", Title = "Disable a shortcut", ReadOnly = false, Destructive = true, Idempotent = true, UseStructuredContent = true, OutputSchemaType = typeof(McpShortcutResult))]
    public Task<McpShortcutResult> DisableShortcutAsync(string taskId, CancellationToken cancellationToken = default) => ExecuteShortcutAsync("disable", new ShortcutCliOptions(ShortcutCliAction.Disable, TaskId: taskId), cancellationToken);

    [McpServerTool(Name = "shortcut.bind", Title = "Bind a shortcut", ReadOnly = false, Destructive = true, Idempotent = true, UseStructuredContent = true, OutputSchemaType = typeof(McpShortcutResult))]
    public Task<McpShortcutResult> BindShortcutAsync(string taskId, string hotkey, CancellationToken cancellationToken = default) => ExecuteShortcutAsync("bind", new ShortcutCliOptions(ShortcutCliAction.Bind, TaskId: taskId, Hotkey: hotkey), cancellationToken);

    [McpServerTool(Name = "trigger.list", Title = "List triggers", ReadOnly = true, Destructive = false, Idempotent = true, UseStructuredContent = true, OutputSchemaType = typeof(McpTriggerResult))]
    public async Task<McpTriggerResult> ListTriggersAsync(CancellationToken cancellationToken = default)
    {
        var capability = _capabilityPolicy.Require(McpCapability.TaskManage);
        return capability.Success
            ? CreateTriggerResult("list", await _triggerCliService.ListAsync(cancellationToken).ConfigureAwait(false))
            : CreateTriggerResult("list", capability);
    }

    [McpServerTool(Name = "trigger.add", Title = "Add a trigger", ReadOnly = false, Destructive = true, Idempotent = false, UseStructuredContent = true, OutputSchemaType = typeof(McpTriggerResult))]
    public Task<McpTriggerResult> AddTriggerAsync(string name, string field, string value, string? matchMode = null, string? action = null, string? targetProfileId = null, string? macroPath = null, string? fireMode = null, int? cooldownMs = null, int? debounceMs = null, bool? enabled = null, CancellationToken cancellationToken = default) =>
        ExecuteTriggerAsync("add", CreateTriggerOptions(TriggerCliAction.Add, name, field, value, matchMode, action, targetProfileId, macroPath, fireMode, cooldownMs, debounceMs, enabled), cancellationToken);

    [McpServerTool(Name = "trigger.edit", Title = "Edit a trigger", ReadOnly = false, Destructive = true, Idempotent = true, UseStructuredContent = true, OutputSchemaType = typeof(McpTriggerResult))]
    public Task<McpTriggerResult> EditTriggerAsync(string taskId, string? name = null, string? field = null, string? value = null, string? matchMode = null, string? action = null, string? targetProfileId = null, string? macroPath = null, string? fireMode = null, int? cooldownMs = null, int? debounceMs = null, bool? enabled = null, CancellationToken cancellationToken = default) =>
        ExecuteTriggerAsync("edit", CreateTriggerOptions(TriggerCliAction.Edit, name, field, value, matchMode, action, targetProfileId, macroPath, fireMode, cooldownMs, debounceMs, enabled, taskId), cancellationToken);

    [McpServerTool(Name = "trigger.remove", Title = "Remove a trigger", ReadOnly = false, Destructive = true, Idempotent = true, UseStructuredContent = true, OutputSchemaType = typeof(McpTriggerResult))]
    public Task<McpTriggerResult> RemoveTriggerAsync(string taskId, CancellationToken cancellationToken = default) => ExecuteTriggerAsync("remove", new TriggerCliOptions(TriggerCliAction.Remove, TaskId: taskId), cancellationToken);

    [McpServerTool(Name = "trigger.enable", Title = "Enable a trigger", ReadOnly = false, Destructive = true, Idempotent = true, UseStructuredContent = true, OutputSchemaType = typeof(McpTriggerResult))]
    public Task<McpTriggerResult> EnableTriggerAsync(string taskId, CancellationToken cancellationToken = default) => ExecuteTriggerAsync("enable", new TriggerCliOptions(TriggerCliAction.Enable, TaskId: taskId), cancellationToken);

    [McpServerTool(Name = "trigger.disable", Title = "Disable a trigger", ReadOnly = false, Destructive = true, Idempotent = true, UseStructuredContent = true, OutputSchemaType = typeof(McpTriggerResult))]
    public Task<McpTriggerResult> DisableTriggerAsync(string taskId, CancellationToken cancellationToken = default) => ExecuteTriggerAsync("disable", new TriggerCliOptions(TriggerCliAction.Disable, TaskId: taskId), cancellationToken);

    private async Task<McpScheduleResult> ExecuteScheduleAsync(string action, ScheduleCliOptions options, CancellationToken cancellationToken)
    {
        var capability = RequireTaskManagementCapability(
            RequiresInputAutomation(options),
            requiresMacroRead: RequiresInputAutomation(options));
        if (capability is not null)
        {
            return CreateScheduleResult(action, capability);
        }

        var normalizedPath = options.MacroFilePath;
        if (!string.IsNullOrWhiteSpace(options.MacroFilePath)
            && !TryNormalizeMacroPath(options.MacroFilePath, out normalizedPath, out var pathError))
        {
            return CreateScheduleResult(action, pathError);
        }

        if (options.MacroFilePath is not null)
        {
            options = options with { MacroFilePath = normalizedPath };
        }

        if (options.Action is ScheduleCliAction.Enable
            || options is { Action: ScheduleCliAction.Edit, Enabled: true, MacroFilePath: null })
        {
            var taskAuthorization = await TryAuthorizeScheduleTaskMacroAsync(options.TaskId ?? string.Empty, cancellationToken).ConfigureAwait(false);
            if (taskAuthorization is not null)
            {
                return CreateScheduleResult(action, taskAuthorization);
            }
        }

        return CreateScheduleResult(action, await _scheduleCliService.ExecuteAsync(options, cancellationToken).ConfigureAwait(false));
    }

    private async Task<McpShortcutResult> ExecuteShortcutAsync(string action, ShortcutCliOptions options, CancellationToken cancellationToken)
    {
        var capability = RequireTaskManagementCapability(
            RequiresInputAutomation(options),
            requiresMacroRead: RequiresInputAutomation(options));
        if (capability is not null)
        {
            return CreateShortcutResult(action, capability);
        }

        var normalizedPath = options.MacroFilePath;
        if (!string.IsNullOrWhiteSpace(options.MacroFilePath)
            && !TryNormalizeMacroPath(options.MacroFilePath, out normalizedPath, out var pathError))
        {
            return CreateShortcutResult(action, pathError);
        }

        if (options.MacroFilePath is not null)
        {
            options = options with { MacroFilePath = normalizedPath };
        }

        if (options.Action is ShortcutCliAction.Enable
            || options is { Action: ShortcutCliAction.Edit, Enabled: true, MacroFilePath: null })
        {
            var taskAuthorization = await TryAuthorizeShortcutTaskMacroAsync(options.TaskId ?? string.Empty, cancellationToken).ConfigureAwait(false);
            if (taskAuthorization is not null)
            {
                return CreateShortcutResult(action, taskAuthorization);
            }
        }

        return CreateShortcutResult(action, await _shortcutCliService.ExecuteAsync(options, cancellationToken).ConfigureAwait(false));
    }

    private async Task<McpTriggerResult> ExecuteTriggerAsync(string action, TriggerCliOptions options, CancellationToken cancellationToken)
    {
        var capability = RequireTaskManagementCapability(
            RequiresInputAutomation(options),
            requiresMacroRead: RequiresMacroRead(options));
        if (capability is not null)
        {
            return CreateTriggerResult(action, capability);
        }

        var normalizedPath = options.MacroFilePath;
        if (!string.IsNullOrWhiteSpace(options.MacroFilePath)
            && !TryNormalizeMacroPath(options.MacroFilePath, out normalizedPath, out var pathError))
        {
            return CreateTriggerResult(action, pathError);
        }

        if (options.MacroFilePath is not null)
        {
            options = options with { MacroFilePath = normalizedPath };
        }

        if (options.Action is TriggerCliAction.Enable
            || options is { Action: TriggerCliAction.Edit, Enabled: true, MacroFilePath: null, TriggerActionVal: null })
        {
            var taskAuthorization = await TryAuthorizeTriggerTaskMacroAsync(options.TaskId ?? string.Empty, cancellationToken).ConfigureAwait(false);
            if (taskAuthorization is not null)
            {
                return CreateTriggerResult(action, taskAuthorization);
            }
        }

        return CreateTriggerResult(action, await _triggerCliService.ExecuteAsync(options, cancellationToken).ConfigureAwait(false));
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Globalization", "CA1308", Justification = "Enum parsing is intentionally case-insensitive for MCP option parity.")]
    private static TriggerCliOptions CreateTriggerOptions(
        TriggerCliAction action,
        string? name,
        string? field,
        string? value,
        string? matchMode,
        string? triggerAction,
        string? targetProfileId,
        string? macroPath,
        string? fireMode,
        int? cooldownMs,
        int? debounceMs,
        bool? enabled,
        string? taskId = null) =>
        new(
            action,
            taskId,
            name,
            TryParseEnum(field, out TriggerField parsedField) ? parsedField : null,
            TryParseEnum(matchMode, out TriggerMatchMode parsedMatchMode) ? parsedMatchMode : null,
            value,
            TryParseEnum(triggerAction, out TriggerOperation parsedAction) ? parsedAction : null,
            targetProfileId,
            macroPath,
            TryParseEnum(fireMode, out TriggerFireMode parsedFireMode) ? parsedFireMode : null,
            cooldownMs,
            debounceMs,
            enabled);

    private static bool TryParseEnum<TEnum>(string? value, out TEnum parsed)
        where TEnum : struct, Enum =>
        Enum.TryParse(value, ignoreCase: true, out parsed) && Enum.IsDefined(parsed);

    [McpServerTool(
        Name = "help.get",
        Title = "Get CrossMacro MCP help",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        UseStructuredContent = true,
        OutputSchemaType = typeof(McpHelpResult))]
    [Description("Gets safe usage guidance and the currently available CrossMacro MCP tools.")]
    public McpHelpResult GetHelp()
    {
        return new McpHelpResult(
            Transport: "local-stdio",
            RuntimeRule: "MCP uses local stdio. Multiple MCP sessions may run, and MCP may run alongside GUI or headless; GUI and headless remain mutually exclusive.",
            SafetyNote: "Use cursor.position to read the current global cursor. Use automation.start with kind=run and steps for input, for example ['mouse position mouse_x mouse_y', 'move abs 1 1']; use command.execute only with a CLI command token and argument array.",
            AvailableTools: CrossMacroMcpToolCatalog.V1
                .Select(tool => new McpAvailableTool(
                    tool.Name,
                    tool.Description,
                    tool.Access.ToString(),
                    Enabled: IsToolEnabled(tool),
                    operationCapabilityStatuses: GetOperationCapabilityStatuses(tool)))
                .ToArray(),
            IsRestricted: _capabilityPolicy.IsRestricted);
    }

    [McpServerTool(
        Name = "automation.start",
        Title = "Start automation",
        ReadOnly = false,
        Destructive = false,
        Idempotent = false,
        UseStructuredContent = true,
        OutputSchemaType = typeof(McpAutomationStartResult))]
    [Description("Starts one bounded play, run, or record operation and returns an opaque operation ID. Use automation.get and automation.stop for its lifecycle.")]
    public async Task<CallToolResult> StartAutomationAsync(
        string kind,
        string? macroPath = null,
        IReadOnlyList<string>? steps = null,
        string? outputPath = null,
        string? stepFilePath = null,
        IReadOnlyList<McpRunImageAsset>? imageAssets = null,
        double? speedMultiplier = null,
        bool loop = false,
        int? repeatCount = null,
        int? repeatDelayMs = null,
        int? countdownSeconds = null,
        int? timeoutSeconds = null,
        int? durationSeconds = null,
        bool dryRun = false,
        bool? recordMouse = null,
        bool? recordKeyboard = null,
        string? coordinateMode = null,
        string? motionMode = null,
        int? strictSpeedMotionEventsPerSecond = null,
        int? precisionMotionEventsPerSecond = null,
        double? maximumMotionErrorPixels = null,
        bool skipInitialZero = false,
        CancellationToken cancellationToken = default)
    {
        var normalizedKind = kind?.Trim().ToLowerInvariant();
        var capability = RequireAutomationCapability(normalizedKind);
        if (capability is not null)
        {
            return CreateAutomationStartToolResult(capability, operation: null);
        }

        return normalizedKind switch
        {
            "play" => await StartPlayAutomationAsync(
                macroPath,
                steps,
                outputPath,
                stepFilePath,
                imageAssets,
                speedMultiplier,
                loop,
                repeatCount,
                repeatDelayMs,
                countdownSeconds,
                timeoutSeconds,
                durationSeconds,
                dryRun,
                recordMouse,
                recordKeyboard,
                coordinateMode,
                motionMode,
                strictSpeedMotionEventsPerSecond,
                precisionMotionEventsPerSecond,
                maximumMotionErrorPixels,
                skipInitialZero,
                cancellationToken).ConfigureAwait(false),
            "run" => await StartRunAutomationAsync(
                macroPath,
                steps,
                outputPath,
                stepFilePath,
                imageAssets,
                speedMultiplier,
                loop,
                repeatCount,
                repeatDelayMs,
                countdownSeconds,
                timeoutSeconds,
                durationSeconds,
                dryRun,
                recordMouse,
                recordKeyboard,
                coordinateMode,
                motionMode,
                strictSpeedMotionEventsPerSecond,
                precisionMotionEventsPerSecond,
                maximumMotionErrorPixels,
                skipInitialZero,
                cancellationToken).ConfigureAwait(false),
            "record" => await StartRecordAutomationAsync(
                macroPath,
                steps,
                outputPath,
                stepFilePath,
                imageAssets,
                speedMultiplier,
                loop,
                repeatCount,
                repeatDelayMs,
                countdownSeconds,
                timeoutSeconds,
                durationSeconds,
                dryRun,
                recordMouse,
                recordKeyboard,
                coordinateMode,
                motionMode,
                strictSpeedMotionEventsPerSecond,
                precisionMotionEventsPerSecond,
                maximumMotionErrorPixels,
                skipInitialZero,
                cancellationToken).ConfigureAwait(false),
            _ => CreateAutomationStartToolResult(
                McpToolOutcomeMapper.InvalidArguments("Automation kind must be play, run, or record."),
                operation: null),
        };
    }

    [McpServerTool(
        Name = "automation.get",
        Title = "Get automation status",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        UseStructuredContent = true,
        OutputSchemaType = typeof(McpAutomationGetResult))]
    [Description("Gets an automation operation state and its final redacted outcome without returning original arguments or execution data.")]
    public CallToolResult GetAutomation(string operationId)
    {
        var capability = RequireCapability(McpCapability.StatusRead);
        if (capability is not null)
        {
            return CreateAutomationGetToolResult(capability, operation: null);
        }

        operationId ??= string.Empty;
        if (!IsValidOperationId(operationId))
        {
            return CreateAutomationGetToolResult(
                McpToolOutcomeMapper.InvalidArguments("Automation operation ID is invalid."),
                operation: null);
        }

        var operation = _operationCoordinator.GetOperation(operationId);
        return operation is null
            ? CreateAutomationGetToolResult(
                McpToolOutcomeMapper.InvalidArguments("Automation operation was not found."),
                operation: null)
            : CreateAutomationGetToolResult(
                McpToolOutcomeMapper.Success("Automation operation status retrieved."),
                operation);
    }

    [McpServerTool(
        Name = "automation.stop",
        Title = "Stop automation",
        ReadOnly = false,
        Destructive = false,
        Idempotent = true,
        UseStructuredContent = true,
        OutputSchemaType = typeof(McpAutomationStopResult))]
    [Description("Requests cancellation for an automation operation. Repeated requests are safe and report whether cancellation was newly initiated.")]
    public CallToolResult StopAutomation(string operationId)
    {
        if (!_capabilityPolicy.IsAnyAllowed(McpCapability.InputAutomation, McpCapability.Recording, McpCapability.CommandExecute))
        {
            return CreateAutomationStopToolResult(
                _capabilityPolicy.Require(McpCapability.InputAutomation),
                operation: null,
                cancellationInitiated: false);
        }

        operationId ??= string.Empty;
        if (!IsValidOperationId(operationId))
        {
            return CreateAutomationStopToolResult(
                McpToolOutcomeMapper.InvalidArguments("Automation operation ID is invalid."),
                operation: null,
                cancellationInitiated: false);
        }

        var result = _operationCoordinator.StopOperation(operationId);
        if (!result.Found)
        {
            return CreateAutomationStopToolResult(
                McpToolOutcomeMapper.InvalidArguments("Automation operation was not found."),
                operation: null,
                cancellationInitiated: false);
        }

        var outcome = result.CancellationInitiated
            ? McpToolOutcomeMapper.Success("Automation cancellation requested.")
            : McpToolOutcomeMapper.Success("Automation operation is already completed or cancellation was already requested.");
        return CreateAutomationStopToolResult(outcome, result.Operation, result.CancellationInitiated);
    }

    [McpServerTool(
        Name = "command.execute",
        Title = "Execute a CrossMacro command",
        ReadOnly = false,
        Destructive = true,
        Idempotent = false,
        UseStructuredContent = true,
        OutputSchemaType = typeof(McpCommandExecuteResult))]
    [Description("Executes a restricted existing CrossMacro CLI command using a command token and argument array, never a shell command string.")]
    public async Task<CallToolResult> ExecuteCommandAsync(
        string command,
        IReadOnlyList<string>? arguments = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var capability = RequireCapability(McpCapability.CommandExecute);
        if (capability is not null)
        {
            return CreateCommandExecuteToolResult(capability, command.Trim(), operationStarted: false, operationId: null);
        }

        var commandArguments = arguments ?? [];
        var policyOutcome = _commandPolicy.Validate(command, commandArguments);
        if (!policyOutcome.Success)
        {
            return CreateCommandExecuteToolResult(policyOutcome, command.Trim(), operationStarted: false, operationId: null);
        }

        var normalizedCommand = command.Trim().ToLowerInvariant();
        var parseArguments = new string[commandArguments.Count + 1];
        parseArguments[0] = normalizedCommand;
        for (var index = 0; index < commandArguments.Count; index++)
        {
            parseArguments[index + 1] = commandArguments[index];
        }
        var parseResult = CliCommandRouter.Parse(parseArguments);
        if (parseResult.Kind is not CliParseResult.ParseResultKind.Success || parseResult.Options is null)
        {
            return CreateCommandExecuteToolResult(
                McpToolOutcomeMapper.InvalidArguments(parseResult.ErrorMessage ?? "Command arguments are invalid."),
                normalizedCommand,
                operationStarted: false,
                operationId: null);
        }

        if (parseResult.Options is McpCliOptions or HeadlessCliOptions or QuickSetupCliOptions)
        {
            return CreateCommandExecuteToolResult(
                McpToolOutcomeMapper.InvalidArguments("This command is not available through command.execute."),
                normalizedCommand,
                operationStarted: false,
                operationId: null);
        }

        var commandCapability = RequireCommandCapability(parseResult.Options);
        if (commandCapability is not null)
        {
            return CreateCommandExecuteToolResult(
                commandCapability,
                normalizedCommand,
                operationStarted: false,
                operationId: null);
        }

        if (!TryAuthorizeCommandOptions(parseResult.Options, out var authorizedOptions, out var authorizationError))
        {
            return CreateCommandExecuteToolResult(
                authorizationError,
                normalizedCommand,
                operationStarted: false,
                operationId: null);
        }

        var taskAuthorization = await TryAuthorizeParsedCommandTaskMacroAsync(authorizedOptions, cancellationToken).ConfigureAwait(false);
        if (taskAuthorization is not null)
        {
            return CreateCommandExecuteToolResult(
                taskAuthorization,
                normalizedCommand,
                operationStarted: false,
                operationId: null);
        }

        if (authorizedOptions is PlayCliOptions playOptions)
        {
            return await StartParsedPlayCommandAsync(playOptions, normalizedCommand, cancellationToken).ConfigureAwait(false);
        }

        if (authorizedOptions is RunCliOptions runOptions)
        {
            return await StartParsedRunCommandAsync(runOptions, normalizedCommand, cancellationToken).ConfigureAwait(false);
        }

        if (authorizedOptions is RecordCliOptions recordOptions)
        {
            return await StartParsedRecordCommandAsync(recordOptions, normalizedCommand, cancellationToken).ConfigureAwait(false);
        }

        try
        {
            var result = await _cliCommandExecutor.ExecuteResultAsync(authorizedOptions, cancellationToken).ConfigureAwait(false);
            return CreateCommandExecuteToolResult(
                McpToolOutcomeMapper.FromCliResultRedactingErrorDetails(result),
                normalizedCommand,
                operationStarted: false,
                operationId: null);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            return CreateCommandExecuteToolResult(
                McpToolOutcomeMapper.FromException(exception),
                normalizedCommand,
                operationStarted: false,
                operationId: null);
        }
    }

    private async Task<CallToolResult> StartParsedPlayCommandAsync(PlayCliOptions options, string command, CancellationToken cancellationToken)
    {
        var macroReadCapability = RequireCapability(McpCapability.MacroRead);
        if (macroReadCapability is not null)
        {
            return CreateCommandExecuteToolResult(macroReadCapability, command, operationStarted: false, operationId: null);
        }

        if (!TryNormalizeMacroPath(options.MacroFilePath, out var normalizedMacroPath, out var pathError))
        {
            return CreateCommandExecuteToolResult(pathError, command, operationStarted: false, operationId: null);
        }

        options = options with
        {
            MacroFilePath = normalizedMacroPath,
            TimeoutSeconds = GetMcpAutomationTimeoutSeconds(options.TimeoutSeconds),
        };
        if (!options.DryRun)
        {
            var preflight = await _cliPreflightService.CheckAsync(CliPreflightTarget.Play, cancellationToken).ConfigureAwait(false);
            if (!preflight.Success)
            {
                return CreateCommandExecuteToolResult(McpToolOutcomeMapper.FromPreflightResult(preflight), command, operationStarted: false, operationId: null);
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        var start = _operationCoordinator.Start(
            McpAutomationOperationKind.Play,
            token => ExecuteParsedPlayAsync(options, token),
            CancellationToken.None);
        return CreateCommandExecuteToolResult(
            start.Error ?? McpToolOutcomeMapper.Success("Command operation started."),
            command,
            operationStarted: start.Started,
            operationId: start.Operation?.OperationId);
    }

    private async Task<CallToolResult> StartParsedRunCommandAsync(RunCliOptions options, string command, CancellationToken cancellationToken)
    {
        var normalizedStepFilePath = options.StepFilePath;
        if (options.StepFilePath is not null)
        {
            var fileReadCapability = RequireCapability(McpCapability.FileRead);
            if (fileReadCapability is not null)
            {
                return CreateCommandExecuteToolResult(fileReadCapability, command, operationStarted: false, operationId: null);
            }

            if (!_pathPolicy.TryAuthorize(options.StepFilePath, McpPathKind.FileRead, requireExisting: true, out normalizedStepFilePath, out var stepPathError))
            {
                return CreateCommandExecuteToolResult(stepPathError, command, operationStarted: false, operationId: null);
            }
        }

        if (options.StepFilePath is not null)
        {
            options = options with { StepFilePath = normalizedStepFilePath };

            var shellCapability = RequireCapability(McpCapability.ShellExecute);
            if (shellCapability is not null)
            {
                return CreateCommandExecuteToolResult(shellCapability, command, operationStarted: false, operationId: null);
            }
        }

        options = options with { TimeoutSeconds = GetMcpAutomationTimeoutSeconds(options.TimeoutSeconds) };

        var inlineShellCapability = RequireShellCapability(options.Steps);
        if (inlineShellCapability is not null)
        {
            return CreateCommandExecuteToolResult(inlineShellCapability, command, operationStarted: false, operationId: null);
        }

        if (options.ImageAssets is not null)
        {
            var fileReadCapability = RequireCapability(McpCapability.FileRead);
            if (fileReadCapability is not null)
            {
                return CreateCommandExecuteToolResult(fileReadCapability, command, operationStarted: false, operationId: null);
            }

            var normalizedAssets = new List<RunImageAssetCliOption>(options.ImageAssets.Count);
            foreach (var asset in options.ImageAssets)
            {
                if (!TryAuthorizeImageOrMacroReadPath(asset.FilePath, out var normalizedAssetPath, out var assetPathError))
                {
                    return CreateCommandExecuteToolResult(assetPathError, command, operationStarted: false, operationId: null);
                }

                normalizedAssets.Add(asset with { FilePath = normalizedAssetPath });
            }

            options = options with { ImageAssets = normalizedAssets };
        }

        if (!options.DryRun)
        {
            var preflight = await _cliPreflightService.CheckAsync(CliPreflightTarget.Run, cancellationToken).ConfigureAwait(false);
            if (!preflight.Success)
            {
                return CreateCommandExecuteToolResult(McpToolOutcomeMapper.FromPreflightResult(preflight), command, operationStarted: false, operationId: null);
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        var start = _operationCoordinator.Start(
            McpAutomationOperationKind.Run,
            token => ExecuteParsedRunAsync(options, token),
            CancellationToken.None);
        return CreateCommandExecuteToolResult(
            start.Error ?? McpToolOutcomeMapper.Success("Command operation started."),
            command,
            operationStarted: start.Started,
            operationId: start.Operation?.OperationId);
    }

    private async Task<CallToolResult> StartParsedRecordCommandAsync(RecordCliOptions options, string command, CancellationToken cancellationToken)
    {
        var fileWriteCapability = RequireCapability(McpCapability.FileWrite);
        if (fileWriteCapability is not null)
        {
            return CreateCommandExecuteToolResult(fileWriteCapability, command, operationStarted: false, operationId: null);
        }

        if (!TryNormalizeRecordingOutputPath(options.OutputFilePath, out var normalizedOutputPath, out var pathError))
        {
            return CreateCommandExecuteToolResult(pathError, command, operationStarted: false, operationId: null);
        }

        options = options with
        {
            OutputFilePath = normalizedOutputPath,
            DurationSeconds = GetMcpAutomationTimeoutSeconds(options.DurationSeconds),
        };
        var preflight = await _cliPreflightService.CheckAsync(CliPreflightTarget.Record, cancellationToken).ConfigureAwait(false);
        if (!preflight.Success)
        {
            return CreateCommandExecuteToolResult(McpToolOutcomeMapper.FromPreflightResult(preflight), command, operationStarted: false, operationId: null);
        }

        cancellationToken.ThrowIfCancellationRequested();
        var start = _operationCoordinator.Start(
            McpAutomationOperationKind.Record,
            token => ExecuteParsedRecordAsync(options, token),
            CancellationToken.None);
        return CreateCommandExecuteToolResult(
            start.Error ?? McpToolOutcomeMapper.Success("Command operation started."),
            command,
            operationStarted: start.Started,
            operationId: start.Operation?.OperationId);
    }

    private async Task<CliCommandExecutionResult> ExecuteParsedPlayAsync(PlayCliOptions options, CancellationToken cancellationToken)
    {
        var result = await RunWithTimeoutAsync(
            options.TimeoutSeconds,
            token => _macroExecutionService.ExecuteAsync(new MacroExecutionRequest
            {
                MacroFilePath = options.MacroFilePath,
                SpeedMultiplier = options.SpeedMultiplier,
                Loop = options.Loop || options.RepeatCount is not 1,
                RepeatCount = options.RepeatCount,
                RepeatDelayMs = options.RepeatDelayMs,
                MotionMode = options.MotionMode,
                StrictSpeedMotionEventsPerSecond = options.StrictSpeedMotionEventsPerSecond,
                PrecisionMotionEventsPerSecond = options.PrecisionMotionEventsPerSecond,
                MaximumMotionErrorPixels = options.MaximumMotionErrorPixels,
                CountdownSeconds = options.CountdownSeconds,
                DryRun = options.DryRun,
            }, token),
            cancellationToken).ConfigureAwait(false);
        return ToCliResult(result);
    }

    private async Task<CliCommandExecutionResult> ExecuteParsedRunAsync(RunCliOptions options, CancellationToken cancellationToken)
    {
        var result = await RunWithTimeoutAsync(
            options.TimeoutSeconds,
            token => _runScriptExecutionService.ExecuteAsync(new RunCliExecutionRequest
            {
                Steps = options.Steps,
                StepFilePath = options.StepFilePath,
                SpeedMultiplier = options.SpeedMultiplier,
                CountdownSeconds = options.CountdownSeconds,
                DryRun = options.DryRun,
                ImageAssets = options.ImageAssets ?? [],
            }, token),
            cancellationToken).ConfigureAwait(false);
        return ToCliResult(result);
    }

    private async Task<CliCommandExecutionResult> ExecuteParsedRecordAsync(RecordCliOptions options, CancellationToken cancellationToken)
    {
        var result = await _recordExecutionService.ExecuteAsync(new RecordExecutionRequest
        {
            OutputFilePath = options.OutputFilePath,
            RecordMouse = options.RecordMouse,
            RecordKeyboard = options.RecordKeyboard,
            CoordinateMode = options.CoordinateMode,
            SkipInitialZero = options.SkipInitialZero,
            DurationSeconds = options.DurationSeconds,
        }, cancellationToken).ConfigureAwait(false);
        return ToCliResult(result);
    }

    private async Task<CallToolResult> StartPlayAutomationAsync(
        string? macroPath,
        IReadOnlyList<string>? steps,
        string? outputPath,
        string? stepFilePath,
        IReadOnlyList<McpRunImageAsset>? imageAssets,
        double? speedMultiplier,
        bool loop,
        int? repeatCount,
        int? repeatDelayMs,
        int? countdownSeconds,
        int? timeoutSeconds,
        int? durationSeconds,
        bool dryRun,
        bool? recordMouse,
        bool? recordKeyboard,
        string? coordinateMode,
        string? motionMode,
        int? strictSpeedMotionEventsPerSecond,
        int? precisionMotionEventsPerSecond,
        double? maximumMotionErrorPixels,
        bool skipInitialZero,
        CancellationToken cancellationToken)
    {
        if (steps is not null || outputPath is not null || stepFilePath is not null || imageAssets is not null || durationSeconds is not null || recordMouse is not null || recordKeyboard is not null || coordinateMode is not null || skipInitialZero)
        {
            return CreateAutomationStartToolResult(
                McpToolOutcomeMapper.InvalidArguments("Play automation accepts macroPath and playback options only."),
                operation: null);
        }

        var macroReadCapability = RequireCapability(McpCapability.MacroRead);
        if (macroReadCapability is not null)
        {
            return CreateAutomationStartToolResult(macroReadCapability, operation: null);
        }

        if (!TryNormalizeMacroPath(macroPath ?? string.Empty, out var normalizedMacroPath, out var pathError))
        {
            return CreateAutomationStartToolResult(pathError, operation: null);
        }

        if (!TryGetAutomationPlaybackOptions(
                speedMultiplier,
                loop,
                repeatCount,
                repeatDelayMs,
                countdownSeconds,
                timeoutSeconds,
                motionMode,
                strictSpeedMotionEventsPerSecond,
                precisionMotionEventsPerSecond,
                maximumMotionErrorPixels,
                out var playbackOptions,
                out var optionsError))
        {
            return CreateAutomationStartToolResult(optionsError, operation: null);
        }

        if (!dryRun)
        {
            var preflight = await _cliPreflightService.CheckAsync(CliPreflightTarget.Play, cancellationToken).ConfigureAwait(false);
            if (!preflight.Success)
            {
                return CreateAutomationStartToolResult(McpToolOutcomeMapper.FromPreflightResult(preflight), operation: null);
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        var start = _operationCoordinator.Start(
            McpAutomationOperationKind.Play,
            token => ExecutePlayAsync(normalizedMacroPath, playbackOptions, dryRun, token),
            CancellationToken.None);
        return CreateAutomationStartToolResult(start.Error ?? McpToolOutcomeMapper.Success("Automation operation started."), start.Operation);
    }

    private async Task<CallToolResult> StartRunAutomationAsync(
        string? macroPath,
        IReadOnlyList<string>? steps,
        string? outputPath,
        string? stepFilePath,
        IReadOnlyList<McpRunImageAsset>? imageAssets,
        double? speedMultiplier,
        bool loop,
        int? repeatCount,
        int? repeatDelayMs,
        int? countdownSeconds,
        int? timeoutSeconds,
        int? durationSeconds,
        bool dryRun,
        bool? recordMouse,
        bool? recordKeyboard,
        string? coordinateMode,
        string? motionMode,
        int? strictSpeedMotionEventsPerSecond,
        int? precisionMotionEventsPerSecond,
        double? maximumMotionErrorPixels,
        bool skipInitialZero,
        CancellationToken cancellationToken)
    {
        if (macroPath is not null || outputPath is not null || motionMode is not null || strictSpeedMotionEventsPerSecond is not null || precisionMotionEventsPerSecond is not null || maximumMotionErrorPixels is not null || loop || repeatCount is not null || repeatDelayMs is not null
            || durationSeconds is not null || recordMouse is not null || recordKeyboard is not null || coordinateMode is not null || skipInitialZero)
        {
            return CreateAutomationStartToolResult(
                McpToolOutcomeMapper.InvalidArguments("Run automation accepts steps and run options only."),
                operation: null);
        }

        var commandExecutionCapability = RequireCapability(McpCapability.CommandExecute);
        if (commandExecutionCapability is not null)
        {
            return CreateAutomationStartToolResult(commandExecutionCapability, operation: null);
        }

        if (!TryGetAutomationRunOptions(speedMultiplier, countdownSeconds, timeoutSeconds, out var runOptions, out var optionsError))
        {
            return CreateAutomationStartToolResult(optionsError, operation: null);
        }

        IReadOnlyList<string> normalizedSteps = [];
        if (steps is not null && !TryValidateAutomationSteps(steps, out normalizedSteps, out var stepsError))
        {
            return CreateAutomationStartToolResult(stepsError, operation: null);
        }

        if (normalizedSteps.Count is 0 && string.IsNullOrWhiteSpace(stepFilePath))
        {
            return CreateAutomationStartToolResult(
                McpToolOutcomeMapper.InvalidArguments("Run automation requires steps or stepFilePath."),
                operation: null);
        }

        var shellCapability = RequireShellCapability(normalizedSteps);
        if (shellCapability is not null)
        {
            return CreateAutomationStartToolResult(shellCapability, operation: null);
        }

        var normalizedStepFilePath = stepFilePath;
        if (stepFilePath is not null)
        {
            var fileReadCapability = RequireCapability(McpCapability.FileRead);
            if (fileReadCapability is not null)
            {
                return CreateAutomationStartToolResult(fileReadCapability, operation: null);
            }

            if (!_pathPolicy.TryAuthorize(stepFilePath, McpPathKind.FileRead, requireExisting: true, out normalizedStepFilePath, out var stepPathError))
            {
                return CreateAutomationStartToolResult(stepPathError, operation: null);
            }

            var stepFileShellCapability = RequireCapability(McpCapability.ShellExecute);
            if (stepFileShellCapability is not null)
            {
                return CreateAutomationStartToolResult(stepFileShellCapability, operation: null);
            }
        }

        var normalizedAssets = NormalizeRunImageAssets(imageAssets);
        if (!normalizedAssets.Success)
        {
            return CreateAutomationStartToolResult(normalizedAssets.Error!, operation: null);
        }

        if (!dryRun)
        {
            var preflight = await _cliPreflightService.CheckAsync(CliPreflightTarget.Run, cancellationToken).ConfigureAwait(false);
            if (!preflight.Success)
            {
                return CreateAutomationStartToolResult(McpToolOutcomeMapper.FromPreflightResult(preflight), operation: null);
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        var start = _operationCoordinator.Start(
            McpAutomationOperationKind.Run,
            token => ExecuteRunAsync(normalizedSteps, normalizedStepFilePath, normalizedAssets.Assets, runOptions, dryRun, token),
            CancellationToken.None);
        return CreateAutomationStartToolResult(start.Error ?? McpToolOutcomeMapper.Success("Automation operation started."), start.Operation);
    }

    private async Task<CallToolResult> StartRecordAutomationAsync(
        string? macroPath,
        IReadOnlyList<string>? steps,
        string? outputPath,
        string? stepFilePath,
        IReadOnlyList<McpRunImageAsset>? imageAssets,
        double? speedMultiplier,
        bool loop,
        int? repeatCount,
        int? repeatDelayMs,
        int? countdownSeconds,
        int? timeoutSeconds,
        int? durationSeconds,
        bool dryRun,
        bool? recordMouse,
        bool? recordKeyboard,
        string? coordinateMode,
        string? motionMode,
        int? strictSpeedMotionEventsPerSecond,
        int? precisionMotionEventsPerSecond,
        double? maximumMotionErrorPixels,
        bool skipInitialZero,
        CancellationToken cancellationToken)
    {
        if (macroPath is not null || steps is not null || stepFilePath is not null || imageAssets is not null || speedMultiplier is not null || motionMode is not null || strictSpeedMotionEventsPerSecond is not null || precisionMotionEventsPerSecond is not null || maximumMotionErrorPixels is not null || loop || repeatCount is not null || repeatDelayMs is not null
            || countdownSeconds is not null || timeoutSeconds is not null || dryRun)
        {
            return CreateAutomationStartToolResult(
                McpToolOutcomeMapper.InvalidArguments("Record automation accepts outputPath and recording options only."),
                operation: null);
        }

        var fileWriteCapability = RequireCapability(McpCapability.FileWrite);
        if (fileWriteCapability is not null)
        {
            return CreateAutomationStartToolResult(fileWriteCapability, operation: null);
        }

        if (!TryNormalizeRecordingOutputPath(outputPath, out var normalizedOutputPath, out var pathError))
        {
            return CreateAutomationStartToolResult(pathError, operation: null);
        }

        if (!TryGetAutomationRecordingOptions(recordMouse, recordKeyboard, coordinateMode, skipInitialZero, durationSeconds, out var recordingOptions, out var optionsError))
        {
            return CreateAutomationStartToolResult(optionsError, operation: null);
        }

        var preflight = await _cliPreflightService.CheckAsync(CliPreflightTarget.Record, cancellationToken).ConfigureAwait(false);
        if (!preflight.Success)
        {
            return CreateAutomationStartToolResult(McpToolOutcomeMapper.FromPreflightResult(preflight), operation: null);
        }

        cancellationToken.ThrowIfCancellationRequested();
        var start = _operationCoordinator.Start(
            McpAutomationOperationKind.Record,
            token => ExecuteRecordAsync(normalizedOutputPath, recordingOptions, token),
            CancellationToken.None);
        return CreateAutomationStartToolResult(start.Error ?? McpToolOutcomeMapper.Success("Automation operation started."), start.Operation);
    }

    private async Task<CliCommandExecutionResult> ExecutePlayAsync(
        string macroPath,
        AutomationPlaybackOptions options,
        bool dryRun,
        CancellationToken cancellationToken)
    {
        var result = await RunWithTimeoutAsync(
            options.TimeoutSeconds,
            token => _macroExecutionService.ExecuteAsync(new MacroExecutionRequest
            {
                MacroFilePath = macroPath,
                SpeedMultiplier = options.SpeedMultiplier,
                Loop = options.Loop,
                RepeatCount = options.RepeatCount,
                RepeatDelayMs = options.RepeatDelayMs,
                MotionMode = options.MotionMode,
                StrictSpeedMotionEventsPerSecond = options.StrictSpeedMotionEventsPerSecond,
                PrecisionMotionEventsPerSecond = options.PrecisionMotionEventsPerSecond,
                MaximumMotionErrorPixels = options.MaximumMotionErrorPixels,
                CountdownSeconds = options.CountdownSeconds,
                DryRun = dryRun,
            }, token),
            cancellationToken).ConfigureAwait(false);
        return ToCliResult(result);
    }

    private async Task<CliCommandExecutionResult> ExecuteRunAsync(
        IReadOnlyList<string> steps,
        string? stepFilePath,
        IReadOnlyList<RunImageAssetCliOption> imageAssets,
        AutomationRunOptions options,
        bool dryRun,
        CancellationToken cancellationToken)
    {
        var result = await RunWithTimeoutAsync(
            options.TimeoutSeconds,
            token => _runScriptExecutionService.ExecuteAsync(new RunCliExecutionRequest
            {
                Steps = steps,
                StepFilePath = stepFilePath,
                SpeedMultiplier = options.SpeedMultiplier,
                CountdownSeconds = options.CountdownSeconds,
                DryRun = dryRun,
                ImageAssets = imageAssets,
            }, token),
            cancellationToken).ConfigureAwait(false);
        return ToCliResult(result);
    }

    private async Task<CliCommandExecutionResult> ExecuteRecordAsync(
        string outputPath,
        AutomationRecordingOptions options,
        CancellationToken cancellationToken)
    {
        var result = await _recordExecutionService.ExecuteAsync(new RecordExecutionRequest
        {
            OutputFilePath = outputPath,
            RecordMouse = options.RecordMouse,
            RecordKeyboard = options.RecordKeyboard,
            CoordinateMode = options.CoordinateMode,
            SkipInitialZero = options.SkipInitialZero,
            DurationSeconds = options.DurationSeconds,
        }, cancellationToken).ConfigureAwait(false);
        return ToCliResult(result);
    }

    private static async Task<MacroExecutionResult> RunWithTimeoutAsync(
        int timeoutSeconds,
        Func<CancellationToken, Task<MacroExecutionResult>> executeAsync,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeoutSeconds, 0);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
        try
        {
            var result = await executeAsync(timeout.Token).ConfigureAwait(false);
            return timeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested
                ? TimedOutResult()
                : result;
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            return TimedOutResult();
        }
    }

    private static CliCommandExecutionResult ToCliResult(MacroExecutionResult result)
    {
        return result.Success
            ? CliCommandExecutionResult.Ok(result.Message, result.Data, result.Warnings)
            : CliCommandExecutionResult.Fail(result.ExitCode, result.Message, result.Errors, result.Warnings, result.Data);
    }

    private static CliCommandExecutionResult ToCliResult(RecordExecutionResult result)
    {
        return result.Success
            ? CliCommandExecutionResult.Ok(result.Message, result.Data, result.Warnings)
            : CliCommandExecutionResult.Fail(result.ExitCode, result.Message, result.Errors, result.Warnings, result.Data);
    }

    private static MacroExecutionResult TimedOutResult() => new()
    {
        Success = false,
        ExitCode = CliExitCode.RuntimeError,
        Message = "Automation operation timed out.",
    };

    private static bool TryGetAutomationPlaybackOptions(
        double? speedMultiplier,
        bool loop,
        int? repeatCount,
        int? repeatDelayMs,
        int? countdownSeconds,
        int? timeoutSeconds,
        string? motionMode,
        int? strictSpeedMotionEventsPerSecond,
        int? precisionMotionEventsPerSecond,
        double? maximumMotionErrorPixels,
        out AutomationPlaybackOptions options,
        out McpToolOutcome error)
    {
        options = new AutomationPlaybackOptions(
            SpeedMultiplier: 1,
            Loop: false,
            RepeatCount: 1,
            RepeatDelayMs: 0,
            CountdownSeconds: 0,
            TimeoutSeconds: DefaultAutomationTimeoutSeconds,
            MotionMode: MotionPlaybackMode.Precision,
            StrictSpeedMotionEventsPerSecond: PlaybackOptions.DefaultStrictSpeedMotionEventsPerSecond,
            PrecisionMotionEventsPerSecond: PlaybackOptions.DefaultPrecisionMotionEventsPerSecond,
            MaximumMotionErrorPixels: PlaybackOptions.DefaultMaximumMotionErrorPixels);
        var speed = speedMultiplier ?? 1d;
        if (!double.IsFinite(speed) || speed is < PlaybackOptions.MinSpeedMultiplier or > PlaybackOptions.MaxSpeedMultiplier)
        {
            error = McpToolOutcomeMapper.InvalidArguments("Automation speedMultiplier must be a finite number between 0.1 and 10.");
            return false;
        }

        var repeat = repeatCount ?? 1;
        if (repeat < 0)
        {
            error = McpToolOutcomeMapper.InvalidArguments("Automation repeatCount must be non-negative.");
            return false;
        }

        if (repeat is 0 && !loop)
        {
            error = McpToolOutcomeMapper.InvalidArguments("Automation repeatCount of 0 requires loop to be true.");
            return false;
        }

        var repeatDelay = repeatDelayMs ?? 0;
        if (repeatDelay is < 0 or > MaximumAutomationRepeatDelayMs)
        {
            error = McpToolOutcomeMapper.InvalidArguments("Automation repeatDelayMs must be between 0 and 3600000.");
            return false;
        }

        if (!TryGetBoundedAutomationSeconds(countdownSeconds, "countdownSeconds", defaultValue: 0, allowZero: true, out var countdown, out error)
            || !TryGetBoundedAutomationSeconds(timeoutSeconds, "timeoutSeconds", DefaultAutomationTimeoutSeconds, allowZero: false, out var timeout, out error))
        {
            return false;
        }

        var parsedMotionMode = motionMode?.Trim().ToLowerInvariant() switch
        {
            null or "" or "precision" => MotionPlaybackMode.Precision,
            "strict-speed" or "strictspeed" => MotionPlaybackMode.StrictSpeed,
            _ => (MotionPlaybackMode)(-1),
        };
        if (!Enum.IsDefined(parsedMotionMode))
        {
            error = McpToolOutcomeMapper.InvalidArguments("Automation motionMode must be precision or strict-speed.");
            return false;
        }

        var strictRate = strictSpeedMotionEventsPerSecond ?? PlaybackOptions.DefaultStrictSpeedMotionEventsPerSecond;
        var precisionRate = precisionMotionEventsPerSecond ?? PlaybackOptions.DefaultPrecisionMotionEventsPerSecond;
        var motionError = maximumMotionErrorPixels ?? PlaybackOptions.DefaultMaximumMotionErrorPixels;
        if (strictRate is < PlaybackOptions.MinStrictSpeedMotionEventsPerSecond or > PlaybackOptions.MaxStrictSpeedMotionEventsPerSecond
            || precisionRate is < PlaybackOptions.MinPrecisionMotionEventsPerSecond or > PlaybackOptions.MaxPrecisionMotionEventsPerSecond
            || !double.IsFinite(motionError) || motionError is < PlaybackOptions.MinMaximumMotionErrorPixels or > PlaybackOptions.MaxMaximumMotionErrorPixels)
        {
            error = McpToolOutcomeMapper.InvalidArguments("Automation motion options are outside the supported ranges.");
            return false;
        }

        options = new AutomationPlaybackOptions(speed, loop || repeat is not 1, repeat, repeatDelay, countdown, timeout, parsedMotionMode, strictRate, precisionRate, motionError);
        return true;
    }

    private ResultWithAssets NormalizeRunImageAssets(IReadOnlyList<McpRunImageAsset>? assets)
    {
        if (assets is null or { Count: 0 })
        {
            return new(Success: true, Assets: [], Error: null);
        }

        if (assets.Count > 100)
        {
            return new(Success: false, Assets: [], Error: McpToolOutcomeMapper.InvalidArguments("Run image assets exceed the maximum count."));
        }

        var normalized = new List<RunImageAssetCliOption>(assets.Count);
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var asset in assets)
        {
            if (string.IsNullOrWhiteSpace(asset.Name) || !asset.Name.All(static character => char.IsAsciiLetterOrDigit(character) || character is '_') || !char.IsAsciiLetter(asset.Name[0]))
            {
                return new(Success: false, Assets: [], Error: McpToolOutcomeMapper.InvalidArguments("Run image asset names must match [A-Za-z_][A-Za-z0-9_]*."));
            }

            if (!names.Add(asset.Name))
            {
                return new(Success: false, Assets: [], Error: McpToolOutcomeMapper.InvalidArguments("Run image asset names must be unique."));
            }

            if (!TryAuthorizeImageOrMacroReadPath(asset.FilePath, out var path, out var pathError))
            {
                return new(Success: false, Assets: [], Error: pathError);
            }

            normalized.Add(new RunImageAssetCliOption(asset.Name, path));
        }

        return new(Success: true, Assets: normalized, Error: null);
    }

    private static bool TryGetAutomationRunOptions(
        double? speedMultiplier,
        int? countdownSeconds,
        int? timeoutSeconds,
        out AutomationRunOptions options,
        out McpToolOutcome error)
    {
        options = new AutomationRunOptions(
            SpeedMultiplier: 1,
            CountdownSeconds: 0,
            TimeoutSeconds: DefaultAutomationTimeoutSeconds);
        var speed = speedMultiplier ?? 1d;
        if (!double.IsFinite(speed) || speed is < PlaybackOptions.MinSpeedMultiplier or > PlaybackOptions.MaxSpeedMultiplier)
        {
            error = McpToolOutcomeMapper.InvalidArguments("Automation speedMultiplier must be a finite number between 0.1 and 10.");
            return false;
        }

        if (!TryGetBoundedAutomationSeconds(countdownSeconds, "countdownSeconds", defaultValue: 0, allowZero: true, out var countdown, out error)
            || !TryGetBoundedAutomationSeconds(timeoutSeconds, "timeoutSeconds", DefaultAutomationTimeoutSeconds, allowZero: false, out var timeout, out error))
        {
            return false;
        }

        options = new AutomationRunOptions(speed, countdown, timeout);
        return true;
    }

    private static bool TryGetAutomationRecordingOptions(
        bool? recordMouse,
        bool? recordKeyboard,
        string? coordinateMode,
        bool skipInitialZero,
        int? durationSeconds,
        out AutomationRecordingOptions options,
        out McpToolOutcome error)
    {
        var mouse = recordMouse ?? true;
        var keyboard = recordKeyboard ?? true;
        var duration = durationSeconds ?? DefaultAutomationTimeoutSeconds;
        options = new AutomationRecordingOptions(mouse, keyboard, RecordCoordinateMode.Auto, skipInitialZero, duration);
        if (!mouse && !keyboard)
        {
            error = McpToolOutcomeMapper.InvalidArguments("Automation recording requires mouse or keyboard capture.");
            return false;
        }

        var mode = coordinateMode?.Trim().ToLowerInvariant() switch
        {
            null or "" or "auto" => RecordCoordinateMode.Auto,
            "absolute" => RecordCoordinateMode.Absolute,
            "relative" => RecordCoordinateMode.Relative,
            _ => (RecordCoordinateMode)(-1),
        };
        if (!Enum.IsDefined(mode))
        {
            error = McpToolOutcomeMapper.InvalidArguments("Automation coordinateMode must be auto, absolute, or relative.");
            return false;
        }

        if (duration is <= 0 or > MaximumAutomationRecordDurationSeconds)
        {
            error = McpToolOutcomeMapper.InvalidArguments("Automation durationSeconds must be between 1 and 3600.");
            return false;
        }

        options = new AutomationRecordingOptions(mouse, keyboard, mode, skipInitialZero, duration);
        error = McpToolOutcomeMapper.Success(string.Empty);
        return true;
    }

    private static bool TryGetBoundedAutomationSeconds(
        int? value,
        string argumentName,
        int defaultValue,
        bool allowZero,
        out int seconds,
        out McpToolOutcome error)
    {
        seconds = value ?? defaultValue;
        if (seconds is < 0 or > MaximumAutomationTimeoutSeconds || (!allowZero && seconds is 0))
        {
            var minimum = allowZero ? 0 : 1;
            error = McpToolOutcomeMapper.InvalidArguments(
                $"Automation {argumentName} must be between {minimum.ToString(System.Globalization.CultureInfo.InvariantCulture)} and 3600.");
            return false;
        }

        error = McpToolOutcomeMapper.Success(string.Empty);
        return true;
    }

    private static bool TryValidateAutomationSteps(
        IReadOnlyList<string>? steps,
        out IReadOnlyList<string> normalizedSteps,
        out McpToolOutcome error)
    {
        normalizedSteps = [];
        if (steps is null || steps.Count is 0)
        {
            error = McpToolOutcomeMapper.InvalidArguments("Run automation requires at least one step.");
            return false;
        }

        if (steps.Count > MaximumAutomationStepCount)
        {
            error = McpToolOutcomeMapper.InvalidArguments("Run automation exceeds the maximum step count.");
            return false;
        }

        var totalCharacters = 0;
        var normalized = new string[steps.Count];
        for (var index = 0; index < steps.Count; index++)
        {
            var step = steps[index];
            if (string.IsNullOrWhiteSpace(step) || step.Length > MaximumAutomationStepCharacters)
            {
                error = McpToolOutcomeMapper.InvalidArguments("Run automation steps must be non-empty and at most 16384 characters.");
                return false;
            }

            totalCharacters = checked(totalCharacters + step.Length);
            if (totalCharacters > MaximumAutomationStepPayloadCharacters)
            {
                error = McpToolOutcomeMapper.InvalidArguments("Run automation steps exceed the maximum payload size.");
                return false;
            }

            normalized[index] = step;
        }

        normalizedSteps = normalized;
        error = McpToolOutcomeMapper.Success(string.Empty);
        return true;
    }

    private bool TryNormalizeRecordingOutputPath(string? outputPath, out string normalizedOutputPath, out McpToolOutcome error)
    {
        normalizedOutputPath = string.Empty;
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            error = McpToolOutcomeMapper.InvalidArguments("Recording outputPath is required.");
            return false;
        }

        if (!Path.IsPathFullyQualified(outputPath))
        {
            error = McpToolOutcomeMapper.InvalidArguments("Recording outputPath must be absolute.");
            return false;
        }

        if (!string.Equals(Path.GetExtension(outputPath), ".macro", StringComparison.OrdinalIgnoreCase))
        {
            error = McpToolOutcomeMapper.InvalidArguments("Recording outputPath must use the .macro extension.");
            return false;
        }

        try
        {
            if (!_pathPolicy.TryAuthorize(outputPath, McpPathKind.MacroWrite, requireExisting: false, out normalizedOutputPath, out error))
            {
                return false;
            }

            if (Directory.Exists(normalizedOutputPath))
            {
                error = McpToolOutcomeMapper.InvalidArguments("Recording outputPath must refer to a file.");
                return false;
            }

            if (File.Exists(normalizedOutputPath)
                && new FileInfo(normalizedOutputPath).Attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                error = McpToolOutcomeMapper.InvalidArguments("Recording outputPath must not be a symbolic link.");
                return false;
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            error = McpToolOutcomeMapper.FileError("Recording outputPath could not be accessed.");
            return false;
        }

        error = McpToolOutcomeMapper.Success(string.Empty);
        return true;
    }

    private static bool IsValidOperationId(string operationId) =>
        operationId.Length is 32 && operationId.All(static character => char.IsAsciiHexDigit(character));

    private sealed record AutomationPlaybackOptions(
        double SpeedMultiplier,
        bool Loop,
        int RepeatCount,
        int RepeatDelayMs,
        int CountdownSeconds,
        int TimeoutSeconds,
        MotionPlaybackMode MotionMode,
        int StrictSpeedMotionEventsPerSecond,
        int PrecisionMotionEventsPerSecond,
        double MaximumMotionErrorPixels);

    private sealed record AutomationRunOptions(
        double SpeedMultiplier,
        int CountdownSeconds,
        int TimeoutSeconds);

    private sealed record ResultWithAssets(bool Success, IReadOnlyList<RunImageAssetCliOption> Assets, McpToolOutcome? Error);

    private sealed record AutomationRecordingOptions(
        bool RecordMouse,
        bool RecordKeyboard,
        RecordCoordinateMode CoordinateMode,
        bool SkipInitialZero,
        int DurationSeconds);

    [McpServerTool(
        Name = "macro.list",
        Title = "List macro files",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        UseStructuredContent = true,
        OutputSchemaType = typeof(McpMacroListResult))]
    [Description("Lists up to 100 regular .macro files directly within an absolute directory path.")]
    public CallToolResult ListMacros(string directoryPath, CancellationToken cancellationToken)
    {
        var capability = RequireCapability(McpCapability.MacroRead);
        if (capability is not null)
        {
            return CreateListToolResult(
                directoryPath: directoryPath,
                macros: [],
                isTruncated: false,
                outcome: capability);
        }

        if (!TryNormalizeDirectoryPath(directoryPath, out var normalizedDirectoryPath, out var error))
        {
            return CreateListToolResult(
                directoryPath: directoryPath,
                macros: [],
                isTruncated: false,
                outcome: error);
        }

        var macros = new List<McpMacroFile>(MaximumMacroListCount + 1);

        try
        {
            foreach (var filePath in Directory.EnumerateFiles(normalizedDirectoryPath, "*.macro", SearchOption.TopDirectoryOnly))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var fileInfo = new FileInfo(filePath);
                if (fileInfo.Attributes.HasFlag(FileAttributes.Directory)
                    || fileInfo.Attributes.HasFlag(FileAttributes.ReparsePoint))
                {
                    continue;
                }

                macros.Add(new McpMacroFile(
                    fileInfo.FullName,
                    fileInfo.Name,
                    fileInfo.Length,
                    new DateTimeOffset(fileInfo.LastWriteTimeUtc, TimeSpan.Zero)));

                if (macros.Count > MaximumMacroListCount)
                {
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return CreateListToolResult(
                directoryPath: normalizedDirectoryPath,
                macros: [],
                isTruncated: false,
                outcome: McpToolOutcomeMapper.FileError("Macro directory could not be listed."));
        }

        var ordered = macros
            .OrderBy(static macro => macro.FileName, StringComparer.Ordinal)
            .Take(MaximumMacroListCount)
            .ToArray();
        return CreateListToolResult(
            directoryPath: normalizedDirectoryPath,
            macros: ordered,
            isTruncated: macros.Count > MaximumMacroListCount,
            outcome: McpToolOutcomeMapper.Success("Macro files listed."));
    }

    [McpServerTool(
        Name = "macro.inspect",
        Title = "Inspect a macro",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        UseStructuredContent = true,
        OutputSchemaType = typeof(McpMacroInspectResult))]
    [Description("Reads macro metadata and validation diagnostics without returning macro events, script steps, or embedded assets.")]
    public async Task<CallToolResult> InspectMacroAsync(string macroPath, CancellationToken cancellationToken)
    {
        var capability = RequireCapability(McpCapability.MacroRead);
        if (capability is not null)
        {
            return CreateInspectToolResult(capability, macro: null);
        }

        if (!TryNormalizeMacroPath(macroPath, out var normalizedMacroPath, out var error))
        {
            return CreateInspectToolResult(outcome: error, macro: null);
        }

        var result = await _macroExecutionService
            .GetInfoAsync(normalizedMacroPath, cancellationToken)
            .ConfigureAwait(false);
        return CreateInspectToolResult(
            outcome: McpToolOutcomeMapper.FromMacroResult(result),
            macro: ToMacroInfo(result.Data));
    }

    [McpServerTool(
        Name = "macro.validate",
        Title = "Validate a macro",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        UseStructuredContent = true,
        OutputSchemaType = typeof(McpMacroValidateResult))]
    [Description("Validates a macro without playing it.")]
    public async Task<CallToolResult> ValidateMacroAsync(string macroPath, CancellationToken cancellationToken)
    {
        var capability = RequireCapability(McpCapability.MacroRead);
        if (capability is not null)
        {
            return CreateValidateToolResult(capability, macro: null);
        }

        if (!TryNormalizeMacroPath(macroPath, out var normalizedMacroPath, out var error))
        {
            return CreateValidateToolResult(outcome: error, macro: null);
        }

        var result = await _macroExecutionService
            .ValidateAsync(normalizedMacroPath, cancellationToken)
            .ConfigureAwait(false);
        return CreateValidateToolResult(
            outcome: McpToolOutcomeMapper.FromMacroResult(result),
            macro: ToMacroSummary(result.Data));
    }

    [McpServerTool(
        Name = "clipboard.get_text",
        Title = "Read text clipboard",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        UseStructuredContent = true,
        OutputSchemaType = typeof(McpClipboardTextResult))]
    [Description("Reads up to 65,536 characters of text from the system clipboard.")]
    public async Task<CallToolResult> GetClipboardTextAsync(CancellationToken cancellationToken)
    {
        var capability = RequireCapability(McpCapability.ClipboardRead);
        if (capability is not null)
        {
            return CreateClipboardTextToolResult(capability, text: null, length: null);
        }

        var result = await _clipboardCliService.GetAsync(cancellationToken).ConfigureAwait(false);
        var outcome = McpToolOutcomeMapper.FromCliResultRedactingErrorDetails(result);
        if (!outcome.Success)
        {
            return CreateClipboardTextToolResult(outcome, text: null, length: null);
        }

        if (result.Data is not ClipboardTextData clipboardText)
        {
            return CreateClipboardTextToolResult(
                McpToolOutcomeMapper.RuntimeError("Clipboard text could not be read."),
                text: null,
                length: null);
        }

        if (clipboardText.Value is null)
        {
            return CreateClipboardTextToolResult(
                McpToolOutcomeMapper.RuntimeError("Clipboard text could not be read."),
                text: null,
                length: null);
        }

        if (clipboardText.Value.Length > MaximumClipboardTextCharacters)
        {
            return CreateClipboardTextToolResult(
                McpToolOutcomeMapper.RuntimeError("Clipboard text exceeds the maximum allowed length."),
                text: null,
                length: null);
        }

        return CreateClipboardTextToolResult(outcome, clipboardText.Value, clipboardText.Value.Length);
    }

    [McpServerTool(
        Name = "clipboard.get_image",
        Title = "Read image clipboard",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        UseStructuredContent = true,
        OutputSchemaType = typeof(McpClipboardImageResult))]
    [Description("Reads a validated PNG clipboard image only when the platform supports image reads. MCP image content is returned only when explicitly requested.")]
    public async Task<CallToolResult> GetClipboardImageAsync(
        bool includeImage = false,
        CancellationToken cancellationToken = default)
    {
        var capability = RequireCapability(McpCapability.ClipboardRead);
        if (capability is not null)
        {
            return CreateClipboardImageToolResult(capability, imageAvailable: false, width: null, height: null, pngBytes: null, imageIncluded: false);
        }

        if (!_imageClipboardReader.IsSupported)
        {
            return CreateClipboardImageToolResult(
                McpToolOutcomeMapper.EnvironmentError("PNG image clipboard reading is not supported in this runtime."),
                imageAvailable: false,
                width: null,
                height: null,
                pngBytes: null,
                imageIncluded: false);
        }

        try
        {
            var pngBytes = await _imageClipboardReader
                .GetPngAsync(MaximumClipboardImageBytes, cancellationToken)
                .ConfigureAwait(false);
            if (pngBytes is null)
            {
                return CreateClipboardImageToolResult(
                    McpToolOutcomeMapper.Success("No PNG image is available on the clipboard."),
                    imageAvailable: false,
                    width: null,
                    height: null,
                    pngBytes: null,
                    imageIncluded: false);
            }

            using var frame = await _imageAssetCodec
                .DecodePngAsync(pngBytes, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            if (includeImage && pngBytes.Length > MaximumInlineScreenshotBytes)
            {
                return CreateClipboardImageToolResult(
                    McpToolOutcomeMapper.RuntimeError("Clipboard PNG exceeds the maximum inline image size."),
                    imageAvailable: true,
                    frame.Width,
                    frame.Height,
                    pngBytes,
                    imageIncluded: false);
            }

            return CreateClipboardImageToolResult(
                McpToolOutcomeMapper.Success("PNG image read from the clipboard."),
                imageAvailable: true,
                frame.Width,
                frame.Height,
                pngBytes,
                includeImage);
        }
        catch (ImageClipboardUnavailableException)
        {
            return CreateClipboardImageToolResult(
                McpToolOutcomeMapper.EnvironmentError("PNG image clipboard reading is not supported in this runtime."),
                imageAvailable: false,
                width: null,
                height: null,
                pngBytes: null,
                imageIncluded: false);
        }
        catch (InvalidDataException)
        {
            return CreateClipboardImageToolResult(
                McpToolOutcomeMapper.ValidationError("Clipboard PNG could not be validated."),
                imageAvailable: false,
                width: null,
                height: null,
                pngBytes: null,
                imageIncluded: false);
        }
        catch (NotSupportedException)
        {
            return CreateClipboardImageToolResult(
                McpToolOutcomeMapper.ValidationError("Clipboard PNG could not be validated."),
                imageAvailable: false,
                width: null,
                height: null,
                pngBytes: null,
                imageIncluded: false);
        }
        catch (ArgumentException)
        {
            return CreateClipboardImageToolResult(
                McpToolOutcomeMapper.ValidationError("Clipboard PNG could not be validated."),
                imageAvailable: false,
                width: null,
                height: null,
                pngBytes: null,
                imageIncluded: false);
        }
        catch (InvalidOperationException)
        {
            return CreateClipboardImageToolResult(
                McpToolOutcomeMapper.RuntimeError("Clipboard PNG could not be read."),
                imageAvailable: false,
                width: null,
                height: null,
                pngBytes: null,
                imageIncluded: false);
        }
    }

    [McpServerTool(
        Name = "clipboard.set_image",
        Title = "Set image clipboard",
        ReadOnly = false,
        Destructive = false,
        Idempotent = true,
        UseStructuredContent = true,
        OutputSchemaType = typeof(McpClipboardSetImageResult))]
    [Description("Validates an absolute regular PNG file and sets it on the system image clipboard without returning image bytes.")]
    public async Task<CallToolResult> SetClipboardImageAsync(
        string imagePath,
        CancellationToken cancellationToken = default)
    {
        var capability = RequireCapability(McpCapability.ClipboardWrite);
        if (capability is not null)
        {
            return CreateClipboardSetImageToolResult(capability, width: null, height: null, pngByteCount: null);
        }

        capability = RequireCapability(McpCapability.FileRead);
        if (capability is not null)
        {
            return CreateClipboardSetImageToolResult(capability, width: null, height: null, pngByteCount: null);
        }

        if (!_imageClipboardService.IsSupported)
        {
            return CreateClipboardSetImageToolResult(
                McpToolOutcomeMapper.EnvironmentError("PNG image clipboard writing is not supported in this runtime."),
                width: null,
                height: null,
                pngByteCount: null);
        }

        if (!TryNormalizeScreenImagePath(imagePath, out var normalizedImagePath, out var error))
        {
            return CreateClipboardSetImageToolResult(error, width: null, height: null, pngByteCount: null);
        }

        try
        {
            var pngBytes = await _imageAssetCodec
                .ReadFileAsync(normalizedImagePath, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            using var frame = await _imageAssetCodec
                .DecodePngAsync(pngBytes, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            await _imageClipboardService.SetPngAsync(pngBytes, cancellationToken).ConfigureAwait(false);

            return CreateClipboardSetImageToolResult(
                McpToolOutcomeMapper.Success("PNG image set on the clipboard."),
                width: frame.Width,
                height: frame.Height,
                pngByteCount: pngBytes.Length);
        }
        catch (InvalidDataException)
        {
            return CreateClipboardSetImageToolResult(McpToolOutcomeMapper.ValidationError("PNG image could not be validated."), width: null, height: null, pngByteCount: null);
        }
        catch (NotSupportedException)
        {
            return CreateClipboardSetImageToolResult(McpToolOutcomeMapper.ValidationError("PNG image could not be validated."), width: null, height: null, pngByteCount: null);
        }
        catch (ArgumentException)
        {
            return CreateClipboardSetImageToolResult(McpToolOutcomeMapper.ValidationError("PNG image could not be validated."), width: null, height: null, pngByteCount: null);
        }
        catch (IOException)
        {
            return CreateClipboardSetImageToolResult(McpToolOutcomeMapper.FileError("PNG image could not be read."), width: null, height: null, pngByteCount: null);
        }
        catch (UnauthorizedAccessException)
        {
            return CreateClipboardSetImageToolResult(McpToolOutcomeMapper.FileError("PNG image could not be read."), width: null, height: null, pngByteCount: null);
        }
        catch (ImageClipboardUnavailableException)
        {
            return CreateClipboardSetImageToolResult(McpToolOutcomeMapper.EnvironmentError("PNG image clipboard writing is not supported in this runtime."), width: null, height: null, pngByteCount: null);
        }
        catch (InvalidOperationException)
        {
            return CreateClipboardSetImageToolResult(McpToolOutcomeMapper.RuntimeError("PNG image could not be written to the clipboard."), width: null, height: null, pngByteCount: null);
        }
    }

    [McpServerTool(
        Name = "clipboard.set_text",
        Title = "Set text clipboard",
        ReadOnly = false,
        Destructive = false,
        Idempotent = true,
        UseStructuredContent = true,
        OutputSchemaType = typeof(McpClipboardSetTextResult))]
    [Description("Sets up to 65,536 characters of text on the system clipboard without returning the text.")]
    public async Task<CallToolResult> SetClipboardTextAsync(string text, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(text);
        var capability = RequireCapability(McpCapability.ClipboardWrite);
        if (capability is not null)
        {
            return CreateClipboardSetTextToolResult(capability, length: null);
        }

        if (text.Length > MaximumClipboardTextCharacters)
        {
            return CreateClipboardSetTextToolResult(
                McpToolOutcomeMapper.InvalidArguments("Clipboard text exceeds the maximum allowed length."),
                length: null);
        }

        var result = await _clipboardCliService.SetTextAsync(text, cancellationToken).ConfigureAwait(false);
        var outcome = McpToolOutcomeMapper.FromCliResultRedactingErrorDetails(result);
        var length = outcome.Success && result.Data is ClipboardSetData clipboardSet
            ? clipboardSet.Length
            : (int?)null;
        return CreateClipboardSetTextToolResult(outcome, length);
    }

    [McpServerTool(
        Name = "window.query",
        Title = "Query desktop windows",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        UseStructuredContent = true,
        OutputSchemaType = typeof(McpWindowQueryResult))]
    [Description("Reads the active window, a bounded window list, title/class matches, or a bounded wait result without changing desktop windows.")]
    public async Task<CallToolResult> QueryWindowsAsync(
        string mode,
        string? selectorKind = null,
        string? selectorValue = null,
        int? timeoutMs = null,
        CancellationToken cancellationToken = default)
    {
        var capability = RequireCapability(McpCapability.WindowRead);
        if (capability is not null)
        {
            return CreateWindowQueryToolResult(
                outcome: capability,
                mode: string.Empty,
                windows: [],
                totalCount: 0,
                isTruncated: false,
                found: null,
                timeoutMs: null);
        }

        ArgumentNullException.ThrowIfNull(mode);
        if (!TryCreateWindowQueryOptions(
                mode,
                selectorKind,
                selectorValue,
                timeoutMs,
                out var normalizedMode,
                out var options,
                out var error))
        {
            return CreateWindowQueryToolResult(
                error,
                normalizedMode,
                [],
                totalCount: 0,
                isTruncated: false,
                found: null,
                timeoutMs: null);
        }

        var result = await _windowCliService.ExecuteAsync(options, cancellationToken).ConfigureAwait(false);
        var outcome = McpToolOutcomeMapper.FromCliResultRedactingErrorDetails(result);
        if (!outcome.Success)
        {
            return CreateWindowQueryToolResult(
                outcome,
                normalizedMode,
                [],
                totalCount: 0,
                isTruncated: false,
                found: null,
                timeoutMs: options.TimeoutMs);
        }

        return result.Data switch
        {
            WindowInfoData window => CreateWindowQueryToolResult(
                outcome,
                normalizedMode,
                [ToWindowInfo(window)],
                totalCount: 1,
                isTruncated: false,
                found: null,
                timeoutMs: null),
            WindowListData windows => CreateWindowListToolResult(outcome, normalizedMode, windows),
            WindowWaitData wait => CreateWindowWaitToolResult(outcome, normalizedMode, wait),
            _ => CreateWindowQueryToolResult(
                McpToolOutcomeMapper.RuntimeError("Window query could not be read."),
                normalizedMode,
                [],
                totalCount: 0,
                isTruncated: false,
                found: null,
                timeoutMs: options.TimeoutMs),
        };
    }

    [McpServerTool(
        Name = "window.control",
        Title = "Control desktop windows",
        ReadOnly = false,
        Destructive = true,
        Idempotent = false,
        UseStructuredContent = true,
        OutputSchemaType = typeof(McpWindowControlResult))]
    [Description("Focuses, closes, moves, resizes, or changes supported active-window/workspace state through the existing CrossMacro window service.")]
    public async Task<CallToolResult> ControlWindowsAsync(
        string action,
        string? selectorKind = null,
        string? selectorValue = null,
        int? x = null,
        int? y = null,
        int? width = null,
        int? height = null,
        string? workspaceName = null,
        CancellationToken cancellationToken = default)
    {
        var capability = RequireCapability(McpCapability.WindowControl);
        if (capability is not null)
        {
            return CreateWindowControlToolResult(
                outcome: capability,
                action: string.Empty,
                changed: null,
                workspace: null,
                window: null);
        }

        ArgumentNullException.ThrowIfNull(action);
        if (!TryCreateWindowControlOptions(
                action,
                selectorKind,
                selectorValue,
                x,
                y,
                width,
                height,
                workspaceName,
                out var normalizedAction,
                out var options,
                out var error))
        {
            return CreateWindowControlToolResult(error, normalizedAction, changed: null, workspace: null, window: null);
        }

        var result = await _windowCliService.ExecuteAsync(options, cancellationToken).ConfigureAwait(false);
        var outcome = McpToolOutcomeMapper.FromCliResultRedactingErrorDetails(result);
        if (!outcome.Success)
        {
            return CreateWindowControlToolResult(outcome, normalizedAction, changed: null, workspace: null, window: null);
        }

        return result.Data switch
        {
            WindowMutationData mutation => CreateWindowControlToolResult(
                outcome,
                normalizedAction,
                mutation.Result,
                workspace: null,
                window: null),
            WorkspaceData workspace => CreateWindowControlToolResult(
                outcome,
                normalizedAction,
                changed: null,
                workspace.Workspace,
                window: null),
            WindowInfoData window => CreateWindowControlToolResult(
                outcome,
                normalizedAction,
                changed: null,
                workspace: null,
                ToWindowInfo(window)),
            _ => CreateWindowControlToolResult(
                McpToolOutcomeMapper.RuntimeError("Window control result could not be read."),
                normalizedAction,
                changed: null,
                workspace: null,
                window: null),
        };
    }

    [McpServerTool(
        Name = "screen.read",
        Title = "Read screen data",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        UseStructuredContent = true,
        OutputSchemaType = typeof(McpScreenReadResult))]
    [Description("Reads one pixel, waits for a color, or searches a bounded screen region without changing the desktop.")]
    public async Task<CallToolResult> ReadScreenAsync(
        string mode,
        int x,
        int y,
        string? color = null,
        int? x2 = null,
        int? y2 = null,
        int? tolerance = null,
        int? timeoutMs = null,
        CancellationToken cancellationToken = default)
    {
        var capability = RequireCapability(McpCapability.ScreenRead);
        if (capability is not null)
        {
            return CreateScreenReadToolResult(
                outcome: capability,
                mode: string.Empty,
                point: null,
                color: null,
                expectedColor: null,
                region: null,
                tolerance: null,
                found: null,
                timeoutMs: null,
                providerName: null);
        }

        ArgumentNullException.ThrowIfNull(mode);
        if (!TryCreateScreenReadOptions(
                mode,
                x,
                y,
                color,
                x2,
                y2,
                tolerance,
                timeoutMs,
                out var normalizedMode,
                out var options,
                out var error))
        {
            return CreateScreenReadToolResult(
                outcome: error,
                mode: normalizedMode,
                point: null,
                color: null,
                expectedColor: null,
                region: null,
                tolerance: null,
                found: null,
                timeoutMs: null,
                providerName: null);
        }

        var result = await _screenCliService.ExecuteAsync(options, cancellationToken).ConfigureAwait(false);
        var outcome = McpToolOutcomeMapper.FromCliResultRedactingErrorDetails(result);
        if (!outcome.Success)
        {
            return CreateScreenReadToolResult(
                outcome,
                normalizedMode,
                point: null,
                color: null,
                expectedColor: options.ExpectedColor?.ToString(),
                region: ToScreenRegion(options),
                tolerance: options.Action is ScreenCliAction.SearchColor ? options.Tolerance : null,
                found: null,
                timeoutMs: options.TimeoutMs,
                providerName: null);
        }

        return result.Data switch
        {
            ScreenPixelData pixel => CreateScreenReadToolResult(
                outcome,
                normalizedMode,
                new McpScreenPoint(pixel.X, pixel.Y),
                pixel.Color,
                expectedColor: null,
                region: null,
                tolerance: null,
                found: null,
                timeoutMs: null,
                providerName: pixel.ProviderName),
            ScreenWaitColorData wait => CreateScreenReadToolResult(
                outcome,
                normalizedMode,
                new McpScreenPoint(wait.X, wait.Y),
                wait.ActualColor,
                wait.ExpectedColor,
                region: null,
                tolerance: null,
                found: wait.Matched,
                timeoutMs: wait.TimeoutMs,
                providerName: wait.ProviderName),
            ScreenSearchColorData search => CreateScreenReadToolResult(
                outcome,
                normalizedMode,
                search.X is int matchX && search.Y is int matchY ? new McpScreenPoint(matchX, matchY) : null,
                search.Color,
                search.ExpectedColor,
                new McpScreenRegion(search.RegionX, search.RegionY, search.RegionWidth, search.RegionHeight),
                search.Tolerance,
                search.Found,
                options.TimeoutMs,
                search.ProviderName),
            _ => CreateScreenReadToolResult(
                McpToolOutcomeMapper.RuntimeError("Screen data could not be read."),
                normalizedMode,
                point: null,
                color: null,
                expectedColor: null,
                region: null,
                tolerance: null,
                found: null,
                timeoutMs: options.TimeoutMs,
                providerName: null),
        };
    }

    [McpServerTool(
        Name = "cursor.position",
        Title = "Read cursor position",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        UseStructuredContent = true,
        OutputSchemaType = typeof(McpCursorPositionResult))]
    [Description("Reads the current logical global mouse position without moving the pointer. Use the returned point.x and point.y as coordinates for move abs. Returns an environment error when the active desktop provider cannot expose a global cursor position.")]
    public async Task<CallToolResult> GetCursorPositionAsync(CancellationToken cancellationToken = default)
    {
        var capability = RequireCapability(McpCapability.ScreenRead);
        if (capability is not null)
        {
            return CreateToolResult(new McpCursorPositionResult(capability, Point: null, ProviderName: null));
        }

        if (_mousePositionProvider is null
            || !_mousePositionProvider.SupportsAbsolutePosition
            || !MousePositionProviderExtensions.HasUsableAbsolutePosition(_mousePositionProvider))
        {
            return CreateToolResult(new McpCursorPositionResult(
                McpToolOutcomeMapper.EnvironmentError("The active desktop session cannot provide a global cursor position."),
                Point: null,
                ProviderName: _mousePositionProvider?.ProviderName));
        }

        cancellationToken.ThrowIfCancellationRequested();
        var position = await _mousePositionProvider.GetAbsolutePositionAsync().ConfigureAwait(false);
        if (position is null)
        {
            return CreateToolResult(new McpCursorPositionResult(
                McpToolOutcomeMapper.EnvironmentError("The active desktop session could not read the global cursor position."),
                Point: null,
                ProviderName: _mousePositionProvider.ProviderName));
        }

        return CreateToolResult(new McpCursorPositionResult(
            McpToolOutcomeMapper.Success($"Cursor position: {position.Value.X.ToString(System.Globalization.CultureInfo.InvariantCulture)},{position.Value.Y.ToString(System.Globalization.CultureInfo.InvariantCulture)}."),
            new McpScreenPoint(position.Value.X, position.Value.Y),
            _mousePositionProvider.ProviderName));
    }

    [McpServerTool(
        Name = "screen.find_image",
        Title = "Find an image on screen",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        UseStructuredContent = true,
        OutputSchemaType = typeof(McpScreenImageSearchResult))]
    [Description("Searches a bounded screen region for an absolute regular PNG file without returning file content.")]
    public async Task<CallToolResult> FindScreenImageAsync(
        string imagePath,
        int? regionX = null,
        int? regionY = null,
        int? regionWidth = null,
        int? regionHeight = null,
        double? similarity = null,
        string? matchMode = null,
        CancellationToken cancellationToken = default)
    {
        var capability = RequireCapability(McpCapability.ScreenRead);
        if (capability is not null)
        {
            return CreateScreenImageSearchToolResult(
                outcome: capability,
                found: null,
                point: null,
                score: null,
                region: null,
                similarity: null,
                matchMode: null,
                providerName: null);
        }

        capability = RequireCapability(McpCapability.FileRead);
        if (capability is not null)
        {
            return CreateScreenImageSearchToolResult(
                outcome: capability,
                found: null,
                point: null,
                score: null,
                region: null,
                similarity: null,
                matchMode: null,
                providerName: null);
        }

        if (!TryCreateImageSearchOptions(
                imagePath,
                regionX,
                regionY,
                regionWidth,
                regionHeight,
                similarity,
                matchMode,
                out var options,
                out var error))
        {
            return CreateScreenImageSearchToolResult(
                outcome: error,
                found: null,
                point: null,
                score: null,
                region: null,
                similarity: null,
                matchMode: null,
                providerName: null);
        }

        var result = await _screenCliService.ExecuteAsync(options, cancellationToken).ConfigureAwait(false);
        var outcome = McpToolOutcomeMapper.FromCliResultRedactingErrorDetails(result);
        if (!outcome.Success)
        {
            return CreateScreenImageSearchToolResult(
                outcome,
                found: null,
                point: null,
                score: null,
                region: ToScreenRegion(options),
                similarity: options.Similarity,
                matchMode: ToMatchModeToken(options.MatchMode),
                providerName: null);
        }

        if (result.Data is not ScreenSearchImageData image)
        {
            return CreateScreenImageSearchToolResult(
                McpToolOutcomeMapper.RuntimeError("Screen image search could not be read."),
                found: null,
                point: null,
                score: null,
                region: ToScreenRegion(options),
                similarity: options.Similarity,
                matchMode: ToMatchModeToken(options.MatchMode),
                providerName: null);
        }

        return CreateScreenImageSearchToolResult(
            outcome,
            image.Found,
            image.X is int matchX && image.Y is int matchY ? new McpScreenPoint(matchX, matchY) : null,
            image.Score,
            ToScreenRegion(options),
            image.Similarity,
            image.MatchMode,
            image.ProviderName);
    }

    [McpServerTool(
        Name = "screenshot.capture",
        Title = "Capture a screenshot",
        ReadOnly = false,
        Destructive = false,
        Idempotent = false,
        UseStructuredContent = true,
        OutputSchemaType = typeof(McpScreenshotCaptureResult))]
    [Description("Captures one bounded screenshot inline only when requested, and optionally writes the same PNG to a file or image clipboard.")]
    public async Task<CallToolResult> CaptureScreenshotAsync(
        bool includeImage = false,
        string? outputPath = null,
        bool copyToClipboard = false,
        int? regionX = null,
        int? regionY = null,
        int? regionWidth = null,
        int? regionHeight = null,
        CancellationToken cancellationToken = default)
    {
        var capability = RequireCapability(McpCapability.ScreenRead);
        if (capability is not null)
        {
            return CreateScreenshotCaptureToolResult(capability, data: null, imageIncluded: false);
        }

        if (!string.IsNullOrWhiteSpace(outputPath))
        {
            capability = RequireCapability(McpCapability.FileWrite);
            if (capability is not null)
            {
                return CreateScreenshotCaptureToolResult(capability, data: null, imageIncluded: false);
            }
        }

        if (copyToClipboard)
        {
            capability = RequireCapability(McpCapability.ClipboardWrite);
            if (capability is not null)
            {
                return CreateScreenshotCaptureToolResult(capability, data: null, imageIncluded: false);
            }
        }

        if (!includeImage && string.IsNullOrWhiteSpace(outputPath) && !copyToClipboard)
        {
            return CreateScreenshotCaptureToolResult(
                McpToolOutcomeMapper.InvalidArguments("Screenshot capture requires includeImage, outputPath, or copyToClipboard."),
                data: null,
                imageIncluded: false);
        }

        if (!TryNormalizeScreenshotOutputPath(outputPath, out var normalizedOutputPath, out var outputError))
        {
            return CreateScreenshotCaptureToolResult(
                outputError,
                data: null,
                imageIncluded: false);
        }

        if (!TryCreateOptionalBoundedScreenRegion(regionX, regionY, regionWidth, regionHeight, out var region, out var regionError))
        {
            return CreateScreenshotCaptureToolResult(
                regionError,
                data: null,
                imageIncluded: false);
        }

        var maximumEncodedBytes = includeImage
            ? MaximumInlineScreenshotBytes
            : ScreenshotPngCaptureRequest.DefaultMaximumEncodedBytes;
        var capture = await _screenshotCaptureService.CapturePngAsync(
            new ScreenshotPngCaptureRequest(normalizedOutputPath, copyToClipboard, ToScreenRect(region), maximumEncodedBytes),
            cancellationToken).ConfigureAwait(false);
        if (!capture.Success)
        {
            return CreateScreenshotCaptureToolResult(
                McpToolOutcomeMapper.FromScreenshotCaptureFailure(
                    capture.FailureKind!.Value,
                    capture.ScreenReadErrorKind,
                    capture.Message),
                data: null,
                imageIncluded: false);
        }

        var data = capture.Data!;
        if (includeImage && data.PngBytes.Length > MaximumInlineScreenshotBytes)
        {
            return CreateScreenshotCaptureToolResult(
                McpToolOutcomeMapper.RuntimeError("Screenshot PNG exceeds the maximum inline image size."),
                data,
                imageIncluded: false);
        }

        return CreateScreenshotCaptureToolResult(
            McpToolOutcomeMapper.Success("Screenshot captured."),
            data,
            imageIncluded: includeImage);
    }

    [McpServerTool(
        Name = "image.read",
        Title = "Read a PNG image",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        UseStructuredContent = true,
        OutputSchemaType = typeof(McpImageReadResult))]
    [Description("Validates an absolute regular PNG file and returns image content only when explicitly requested.")]
    public async Task<CallToolResult> ReadImageAsync(
        string imagePath,
        bool includeImage = false,
        CancellationToken cancellationToken = default)
    {
        var capability = RequireCapability(McpCapability.FileRead);
        if (capability is not null)
        {
            return CreateImageReadToolResult(capability, width: null, height: null, pngBytes: null, imageIncluded: false);
        }

        if (!TryNormalizeScreenImagePath(imagePath, out var normalizedImagePath, out var error))
        {
            return CreateImageReadToolResult(error, width: null, height: null, pngBytes: null, imageIncluded: false);
        }

        try
        {
            var pngBytes = await _imageAssetCodec
                .ReadFileAsync(normalizedImagePath, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            using var frame = await _imageAssetCodec
                .DecodePngAsync(pngBytes, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            if (includeImage && pngBytes.Length > MaximumInlineScreenshotBytes)
            {
                return CreateImageReadToolResult(
                    McpToolOutcomeMapper.RuntimeError("PNG image exceeds the maximum inline image size."),
                    frame.Width,
                    frame.Height,
                    pngBytes,
                    imageIncluded: false);
            }

            return CreateImageReadToolResult(
                McpToolOutcomeMapper.Success("PNG image read."),
                frame.Width,
                frame.Height,
                pngBytes,
                includeImage);
        }
        catch (InvalidDataException)
        {
            return CreateImageReadToolResult(
                McpToolOutcomeMapper.ValidationError("PNG image could not be validated."),
                width: null,
                height: null,
                pngBytes: null,
                imageIncluded: false);
        }
        catch (NotSupportedException)
        {
            return CreateImageReadToolResult(
                McpToolOutcomeMapper.ValidationError("PNG image could not be validated."),
                width: null,
                height: null,
                pngBytes: null,
                imageIncluded: false);
        }
        catch (ArgumentException)
        {
            return CreateImageReadToolResult(
                McpToolOutcomeMapper.ValidationError("PNG image could not be validated."),
                width: null,
                height: null,
                pngBytes: null,
                imageIncluded: false);
        }
        catch (IOException)
        {
            return CreateImageReadToolResult(
                McpToolOutcomeMapper.FileError("PNG image could not be read."),
                width: null,
                height: null,
                pngBytes: null,
                imageIncluded: false);
        }
        catch (UnauthorizedAccessException)
        {
            return CreateImageReadToolResult(
                McpToolOutcomeMapper.FileError("PNG image could not be read."),
                width: null,
                height: null,
                pngBytes: null,
                imageIncluded: false);
        }
    }

    private string GetOperatingSystem() => _runtimeContext switch
    {
        { IsLinux: true } => "linux",
        { IsWindows: true } => "windows",
        { IsMacOS: true } => "macos",
        _ => "unknown",
    };

    private bool TryNormalizeDirectoryPath(string directoryPath, out string normalizedDirectoryPath, out McpToolOutcome error)
    {
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            normalizedDirectoryPath = string.Empty;
            error = McpToolOutcomeMapper.InvalidArguments("Directory path is required.");
            return false;
        }

        if (!Path.IsPathFullyQualified(directoryPath))
        {
            normalizedDirectoryPath = string.Empty;
            error = McpToolOutcomeMapper.InvalidArguments("Directory path must be absolute.");
            return false;
        }

        try
        {
            if (!_pathPolicy.TryAuthorize(directoryPath, McpPathKind.MacroRead, requireExisting: true, out normalizedDirectoryPath, out error))
            {
                return false;
            }

            var directoryInfo = new DirectoryInfo(normalizedDirectoryPath);
            if (!directoryInfo.Exists)
            {
                error = McpToolOutcomeMapper.FileError("Macro directory not found.");
                return false;
            }

            if (directoryInfo.Attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                error = McpToolOutcomeMapper.InvalidArguments("Macro directory must not be a symbolic link.");
                return false;
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            normalizedDirectoryPath = string.Empty;
            error = McpToolOutcomeMapper.InvalidArguments("Directory path is invalid.");
            return false;
        }

        error = McpToolOutcomeMapper.Success(string.Empty);
        return true;
    }

    private bool TryNormalizeMacroPath(string macroPath, out string normalizedMacroPath, out McpToolOutcome error)
    {
        if (string.IsNullOrWhiteSpace(macroPath))
        {
            normalizedMacroPath = string.Empty;
            error = McpToolOutcomeMapper.InvalidArguments("Macro path is required.");
            return false;
        }

        if (!Path.IsPathFullyQualified(macroPath))
        {
            normalizedMacroPath = string.Empty;
            error = McpToolOutcomeMapper.InvalidArguments("Macro path must be absolute.");
            return false;
        }

        if (!string.Equals(Path.GetExtension(macroPath), ".macro", StringComparison.OrdinalIgnoreCase))
        {
            normalizedMacroPath = string.Empty;
            error = McpToolOutcomeMapper.InvalidArguments("Macro path must use the .macro extension.");
            return false;
        }

        try
        {
            if (!_pathPolicy.TryAuthorize(macroPath, McpPathKind.MacroRead, requireExisting: true, out normalizedMacroPath, out error))
            {
                return false;
            }

            if (!File.Exists(normalizedMacroPath))
            {
                error = McpToolOutcomeMapper.FileError("Macro file not found.");
                return false;
            }

            var fileInfo = new FileInfo(normalizedMacroPath);
            if (fileInfo.Attributes.HasFlag(FileAttributes.Directory)
                || fileInfo.Attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                error = McpToolOutcomeMapper.InvalidArguments("Macro path must refer to a regular file.");
                return false;
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            normalizedMacroPath = string.Empty;
            error = McpToolOutcomeMapper.InvalidArguments("Macro path is invalid.");
            return false;
        }

        error = McpToolOutcomeMapper.Success(string.Empty);
        return true;
    }

    private static bool TryCreateWindowQueryOptions(
        string mode,
        string? selectorKind,
        string? selectorValue,
        int? timeoutMs,
        out string normalizedMode,
        out WindowCliOptions options,
        out McpToolOutcome error)
    {
        normalizedMode = mode.Trim().ToLowerInvariant();
        options = new WindowCliOptions(WindowCliAction.List);

        var action = normalizedMode switch
        {
            "active" => WindowCliAction.Active,
            "list" => WindowCliAction.List,
            "search" => WindowCliAction.Search,
            "wait" => WindowCliAction.Wait,
            _ => (WindowCliAction?)null,
        };
        if (action is null)
        {
            error = McpToolOutcomeMapper.InvalidArguments("Window query mode must be active, list, search, or wait.");
            return false;
        }

        if (action is WindowCliAction.Active or WindowCliAction.List)
        {
            if (!string.IsNullOrWhiteSpace(selectorKind) || !string.IsNullOrWhiteSpace(selectorValue))
            {
                error = McpToolOutcomeMapper.InvalidArguments("Window selectors are only supported for search and wait modes.");
                return false;
            }

            if (timeoutMs is not null)
            {
                error = McpToolOutcomeMapper.InvalidArguments("Window timeout is only supported for wait mode.");
                return false;
            }

            options = new WindowCliOptions(action.Value);
            error = McpToolOutcomeMapper.Success(string.Empty);
            return true;
        }

        if (!TryCreateWindowSelector(selectorKind, selectorValue, out var selector, out error))
        {
            return false;
        }

        if (action is WindowCliAction.Search)
        {
            if (timeoutMs is not null)
            {
                error = McpToolOutcomeMapper.InvalidArguments("Window timeout is only supported for wait mode.");
                return false;
            }

            options = new WindowCliOptions(action.Value, selector);
            error = McpToolOutcomeMapper.Success(string.Empty);
            return true;
        }

        var effectiveTimeoutMs = timeoutMs ?? DefaultWindowWaitTimeoutMs;
        if (effectiveTimeoutMs is < 0 or > MaximumWindowWaitTimeoutMs)
        {
            error = McpToolOutcomeMapper.InvalidArguments("Window wait timeout must be between 0 and 30,000 milliseconds.");
            return false;
        }

        options = new WindowCliOptions(action.Value, selector, TimeoutMs: effectiveTimeoutMs);
        error = McpToolOutcomeMapper.Success(string.Empty);
        return true;
    }

    private static bool TryCreateWindowControlOptions(
        string action,
        string? selectorKind,
        string? selectorValue,
        int? x,
        int? y,
        int? width,
        int? height,
        string? workspaceName,
        out string normalizedAction,
        out WindowCliOptions options,
        out McpToolOutcome error)
    {
        normalizedAction = action.Trim().ToLowerInvariant();
        options = new WindowCliOptions(WindowCliAction.Active);
        error = McpToolOutcomeMapper.Success(string.Empty);
        if (string.IsNullOrWhiteSpace(normalizedAction))
        {
            error = McpToolOutcomeMapper.InvalidArguments("Window control action is required.");
            return false;
        }

        if (workspaceName is { Length: > MaximumWindowSelectorCharacters })
        {
            error = McpToolOutcomeMapper.InvalidArguments("Workspace name exceeds the maximum allowed length.");
            return false;
        }

        if (normalizedAction is "focus" or "close")
        {
            if (!TryCreateWindowControlSelector(selectorKind, selectorValue, normalizedAction is "close", out var selector, out error))
            {
                return false;
            }

            options = new WindowCliOptions(
                normalizedAction is "focus" ? WindowCliAction.Focus : WindowCliAction.Close,
                selector);
            return true;
        }

        if (normalizedAction is "move" or "resize")
        {
            if (selectorKind is not null || selectorValue is not null || workspaceName is not null || x is null || y is null)
            {
                error = McpToolOutcomeMapper.InvalidArguments($"Window {normalizedAction} requires x and y only.");
                return false;
            }

            if (normalizedAction is "resize" && (x <= 0 || y <= 0))
            {
                error = McpToolOutcomeMapper.InvalidArguments("Window resize width and height must be positive.");
                return false;
            }

            options = normalizedAction is "move"
                ? new WindowCliOptions(WindowCliAction.Move, X: x, Y: y)
                : new WindowCliOptions(WindowCliAction.Resize, Width: x, Height: y);
            return true;
        }

        if (normalizedAction is "workspace_switch" or "workspace_move_active" or "workspace_move_window")
        {
            if (string.IsNullOrWhiteSpace(workspaceName))
            {
                error = McpToolOutcomeMapper.InvalidArguments("Workspace control requires workspaceName.");
                return false;
            }

            if (normalizedAction is "workspace_move_window")
            {
                if (!string.Equals(selectorKind, "address", StringComparison.OrdinalIgnoreCase)
                    || string.IsNullOrWhiteSpace(selectorValue))
                {
                    error = McpToolOutcomeMapper.InvalidArguments("workspace_move_window requires an address selector.");
                    return false;
                }

                options = new WindowCliOptions(
                    WindowCliAction.WorkspaceMoveWindow,
                    new WindowSelector(WindowSelectorKind.Address, selectorValue),
                    WorkspaceName: workspaceName);
                return true;
            }

            if (selectorKind is not null || selectorValue is not null || x is not null || y is not null || width is not null || height is not null)
            {
                error = McpToolOutcomeMapper.InvalidArguments("Workspace control received unsupported selector or geometry fields.");
                return false;
            }

            options = normalizedAction is "workspace_switch"
                ? new WindowCliOptions(WindowCliAction.WorkspaceSwitch, WorkspaceName: workspaceName)
                : new WindowCliOptions(WindowCliAction.WorkspaceMoveActive, WorkspaceName: workspaceName);
            return true;
        }

        var flagAction = normalizedAction switch
        {
            "center" => WindowCliAction.Center,
            "maximize" => WindowCliAction.Maximize,
            "fullscreen" => WindowCliAction.Fullscreen,
            "floating" or "float" => WindowCliAction.Floating,
            _ => (WindowCliAction?)null,
        };
        if (flagAction is null || selectorKind is not null || selectorValue is not null || x is not null || y is not null || width is not null || height is not null || workspaceName is not null)
        {
            error = McpToolOutcomeMapper.InvalidArguments("Window control action or arguments are invalid.");
            return false;
        }

        options = new WindowCliOptions(flagAction.Value);
        return true;
    }

    private static bool TryCreateWindowControlSelector(
        string? selectorKind,
        string? selectorValue,
        bool close,
        out WindowSelector selector,
        out McpToolOutcome error)
    {
        selector = new WindowSelector(WindowSelectorKind.Title, string.Empty);
        if (string.IsNullOrWhiteSpace(selectorKind) || string.IsNullOrWhiteSpace(selectorValue) || selectorValue.Length > MaximumWindowSelectorCharacters)
        {
            error = McpToolOutcomeMapper.InvalidArguments("Window control requires a bounded selectorKind and selectorValue.");
            return false;
        }

        var kind = selectorKind.Trim().ToLowerInvariant() switch
        {
            "address" => WindowSelectorKind.Address,
            "title" => WindowSelectorKind.Title,
            "class" when !close => WindowSelectorKind.Class,
            _ => (WindowSelectorKind?)null,
        };
        if (kind is null)
        {
            error = McpToolOutcomeMapper.InvalidArguments(close
                ? "Window close selectorKind must be address or title."
                : "Window focus selectorKind must be address, title, or class.");
            return false;
        }

        selector = new WindowSelector(kind.Value, selectorValue);
        error = McpToolOutcomeMapper.Success(string.Empty);
        return true;
    }

    private static bool TryCreateWindowSelector(
        string? selectorKind,
        string? selectorValue,
        out WindowSelector selector,
        out McpToolOutcome error)
    {
        selector = new WindowSelector(WindowSelectorKind.Title, string.Empty);
        if (string.IsNullOrWhiteSpace(selectorKind) || string.IsNullOrWhiteSpace(selectorValue))
        {
            error = McpToolOutcomeMapper.InvalidArguments("Window search and wait require selectorKind and selectorValue.");
            return false;
        }

        if (selectorValue.Length > MaximumWindowSelectorCharacters)
        {
            error = McpToolOutcomeMapper.InvalidArguments("Window selector value exceeds the maximum allowed length.");
            return false;
        }

        var kind = selectorKind.Trim().ToLowerInvariant() switch
        {
            "title" => WindowSelectorKind.Title,
            "class" => WindowSelectorKind.Class,
            _ => (WindowSelectorKind?)null,
        };
        if (kind is null)
        {
            error = McpToolOutcomeMapper.InvalidArguments("Window selector kind must be title or class.");
            return false;
        }

        selector = new WindowSelector(kind.Value, selectorValue);
        error = McpToolOutcomeMapper.Success(string.Empty);
        return true;
    }

    private static bool TryCreateScreenReadOptions(
        string mode,
        int x,
        int y,
        string? color,
        int? x2,
        int? y2,
        int? tolerance,
        int? timeoutMs,
        out string normalizedMode,
        out ScreenCliOptions options,
        out McpToolOutcome error)
    {
        normalizedMode = mode.Trim().ToLowerInvariant();
        options = new ScreenCliOptions(ScreenCliAction.Pixel);

        switch (normalizedMode)
        {
            case "pixel":
                if (color is not null || x2 is not null || y2 is not null || tolerance is not null || timeoutMs is not null)
                {
                    error = McpToolOutcomeMapper.InvalidArguments("Pixel mode accepts only x and y coordinates.");
                    return false;
                }

                options = new ScreenCliOptions(ScreenCliAction.Pixel, x, y);
                error = McpToolOutcomeMapper.Success(string.Empty);
                return true;

            case "wait_color":
                if (x2 is not null || y2 is not null || tolerance is not null)
                {
                    error = McpToolOutcomeMapper.InvalidArguments("Wait color mode does not accept search bounds or tolerance.");
                    return false;
                }

                if (!TryParseScreenColor(color, out var expectedColor, out error)
                    || !TryGetBoundedScreenTimeout(timeoutMs, out var waitTimeoutMs, out error))
                {
                    return false;
                }

                options = new ScreenCliOptions(ScreenCliAction.WaitColor, x, y, expectedColor, TimeoutMs: waitTimeoutMs);
                error = McpToolOutcomeMapper.Success(string.Empty);
                return true;

            case "search_color":
                if (!TryParseScreenColor(color, out var searchColor, out error)
                    || !TryCreateBoundedColorSearchRegion(x, y, x2, y2, out _, out error)
                    || !TryGetScreenTolerance(tolerance, out var searchTolerance, out error)
                    || !TryGetBoundedScreenTimeout(timeoutMs, out var searchTimeoutMs, out error))
                {
                    return false;
                }

                options = new ScreenCliOptions(
                    ScreenCliAction.SearchColor,
                    x,
                    y,
                    searchColor,
                    X2: x2,
                    Y2: y2,
                    TimeoutMs: searchTimeoutMs,
                    Tolerance: searchTolerance);
                error = McpToolOutcomeMapper.Success(string.Empty);
                return true;

            default:
                error = McpToolOutcomeMapper.InvalidArguments("Screen read mode must be pixel, wait_color, or search_color.");
                return false;
        }
    }

    private bool TryCreateImageSearchOptions(
        string imagePath,
        int? regionX,
        int? regionY,
        int? regionWidth,
        int? regionHeight,
        double? similarity,
        string? matchMode,
        out ScreenCliOptions options,
        out McpToolOutcome error)
    {
        options = new ScreenCliOptions(ScreenCliAction.SearchImage);
        if (!TryNormalizeScreenImagePath(imagePath, out var normalizedImagePath, out error)
            || !TryCreateOptionalBoundedScreenRegion(regionX, regionY, regionWidth, regionHeight, out var region, out error)
            || !TryGetImageSimilarity(similarity, out var effectiveSimilarity, out error)
            || !TryGetImageMatchMode(matchMode, out var effectiveMatchMode, out error))
        {
            return false;
        }

        options = new ScreenCliOptions(
            ScreenCliAction.SearchImage,
            ImagePath: normalizedImagePath,
            RegionX: region?.X,
            RegionY: region?.Y,
            RegionWidth: region?.Width,
            RegionHeight: region?.Height,
            Similarity: effectiveSimilarity,
            MatchMode: effectiveMatchMode);
        error = McpToolOutcomeMapper.Success(string.Empty);
        return true;
    }

    private static bool TryParseScreenColor(string? value, out ScreenPixelColor color, out McpToolOutcome error)
    {
        if (!ScreenPixelColor.TryParse(value, out color))
        {
            error = McpToolOutcomeMapper.InvalidArguments("Screen color must be exactly 6 hexadecimal RGB characters.");
            return false;
        }

        error = McpToolOutcomeMapper.Success(string.Empty);
        return true;
    }

    private static bool TryGetBoundedScreenTimeout(int? value, out int timeoutMs, out McpToolOutcome error)
    {
        timeoutMs = value ?? DefaultScreenTimeoutMs;
        if (timeoutMs is < 0 or > MaximumScreenTimeoutMs)
        {
            error = McpToolOutcomeMapper.InvalidArguments("Screen timeout must be between 0 and 30,000 milliseconds.");
            return false;
        }

        error = McpToolOutcomeMapper.Success(string.Empty);
        return true;
    }

    private static bool TryGetScreenTolerance(int? value, out int tolerance, out McpToolOutcome error)
    {
        tolerance = value ?? 0;
        if (tolerance is < 0 or > byte.MaxValue)
        {
            error = McpToolOutcomeMapper.InvalidArguments("Screen color tolerance must be between 0 and 255.");
            return false;
        }

        error = McpToolOutcomeMapper.Success(string.Empty);
        return true;
    }

    private static bool TryCreateBoundedColorSearchRegion(
        int x,
        int y,
        int? x2,
        int? y2,
        out McpScreenRegion region,
        out McpToolOutcome error)
    {
        region = new McpScreenRegion(0, 0, 1, 1);
        if (x2 is null || y2 is null)
        {
            error = McpToolOutcomeMapper.InvalidArguments("Screen color search requires x2 and y2 bounds.");
            return false;
        }

        if (x is var firstX && y is var firstY && x2.Value is var secondX && y2.Value is var secondY)
        {
            var left = Math.Min(firstX, secondX);
            var top = Math.Min(firstY, secondY);
            var width = (long)Math.Max(firstX, secondX) - left;
            var height = (long)Math.Max(firstY, secondY) - top;
            return TryCreateBoundedScreenRegion(left, top, width, height, out region, out error);
        }

        error = McpToolOutcomeMapper.InvalidArguments("Screen color search bounds are invalid.");
        return false;
    }

    private static bool TryCreateOptionalBoundedScreenRegion(
        int? x,
        int? y,
        int? width,
        int? height,
        out McpScreenRegion? region,
        out McpToolOutcome error)
    {
        region = null;
        if (x is null && y is null && width is null && height is null)
        {
            error = McpToolOutcomeMapper.Success(string.Empty);
            return true;
        }

        if (x is null || y is null || width is null || height is null)
        {
            error = McpToolOutcomeMapper.InvalidArguments("Screen region requires x, y, width, and height.");
            return false;
        }

        if (!TryCreateBoundedScreenRegion(x.Value, y.Value, width.Value, height.Value, out var requiredRegion, out error))
        {
            return false;
        }

        region = requiredRegion;
        return true;
    }

    private static bool TryCreateBoundedScreenRegion(
        int x,
        int y,
        long width,
        long height,
        out McpScreenRegion region,
        out McpToolOutcome error)
    {
        region = new McpScreenRegion(0, 0, 1, 1);
        if (width <= 0 || height <= 0)
        {
            error = McpToolOutcomeMapper.InvalidArguments("Screen region width and height must be positive.");
            return false;
        }

        if (width > int.MaxValue || height > int.MaxValue
            || x + width > int.MaxValue || x + width < int.MinValue
            || y + height > int.MaxValue || y + height < int.MinValue)
        {
            error = McpToolOutcomeMapper.InvalidArguments("Screen region endpoint exceeds the supported coordinate range.");
            return false;
        }

        if (width * height > MaximumScreenRegionPixels)
        {
            error = McpToolOutcomeMapper.InvalidArguments("Screen region exceeds the maximum allowed pixel count.");
            return false;
        }

        region = new McpScreenRegion(x, y, (int)width, (int)height);
        error = McpToolOutcomeMapper.Success(string.Empty);
        return true;
    }

    private bool TryNormalizeScreenImagePath(string imagePath, out string normalizedImagePath, out McpToolOutcome error)
    {
        if (string.IsNullOrWhiteSpace(imagePath))
        {
            normalizedImagePath = string.Empty;
            error = McpToolOutcomeMapper.InvalidArguments("Screen image path is required.");
            return false;
        }

        if (!Path.IsPathFullyQualified(imagePath))
        {
            normalizedImagePath = string.Empty;
            error = McpToolOutcomeMapper.InvalidArguments("Screen image path must be absolute.");
            return false;
        }

        if (!string.Equals(Path.GetExtension(imagePath), ".png", StringComparison.OrdinalIgnoreCase))
        {
            normalizedImagePath = string.Empty;
            error = McpToolOutcomeMapper.InvalidArguments("Screen image path must use the .png extension.");
            return false;
        }

        try
        {
            if (!_pathPolicy.TryAuthorize(imagePath, McpPathKind.ImageRead, requireExisting: true, out normalizedImagePath, out error))
            {
                return false;
            }

            if (!File.Exists(normalizedImagePath))
            {
                error = McpToolOutcomeMapper.FileError("Screen image file not found.");
                return false;
            }

            var fileInfo = new FileInfo(normalizedImagePath);
            if (fileInfo.Attributes.HasFlag(FileAttributes.Directory)
                || fileInfo.Attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                error = McpToolOutcomeMapper.InvalidArguments("Screen image path must refer to a regular file.");
                return false;
            }

            if (fileInfo.Length is <= 0 or > MaximumScreenImageBytes)
            {
                error = McpToolOutcomeMapper.InvalidArguments("Screen image file exceeds the allowed size.");
                return false;
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            normalizedImagePath = string.Empty;
            error = McpToolOutcomeMapper.FileError("Screen image file could not be accessed.");
            return false;
        }

        error = McpToolOutcomeMapper.Success(string.Empty);
        return true;
    }

    private static bool TryGetImageSimilarity(double? value, out double similarity, out McpToolOutcome error)
    {
        similarity = value ?? 0.95;
        if (!double.IsFinite(similarity) || similarity is < 0.0 or > 1.0)
        {
            error = McpToolOutcomeMapper.InvalidArguments("Screen image similarity must be a finite number between 0 and 1.");
            return false;
        }

        error = McpToolOutcomeMapper.Success(string.Empty);
        return true;
    }

    private static bool TryGetImageMatchMode(string? value, out ScreenImageMatchMode matchMode, out McpToolOutcome error)
    {
        matchMode = value?.Trim().ToLowerInvariant() switch
        {
            null or "" or "auto" => ScreenImageMatchMode.Automatic,
            "first" => ScreenImageMatchMode.First,
            "best" => ScreenImageMatchMode.Best,
            _ => (ScreenImageMatchMode)(-1),
        };
        if (!Enum.IsDefined(matchMode))
        {
            error = McpToolOutcomeMapper.InvalidArguments("Screen image match mode must be auto, first, or best.");
            return false;
        }

        error = McpToolOutcomeMapper.Success(string.Empty);
        return true;
    }

    private bool TryNormalizeScreenshotOutputPath(
        string? outputPath,
        out string? normalizedOutputPath,
        out McpToolOutcome error)
    {
        normalizedOutputPath = null;
        if (outputPath is null)
        {
            error = McpToolOutcomeMapper.Success(string.Empty);
            return true;
        }

        if (string.IsNullOrWhiteSpace(outputPath))
        {
            error = McpToolOutcomeMapper.InvalidArguments("Screenshot output path must not be empty.");
            return false;
        }

        if (!Path.IsPathFullyQualified(outputPath))
        {
            error = McpToolOutcomeMapper.InvalidArguments("Screenshot output path must be absolute.");
            return false;
        }

        if (!string.Equals(Path.GetExtension(outputPath), ".png", StringComparison.OrdinalIgnoreCase))
        {
            error = McpToolOutcomeMapper.InvalidArguments("Screenshot output path must use the .png extension.");
            return false;
        }

        try
        {
            if (!_pathPolicy.TryAuthorize(outputPath, McpPathKind.ImageWrite, requireExisting: false, out var authorizedPath, out error))
            {
                normalizedOutputPath = null;
                return false;
            }

            normalizedOutputPath = authorizedPath;
            if (Directory.Exists(normalizedOutputPath))
            {
                error = McpToolOutcomeMapper.InvalidArguments("Screenshot output path must refer to a file.");
                return false;
            }

            if (File.Exists(normalizedOutputPath)
                && new FileInfo(normalizedOutputPath).Attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                error = McpToolOutcomeMapper.InvalidArguments("Screenshot output path must not be a symbolic link.");
                return false;
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            normalizedOutputPath = null;
            error = McpToolOutcomeMapper.FileError("Screenshot output path could not be accessed.");
            return false;
        }

        error = McpToolOutcomeMapper.Success(string.Empty);
        return true;
    }

    private bool TryAuthorizeImageOrMacroReadPath(
        string path,
        out string normalizedPath,
        out McpToolOutcome error)
    {
        var kind = Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".macro" => McpPathKind.MacroRead,
            ".png" => McpPathKind.ImageRead,
            _ => McpPathKind.FileRead,
        };
        return _pathPolicy.TryAuthorize(path, kind, requireExisting: true, out normalizedPath, out error);
    }

    private static string ToMatchModeToken(ScreenImageMatchMode matchMode) => matchMode switch
    {
        ScreenImageMatchMode.Automatic => "auto",
        ScreenImageMatchMode.First => "first",
        ScreenImageMatchMode.Best => "best",
        _ => "unknown",
    };

    private static int GetMcpAutomationTimeoutSeconds(int timeoutSeconds) =>
        timeoutSeconds is > 0 and <= MaximumAutomationTimeoutSeconds
            ? timeoutSeconds
            : DefaultAutomationTimeoutSeconds;

    private static string GetCapabilityMessage(DoctorCheckStatus status) => status switch
    {
        DoctorCheckStatus.Pass => "Available.",
        DoctorCheckStatus.Warn => "May require attention.",
        DoctorCheckStatus.Fail => "Unavailable.",
        _ => "Unknown.",
    };

    private static McpMacroInfo? ToMacroInfo(object? data)
    {
        if (data is not MacroInfoData macro)
        {
            return null;
        }

        return new McpMacroInfo(
            macro.MacroPath,
            macro.MacroName,
            macro.CreatedAt,
            macro.EventCount,
            macro.TotalDurationMs,
            macro.CoordinateMode,
            macro.IsAbsoluteCoordinates,
            macro.SkipInitialZeroZero,
            macro.TrailingDelayMicroseconds,
            macro.TrailingDelayMs,
            macro.HasTrailingRandomDelay,
            macro.TrailingDelayMinMs,
            macro.TrailingDelayMaxMs,
            new McpMacroEventBreakdown(
                macro.EventBreakdown.MouseMove,
                macro.EventBreakdown.ButtonPress,
                macro.EventBreakdown.ButtonRelease,
                macro.EventBreakdown.Click,
                macro.EventBreakdown.KeyPress,
                macro.EventBreakdown.KeyRelease));
    }

    private static McpMacroSummary? ToMacroSummary(object? data)
    {
        return data is MacroSummaryData macro
            ? new McpMacroSummary(
                macro.MacroPath,
                macro.MacroName,
                macro.EventCount,
                macro.TotalDurationMs,
                macro.CoordinateMode,
                macro.IsAbsoluteCoordinates)
            : null;
    }

    private static McpScreenRegion? ToScreenRegion(ScreenCliOptions options)
    {
        return options.RegionX is int x
            && options.RegionY is int y
            && options.RegionWidth is int width
            && options.RegionHeight is int height
            ? new McpScreenRegion(x, y, width, height)
            : null;
    }

    private static ScreenRect? ToScreenRect(McpScreenRegion? region)
    {
        return region is { } value
            ? new ScreenRect(value.X, value.Y, value.Width, value.Height)
            : null;
    }

    private static CallToolResult CreateWindowListToolResult(
        McpToolOutcome outcome,
        string mode,
        WindowListData windowList)
    {
        var windows = windowList.Windows
            .Take(MaximumWindowResultCount)
            .Select(ToWindowInfo)
            .ToArray();
        return CreateWindowQueryToolResult(
            outcome,
            mode,
            windows,
            windowList.Count,
            windowList.Count > windows.Length,
            found: null,
            timeoutMs: null);
    }

    private static CallToolResult CreateWindowWaitToolResult(
        McpToolOutcome outcome,
        string mode,
        WindowWaitData wait)
    {
        McpWindowInfo[] windows = wait.Window is null ? [] : [ToWindowInfo(wait.Window)];
        return CreateWindowQueryToolResult(
            outcome,
            mode,
            windows,
            windows.Length,
            isTruncated: false,
            found: wait.Found,
            timeoutMs: wait.TimeoutMs);
    }

    private static McpWindowInfo ToWindowInfo(WindowInfoData window)
    {
        return new McpWindowInfo(
            window.Address,
            window.Title,
            window.Class,
            window.Pid,
            window.Workspace,
            window.IsFocused,
            window.IsFullscreen,
            window.IsMaximized,
            window.IsFloating,
            window.IsPinned,
            window.IsHidden,
            window.X,
            window.Y,
            window.Width,
            window.Height);
    }

    private static CallToolResult CreateListToolResult(
        string directoryPath,
        IReadOnlyList<McpMacroFile> macros,
        bool isTruncated,
        McpToolOutcome outcome)
    {
        return CreateToolResult(new McpMacroListResult(directoryPath, macros, isTruncated, outcome));
    }

    private static CallToolResult CreateInspectToolResult(McpToolOutcome outcome, McpMacroInfo? macro)
    {
        return CreateToolResult(new McpMacroInspectResult(outcome, macro));
    }

    private static CallToolResult CreateValidateToolResult(McpToolOutcome outcome, McpMacroSummary? macro)
    {
        return CreateToolResult(new McpMacroValidateResult(outcome, macro));
    }

    private static CallToolResult CreateAutomationStartToolResult(McpToolOutcome outcome, McpAutomationOperation? operation)
    {
        return CreateToolResult(new McpAutomationStartResult(outcome, operation));
    }

    private static CallToolResult CreateAutomationGetToolResult(McpToolOutcome outcome, McpAutomationOperation? operation)
    {
        return CreateToolResult(new McpAutomationGetResult(outcome, operation));
    }

    private static CallToolResult CreateAutomationStopToolResult(
        McpToolOutcome outcome,
        McpAutomationOperation? operation,
        bool cancellationInitiated)
    {
        return CreateToolResult(new McpAutomationStopResult(outcome, operation, cancellationInitiated));
    }

    private static CallToolResult CreateClipboardTextToolResult(McpToolOutcome outcome, string? text, int? length)
    {
        return CreateToolResult(new McpClipboardTextResult(
            outcome,
            text,
            length,
            MaximumClipboardTextCharacters));
    }

    private static CallToolResult CreateClipboardSetTextToolResult(McpToolOutcome outcome, int? length)
    {
        return CreateToolResult(new McpClipboardSetTextResult(
            outcome,
            length,
            MaximumClipboardTextCharacters));
    }

    private static CallToolResult CreateClipboardSetImageToolResult(
        McpToolOutcome outcome,
        int? width,
        int? height,
        int? pngByteCount)
    {
        return CreateToolResult(new McpClipboardSetImageResult(
            outcome,
            width,
            height,
            pngByteCount,
            MaximumClipboardImageBytes));
    }

    private static CallToolResult CreateClipboardImageToolResult(
        McpToolOutcome outcome,
        bool imageAvailable,
        int? width,
        int? height,
        ReadOnlyMemory<byte>? pngBytes,
        bool imageIncluded)
    {
        return CreateToolResult(new McpClipboardImageResult(
            outcome,
            imageAvailable,
            width,
            height,
            imageIncluded,
            pngBytes?.Length,
            MaximumClipboardImageBytes,
            MaximumInlineScreenshotBytes), pngBytes);
    }

    private static CallToolResult CreateWindowQueryToolResult(
        McpToolOutcome outcome,
        string mode,
        IReadOnlyList<McpWindowInfo> windows,
        int totalCount,
        bool isTruncated,
        bool? found,
        int? timeoutMs)
    {
        return CreateToolResult(new McpWindowQueryResult(
            outcome,
            mode,
            windows,
            totalCount,
            isTruncated,
            found,
            timeoutMs));
    }

    private static CallToolResult CreateWindowControlToolResult(
        McpToolOutcome outcome,
        string action,
        bool? changed,
        string? workspace,
        McpWindowInfo? window)
    {
        return new CallToolResult
        {
            Content = [new TextContentBlock { Text = outcome.Message }],
            StructuredContent = JsonSerializer.SerializeToElement(
                new McpWindowControlResult(outcome, action, changed, workspace, window),
                McpJsonContext.Default.McpWindowControlResult),
            IsError = !outcome.Success,
        };
    }

    private static CallToolResult CreateCommandExecuteToolResult(
        McpToolOutcome outcome,
        string command,
        bool operationStarted,
        string? operationId)
    {
        return new CallToolResult
        {
            Content = [new TextContentBlock { Text = outcome.Message }],
            StructuredContent = JsonSerializer.SerializeToElement(
                new McpCommandExecuteResult(outcome, command, operationStarted, operationId),
                McpJsonContext.Default.McpCommandExecuteResult),
            IsError = !outcome.Success,
        };
    }

    private static CallToolResult CreateScreenReadToolResult(
        McpToolOutcome outcome,
        string mode,
        McpScreenPoint? point,
        string? color,
        string? expectedColor,
        McpScreenRegion? region,
        int? tolerance,
        bool? found,
        int? timeoutMs,
        string? providerName)
    {
        return CreateToolResult(new McpScreenReadResult(
            outcome,
            mode,
            point,
            color,
            expectedColor,
            region,
            tolerance,
            found,
            timeoutMs,
            providerName));
    }

    private static CallToolResult CreateScreenImageSearchToolResult(
        McpToolOutcome outcome,
        bool? found,
        McpScreenPoint? point,
        double? score,
        McpScreenRegion? region,
        double? similarity,
        string? matchMode,
        string? providerName)
    {
        return CreateToolResult(new McpScreenImageSearchResult(
            outcome,
            found,
            point,
            score,
            region,
            similarity,
            matchMode,
            providerName));
    }

    private static CallToolResult CreateScreenshotCaptureToolResult(
        McpToolOutcome outcome,
        ScreenshotPngCaptureData? data,
        bool imageIncluded)
    {
        return CreateToolResult(new McpScreenshotCaptureResult(
            outcome,
            data?.Width,
            data?.Height,
            data?.Provider,
            data?.IsRegion,
            data?.OutputPath,
            data?.CopiedToClipboard,
            imageIncluded,
            data?.PngBytes.Length,
            MaximumInlineScreenshotBytes), data?.PngBytes);
    }

    private static CallToolResult CreateImageReadToolResult(
        McpToolOutcome outcome,
        int? width,
        int? height,
        ReadOnlyMemory<byte>? pngBytes,
        bool imageIncluded)
    {
        return CreateToolResult(new McpImageReadResult(
            outcome,
            width,
            height,
            imageIncluded,
            pngBytes?.Length,
            MaximumInlineScreenshotBytes), pngBytes);
    }

}
