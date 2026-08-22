namespace CrossMacro.Mcp.Tools;

public sealed class McpRuntimeTools(
    IRuntimeContext runtimeContext,
    IDoctorService doctorService,
    IProfileManager profileManager,
    IQuickSetupCliService quickSetupCliService,
    IMcpOperationCoordinator operationCoordinator,
    IMcpCapabilityPolicy capabilityPolicy,
    McpToolAuthorization authorization,
    IImageClipboardReader imageClipboardReader,
    IImageClipboardService imageClipboardService,
    ILinuxDaemonHandshakeProbe? daemonHandshakeProbe = null,
    ILinuxDaemonSocketAccessProbe? daemonSocketAccessProbe = null)
{
    private static readonly string[] AvailableToolNames = [.. CrossMacroMcpToolCatalog.V1.Select(static tool => tool.Name)];

    private readonly IRuntimeContext _runtimeContext = runtimeContext;
    private readonly IDoctorService _doctorService = doctorService;
    private readonly IProfileManager _profileManager = profileManager;
    private readonly IQuickSetupCliService _quickSetupCliService = quickSetupCliService;
    private readonly IMcpOperationCoordinator _operationCoordinator = operationCoordinator;
    private readonly IMcpCapabilityPolicy _capabilityPolicy = capabilityPolicy;
    private readonly McpToolAuthorization _authorization = authorization;
    private readonly IImageClipboardReader _imageClipboardReader = imageClipboardReader;
    private readonly IImageClipboardService _imageClipboardService = imageClipboardService;
    private readonly ILinuxDaemonHandshakeProbe? _daemonHandshakeProbe = daemonHandshakeProbe;
    private readonly ILinuxDaemonSocketAccessProbe? _daemonSocketAccessProbe = daemonSocketAccessProbe;

    [McpServerTool(Name = "status.get", Title = "Get CrossMacro status", ReadOnly = true, Destructive = false, Idempotent = true, UseStructuredContent = true, OutputSchemaType = typeof(McpStatusResult))]
    [Description("Gets CrossMacro runtime and desktop-session status without changing the desktop.")]
    public async Task<McpStatusResult> GetStatusAsync(CancellationToken cancellationToken)
    {
        var capability = _authorization.Require(McpCapability.StatusRead);
        if (capability is not null)
        {
            return CreateUnavailableStatus();
        }

        var doctorReport = await _doctorService.RunAsync(verbose: false, cancellationToken).ConfigureAwait(false);
        var activeProfile = _profileManager.ActiveProfile;
        return new McpStatusResult(
            Runtime: "mcp",
            ProductVersion: GetVersion(),
            OperatingSystem: GetOperatingSystem(),
            SessionType: _runtimeContext.SessionType,
            IsFlatpak: _runtimeContext.IsFlatpak,
            ActiveProfile: new McpActiveProfile(activeProfile.Id, activeProfile.Name),
            Capabilities: new McpCapabilitySummary(
                doctorReport.HasFailures,
                doctorReport.HasWarnings,
                doctorReport.Checks.Select(static check => new McpCapabilityStatus(check.Name, check.Status.ToString().ToLowerInvariant(), GetCapabilityMessage(check.Status))).ToArray()),
            ImageClipboard: new McpImageClipboardCapability(_imageClipboardReader.IsSupported, _imageClipboardService.IsSupported),
            ActiveOperation: _operationCoordinator.GetActive(),
            Policy: "capability-policy-v1",
            IsRestricted: _capabilityPolicy.IsRestricted,
            EnabledCapabilities: Enum.GetValues<McpCapability>().Where(_capabilityPolicy.IsAllowed).Select(static capability => capability.ToString()).ToArray(),
            AvailableTools: AvailableToolNames);
    }

    [McpServerTool(Name = "help.get", Title = "Get CrossMacro MCP help", ReadOnly = true, Destructive = false, Idempotent = true, UseStructuredContent = true, OutputSchemaType = typeof(McpHelpResult))]
    [Description("Gets safe usage guidance and the currently available CrossMacro MCP tools.")]
    public McpHelpResult GetHelp() => new(
        "local-stdio",
        "MCP uses local stdio. Multiple MCP sessions may run, and MCP may run alongside GUI or headless; GUI and headless remain mutually exclusive.",
        "Use cursor.position to read the current global cursor. Use automation.start with kind=run and steps for input, for example ['mouse position mouse_x mouse_y', 'move abs 1 1']; use command.execute only with a CLI command token and argument array.",
        CrossMacroMcpToolCatalog.V1.Select(tool => new McpAvailableTool(tool.Name, tool.Description, tool.Access.ToString(), IsToolEnabled(tool), GetOperationCapabilityStatuses(tool))).ToArray(),
        _capabilityPolicy.IsRestricted);

    [McpServerTool(Name = "setup.status", Title = "Get setup status", ReadOnly = true, Destructive = false, Idempotent = true, UseStructuredContent = true, OutputSchemaType = typeof(McpSetupResult))]
    public McpSetupResult GetSetupStatus()
    {
        var status = _quickSetupCliService.GetStatus();
        return new McpSetupResult(
            Action: "status",
            Outcome: McpToolOutcomeMapper.Success("Setup status retrieved."),
            Applicable: status.Applicable,
            Provider: status.Provider,
            ShouldPrompt: status.ShouldPrompt,
            Executed: false);
    }

    [McpServerTool(Name = "setup.run", Title = "Run temporary setup", ReadOnly = false, Destructive = true, Idempotent = true, UseStructuredContent = true, OutputSchemaType = typeof(McpSetupResult))]
    public async Task<McpSetupResult> RunSetupAsync(CancellationToken cancellationToken = default)
    {
        var status = _quickSetupCliService.GetStatus();
        var capability = _authorization.Require(McpCapability.PrivilegeElevation);
        if (capability is not null)
        {
            return new McpSetupResult(
                Action: "run",
                Outcome: capability,
                Applicable: status.Applicable,
                Provider: status.Provider,
                ShouldPrompt: status.ShouldPrompt,
                Executed: false);
        }

        if (!status.Applicable)
        {
            return new McpSetupResult(
                Action: "run",
                Outcome: McpToolOutcomeMapper.EnvironmentError("Temporary input setup is not applicable in this session."),
                Applicable: status.Applicable,
                Provider: status.Provider,
                ShouldPrompt: status.ShouldPrompt,
                Executed: false);
        }

        var result = await _quickSetupCliService.RunAsync(cancellationToken).ConfigureAwait(false);
        var outcome = result.Result.Success ? McpToolOutcomeMapper.Success("Temporary input setup completed.") : McpToolOutcomeMapper.EnvironmentError("Temporary input setup failed.");
        return new McpSetupResult(
            Action: "run",
            Outcome: outcome,
            Applicable: result.Applicable,
            Provider: result.Provider,
            ShouldPrompt: status.ShouldPrompt,
            Executed: true);
    }

    [McpServerTool(Name = "daemon.status", Title = "Get Linux daemon status", ReadOnly = true, Destructive = false, Idempotent = true, UseStructuredContent = true, OutputSchemaType = typeof(McpDaemonResult))]
    public async Task<McpDaemonResult> GetDaemonStatusAsync(CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsLinux() || _daemonHandshakeProbe is null || _daemonSocketAccessProbe is null)
        {
            return new McpDaemonResult(
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
        var outcome = handshake.Succeeded && access.IsAccessible
            ? McpToolOutcomeMapper.Success("Linux daemon status retrieved.")
            : McpToolOutcomeMapper.EnvironmentError("Linux daemon is not ready.");
        return new McpDaemonResult(
            Action: "status",
            Outcome: outcome,
            SocketPath: socketPath,
            HandshakeStatus: handshake.Status.ToString(),
            SocketAccessStatus: access.Status.ToString(),
            Message: handshake.Message ?? access.Message,
            LinuxOnly: true);
    }

    private McpStatusResult CreateUnavailableStatus() => new(
        Runtime: "mcp",
        ProductVersion: GetVersion(),
        OperatingSystem: GetOperatingSystem(),
        SessionType: _runtimeContext.SessionType,
        IsFlatpak: _runtimeContext.IsFlatpak,
        ActiveProfile: new McpActiveProfile("unknown", "unknown"),
        Capabilities: new McpCapabilitySummary(HasFailures: true, HasWarnings: false, Checks: []),
        ImageClipboard: new McpImageClipboardCapability(ReadSupported: false, WriteSupported: false),
        ActiveOperation: null,
        Policy: "capability-policy-v1",
        IsRestricted: _capabilityPolicy.IsRestricted,
        EnabledCapabilities: [],
        AvailableTools: []);

    private bool IsToolEnabled(McpToolDefinition definition)
    {
        if (definition.OperationCapabilities.Count > 0)
        {
            return definition.OperationCapabilities.Any(operation => operation.Capabilities.All(_capabilityPolicy.IsAllowed));
        }

        return definition.CapabilityRequirement is McpCapabilityRequirement.Any
            ? _capabilityPolicy.IsAnyAllowed([.. definition.Capabilities])
            : definition.Capabilities.All(_capabilityPolicy.IsAllowed);
    }

    private IReadOnlyList<McpToolCapabilityStatus> GetOperationCapabilityStatuses(McpToolDefinition definition) =>
        definition.OperationCapabilities.Select(operation => new McpToolCapabilityStatus(
            Operation: operation.Operation,
            RequiredCapabilities: operation.Capabilities.Select(static capability => capability.ToString()).ToArray(),
            Enabled: operation.Capabilities.All(_capabilityPolicy.IsAllowed))).ToArray();

    private string GetOperatingSystem() => _runtimeContext switch
    {
        { IsLinux: true } => "linux",
        { IsWindows: true } => "windows",
        { IsMacOS: true } => "macos",
        _ => "unknown",
    };

    private static string GetVersion() => typeof(McpRuntimeTools).Assembly.GetName().Version?.ToString() ?? "unknown";

    private static string GetCapabilityMessage(DoctorCheckStatus status) => status switch
    {
        DoctorCheckStatus.Pass => "Available.",
        DoctorCheckStatus.Warn => "May require attention.",
        DoctorCheckStatus.Fail => "Unavailable.",
        _ => "Unknown.",
    };
}
