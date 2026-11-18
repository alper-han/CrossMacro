namespace CrossMacro.Mcp.Tests;

public sealed class McpRuntimeToolsTests
{
    [Fact]
    public async Task GetStatusAsync_ShouldReturnTheRuntimeContextProfileAndRedactedDoctorChecks()
    {
        var doctorService = new TestDoctorService(new DoctorReport
        {
            Checks =
            [
                new DoctorCheck { Name = "display-session", Status = DoctorCheckStatus.Pass, Message = "Display session is supported." },
                new DoctorCheck { Name = "linux-uinput", Status = DoctorCheckStatus.Warn, Message = "/home/user/private-provider-detail" },
            ],
        });
        var profileManager = McpToolTestFactory.CreateProfileManager();
        var scheduleCommands = new TestScheduleCommands();
        var shortcutCommands = new TestShortcutCommands();
        var triggerCommands = new TestTriggerCommands();
        var operationCoordinator = McpToolTestFactory.CreateOperationCoordinator();
        var tools = McpToolTestFactory.CreateRuntimeTools(
            runtimeContext: new TestRuntimeContext(),
            doctorService: doctorService,
            profileManager: profileManager,
            operationCoordinator: operationCoordinator,
            scheduleCommands: scheduleCommands,
            shortcutCommands: shortcutCommands,
            triggerCommands: triggerCommands);

        var result = await tools.GetStatusAsync(CancellationToken.None);

        Assert.Equal("mcp", result.Runtime);
        Assert.False(string.IsNullOrWhiteSpace(result.ProductVersion));
        Assert.Equal("linux", result.OperatingSystem);
        Assert.Equal("wayland", result.SessionType);
        Assert.True(result.IsFlatpak);
        Assert.Equal("work", result.ActiveProfile.Id);
        Assert.Equal("Work", result.ActiveProfile.Name);
        Assert.False(result.Capabilities.HasFailures);
        Assert.True(result.Capabilities.HasWarnings);
        Assert.False(result.ImageClipboard.ReadSupported);
        Assert.True(result.ImageClipboard.WriteSupported);
        Assert.Null(result.ActiveOperation);
        Assert.Collection(
            result.Capabilities.Checks,
            check =>
            {
                Assert.Equal("display-session", check.Name);
                Assert.Equal("pass", check.Status);
                Assert.Equal("Available.", check.Message);
            },
            check =>
            {
                Assert.Equal("linux-uinput", check.Name);
                Assert.Equal("warn", check.Status);
                Assert.Equal("May require attention.", check.Message);
            });
        Assert.Equal("capability-policy-v1", result.Policy);
        Assert.False(result.IsRestricted);
        Assert.Equal(CrossMacroMcpToolCatalog.V1.Select(static definition => definition.Name), result.AvailableTools, StringComparer.Ordinal);
        Assert.True(doctorService.WasRun);
    }

    [Fact]
    public async Task GetStatusAsync_ShouldReportTheActiveAutomationOperation()
    {
        using var coordinator = new McpOperationCoordinator();
        var start = coordinator.Start(
            McpAutomationOperationKind.Play,
            async cancellationToken =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, TimeProvider.System, cancellationToken).ConfigureAwait(false);
                return OperationResult.Ok("Playback complete.");
            },
            CancellationToken.None);
        var activeOperation = Assert.IsType<McpAutomationOperation>(start.Operation);
        var tools = McpToolTestFactory.CreateRuntimeTools(operationCoordinator: coordinator);

        var status = await tools.GetStatusAsync(CancellationToken.None);

        var reportedOperation = Assert.IsType<McpAutomationOperation>(status.ActiveOperation);
        Assert.Equal(activeOperation.OperationId, reportedOperation.OperationId);
        Assert.Equal(McpAutomationOperationKind.Play, reportedOperation.Kind);
        Assert.Equal(McpAutomationOperationState.Running, reportedOperation.State);
        Assert.Null(reportedOperation.Outcome);
    }

    [Fact]
    public void SetupStatus_ShouldExposeProviderApplicabilityWithoutElevation()
    {
        var tools = McpToolTestFactory.CreateRuntimeTools(quickSetupCommandService: new TestQuickSetupCommandService
        {
            Status = new QuickSetupStatus(Applicable: true, "appimage", ShouldPrompt: true),
        });

        var result = tools.GetSetupStatus();

        Assert.True(result.Applicable);
        Assert.Equal("appimage", result.Provider);
        Assert.True(result.ShouldPrompt);
        Assert.False(result.Executed);
        Assert.True(result.Outcome.Success);
    }

    [Fact]
    public async Task DaemonStatus_WithoutDiagnostics_ReportsUnavailableWithoutOpeningRawIpc()
    {
        var result = await McpToolTestFactory.CreateRuntimeTools().GetDaemonStatusAsync(CancellationToken.None);

        Assert.False(result.Outcome.Success);
        Assert.Equal("unavailable", result.HandshakeStatus);
        Assert.Equal("unavailable", result.SocketAccessStatus);
        Assert.True(result.LinuxOnly);
    }

    [LinuxFact]
    public async Task DaemonStatus_WhenThePackageHasNoDaemon_ReportsUnavailable()
    {
        var tools = McpToolTestFactory.CreateRuntimeTools(
            linuxInputDiagnostics: new FakeInputDiagnostics(socketAccess: null, LinuxDaemonHandshakeStatus.MissingSocket));

        var result = await tools.GetDaemonStatusAsync(CancellationToken.None);

        Assert.False(result.Outcome.Success);
        Assert.Equal("unavailable", result.HandshakeStatus);
        Assert.Equal("unavailable", result.SocketAccessStatus);
    }

    [LinuxFact]
    public async Task DaemonStatus_WithDiagnostics_ReportsTheObservedHandshakeAndSocketAccess()
    {
        var tools = McpToolTestFactory.CreateRuntimeTools(
            linuxInputDiagnostics: new FakeInputDiagnostics(SocketAccess(LinuxDaemonSocketAccessStatus.PermissionDenied), LinuxDaemonHandshakeStatus.Timeout));

        var result = await tools.GetDaemonStatusAsync(CancellationToken.None);

        Assert.False(result.Outcome.Success);
        Assert.Equal("Timeout", result.HandshakeStatus);
        Assert.Equal("PermissionDenied", result.SocketAccessStatus);
    }

    [LinuxFact]
    public async Task DaemonStatus_WhenHandshakeSucceedsAndSocketIsAccessible_Succeeds()
    {
        var tools = McpToolTestFactory.CreateRuntimeTools(
            linuxInputDiagnostics: new FakeInputDiagnostics(SocketAccess(LinuxDaemonSocketAccessStatus.Accessible), LinuxDaemonHandshakeStatus.Success));

        var result = await tools.GetDaemonStatusAsync(CancellationToken.None);

        Assert.True(result.Outcome.Success);
    }

    [Fact]
    public void GetHelp_ShouldDescribeTheLocalStdioContractAndOnlyImplementedTools()
    {
        var result = McpToolTestFactory.CreateRuntimeTools().GetHelp();

        Assert.Equal("local-stdio", result.Transport);
        Assert.Contains("Multiple MCP sessions may run", result.RuntimeRule, StringComparison.Ordinal);
        Assert.Contains("cursor.position", result.SafetyNote, StringComparison.Ordinal);
        Assert.Equal(
            CrossMacroMcpToolCatalog.V1.Select(static definition => definition.Name),
            result.AvailableTools.Select(static tool => tool.Name),
            StringComparer.Ordinal);
        Assert.All(result.AvailableTools, tool => Assert.Equal(
            CrossMacroMcpToolCatalog.V1.Single(definition => string.Equals(definition.Name, tool.Name, StringComparison.Ordinal)).Access.ToString(),
            tool.Access));

        var automation = Assert.Single(result.AvailableTools, static tool => tool.Name is "automation.start");
        Assert.Equal(
            ["play", "run", "record"],
            automation.OperationCapabilities.Select(static item => item.Operation),
            StringComparer.Ordinal);
        Assert.All(automation.OperationCapabilities, static item => Assert.True(item.Enabled));
        Assert.Equal(["MacroRead", "InputAutomation"], automation.OperationCapabilities[0].RequiredCapabilities);
        Assert.Equal(["CommandExecute", "InputAutomation"], automation.OperationCapabilities[1].RequiredCapabilities);
        Assert.Equal(["Recording", "FileWrite"], automation.OperationCapabilities[2].RequiredCapabilities);
    }

    [Fact]
    public async Task RegisteredTools_ShouldExposeAndServeTheImplementedMcpTools()
    {
        var scheduleCommands = new TestScheduleCommands();
        var shortcutCommands = new TestShortcutCommands();
        var triggerCommands = new TestTriggerCommands();
        var operationCoordinator = McpToolTestFactory.CreateOperationCoordinator();
        var commandTools = McpToolTestFactory.CreateCommandTools(
            operationCoordinator: operationCoordinator,
            scheduleCommands: scheduleCommands,
            shortcutCommands: shortcutCommands,
            triggerCommands: triggerCommands);
        var screenTools = McpToolTestFactory.CreateScreenTools(
            screenReader: new FakeScreenReader
            {
                PixelResult = ScreenReadResultFactory.Success(new ScreenPixelColor(0, 0, 0)),
            },
            screenshotCaptureService: new TestScreenshotCaptureService
            {
                Result = ScreenshotPngCaptureResult.Ok(new ScreenshotPngCaptureData(
                    McpTestData.CreatePngBytes(),
                    OutputPath: null,
                    Width: 1,
                    Height: 1,
                    Provider: "test",
                    IsRegion: false,
                    CopiedToClipboard: false)),
            },
            imageAssetCodec: new TestImageAssetCodec
            {
                PngBytes = McpTestData.CreatePngBytes(),
                Frame = McpTestData.CreateImageFrame(),
            },
            scheduleCommands: scheduleCommands,
            shortcutCommands: shortcutCommands,
            triggerCommands: triggerCommands);
        var runtimeTools = McpToolTestFactory.CreateRuntimeTools(
            operationCoordinator: operationCoordinator,
            imageClipboardReader: new TestImageClipboardReader(),
            scheduleCommands: scheduleCommands,
            shortcutCommands: shortcutCommands,
            triggerCommands: triggerCommands);
        var settingsTools = McpToolTestFactory.CreateSettingsTools(
            scheduleCommands: scheduleCommands,
            shortcutCommands: shortcutCommands,
            triggerCommands: triggerCommands);
        var profileTools = McpToolTestFactory.CreateProfileTools(
            scheduleCommands: scheduleCommands,
            shortcutCommands: shortcutCommands,
            triggerCommands: triggerCommands);
        var textExpansionTools = McpToolTestFactory.CreateTextExpansionTools(
            scheduleCommands: scheduleCommands,
            shortcutCommands: shortcutCommands,
            triggerCommands: triggerCommands);
        var taskTools = McpToolTestFactory.CreateTaskTools(
            scheduleCommands: scheduleCommands,
            shortcutCommands: shortcutCommands,
            triggerCommands: triggerCommands);
        var automationTools = McpToolTestFactory.CreateAutomationTools(
            operationCoordinator: operationCoordinator,
            scheduleCommands: scheduleCommands,
            shortcutCommands: shortcutCommands,
            triggerCommands: triggerCommands);
        var macroTools = McpToolTestFactory.CreateMacroTools(
            scheduleCommands: scheduleCommands,
            shortcutCommands: shortcutCommands,
            triggerCommands: triggerCommands);
        var clipboardTools = McpToolTestFactory.CreateClipboardTools(
            clipboardCommandService: new TestClipboardCommandService
            {
                GetResult = OperationResult.Ok("Clipboard text read.", new ClipboardTextData("protocol text")),
                SetResult = OperationResult.Ok("Clipboard text set.", new ClipboardSetData(13, "text")),
            },
            imageAssetCodec: new TestImageAssetCodec
            {
                PngBytes = McpTestData.CreatePngBytes(),
                Frame = McpTestData.CreateImageFrame(),
            },
            imageClipboardReader: new TestImageClipboardReader(),
            scheduleCommands: scheduleCommands,
            shortcutCommands: shortcutCommands,
            triggerCommands: triggerCommands);
        var windowTools = McpToolTestFactory.CreateWindowTools(
            windowCommandService: new TestWindowCommandService
            {
                Result = OperationResult.Ok("Windows listed.", new WindowListData([], Count: 0)),
            },
            scheduleCommands: scheduleCommands,
            shortcutCommands: shortcutCommands,
            triggerCommands: triggerCommands);
        await using var server = await InProcessMcpServer.StartAsync(builder => builder.WithCrossMacroTools(
            runtimeTools,
            settingsTools,
            profileTools,
            textExpansionTools,
            taskTools,
            automationTools,
            commandTools,
            macroTools,
            clipboardTools,
            windowTools,
            screenTools));
        var client = server.Client;
        var cancellation = server.Token;

        var discoveredTools = await client.ListToolsAsync(cancellationToken: cancellation);
        Assert.Equal(
            CrossMacroMcpToolCatalog.V1.Select(static definition => definition.Name).Order(StringComparer.Ordinal),
            discoveredTools.Select(static tool => tool.Name).Order(StringComparer.Ordinal),
            StringComparer.Ordinal);
        Assert.All(discoveredTools, tool =>
        {
            var definition = Assert.Single(CrossMacroMcpToolCatalog.V1, candidate => string.Equals(candidate.Name, tool.Name, StringComparison.Ordinal));
            Assert.Equal(definition.Title, tool.ProtocolTool.Title);
            var annotations = Assert.IsType<ToolAnnotations>(tool.ProtocolTool.Annotations);
            Assert.Equal(definition.Access is McpToolAccess.ReadOnly, annotations.ReadOnlyHint);
            if (definition.Access is McpToolAccess.ReadOnly)
            {
                Assert.NotEqual(true, annotations.DestructiveHint);
            }
        });
        Assert.All(discoveredTools, static tool =>
        {
            Assert.Equal(JsonValueKind.Object, tool.ProtocolTool.InputSchema.ValueKind);
            _ = Assert.NotNull(tool.ProtocolTool.OutputSchema);
        });
        var status = await client.CallToolAsync("status.get", cancellationToken: cancellation);
        var help = await client.CallToolAsync("help.get", cancellationToken: cancellation);
        var invalidMacro = await client.CallToolAsync(
            "macro.inspect",
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["macroPath"] = "relative.macro" },
            cancellationToken: cancellation);
        var invalidDirectory = await client.CallToolAsync(
            "macro.list",
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["directoryPath"] = "relative" },
            cancellationToken: cancellation);
        var clipboardText = await client.CallToolAsync(
            "clipboard.get_text",
            cancellationToken: cancellation);
        var clipboardSet = await client.CallToolAsync(
            "clipboard.set_text",
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["text"] = "protocol text" },
            cancellationToken: cancellation);
        var clipboardImage = await client.CallToolAsync(
            "clipboard.get_image",
            cancellationToken: cancellation);
        var invalidClipboardImageWrite = await client.CallToolAsync(
            "clipboard.set_image",
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["imagePath"] = "relative.png" },
            cancellationToken: cancellation);
        var windows = await client.CallToolAsync(
            "window.query",
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["mode"] = "list" },
            cancellationToken: cancellation);
        var pixel = await client.CallToolAsync(
            "screen.read",
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["mode"] = "pixel",
                ["x"] = 0,
                ["y"] = 0,
            },
            cancellationToken: cancellation);
        var invalidImage = await client.CallToolAsync(
            "screen.find_image",
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["imagePath"] = "relative.png" },
            cancellationToken: cancellation);
        var screenshot = await client.CallToolAsync(
            "screenshot.capture",
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["includeImage"] = true },
            cancellationToken: cancellation);
        var invalidImageRead = await client.CallToolAsync(
            "image.read",
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["imagePath"] = "relative.png" },
            cancellationToken: cancellation);
        var invalidAutomation = await client.CallToolAsync(
            "automation.start",
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["kind"] = "shell" },
            cancellationToken: cancellation);
        var invalidAutomationGet = await client.CallToolAsync(
            "automation.get",
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["operationId"] = "bad" },
            cancellationToken: cancellation);
        var invalidAutomationStop = await client.CallToolAsync(
            "automation.stop",
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["operationId"] = "bad" },
            cancellationToken: cancellation);
        var invalidCommand = await client.CallToolAsync(
            "command.execute",
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["command"] = "mcp" },
            cancellationToken: cancellation);

        Assert.True(status.IsError is not true, string.Join(Environment.NewLine, status.Content.Select(static content => content.ToString())));
        Assert.NotEqual(true, help.IsError);
        Assert.Equal(true, invalidMacro.IsError);
        Assert.Equal(true, invalidDirectory.IsError);
        Assert.NotEqual(true, clipboardText.IsError);
        Assert.NotEqual(true, clipboardSet.IsError);
        Assert.NotEqual(true, clipboardImage.IsError);
        Assert.Equal(true, invalidClipboardImageWrite.IsError);
        Assert.NotEqual(true, windows.IsError);
        Assert.NotEqual(true, pixel.IsError);
        Assert.Equal(true, invalidImage.IsError);
        Assert.NotEqual(true, screenshot.IsError);
        Assert.Equal(true, invalidImageRead.IsError);
        Assert.Equal(true, invalidAutomation.IsError);
        Assert.Equal(true, invalidAutomationGet.IsError);
        Assert.Equal(true, invalidAutomationStop.IsError);
        Assert.Equal(true, invalidCommand.IsError);
    }

    private static LinuxDaemonSocketAccessResult SocketAccess(LinuxDaemonSocketAccessStatus status) =>
        new(FakeInputDiagnostics.SocketPath, status);

    private sealed class FakeInputDiagnostics(LinuxDaemonSocketAccessResult? socketAccess, LinuxDaemonHandshakeStatus handshake) : ILinuxInputDiagnostics
    {
        internal const string SocketPath = "/run/crossmacro/crossmacro.sock";

        private readonly LinuxInputDiagnosticSnapshot _snapshot = new(
            socketAccess,
            handshake is LinuxDaemonHandshakeStatus.Success
                ? LinuxDaemonHandshakeProbeResult.Success(SocketPath, TimeSpan.Zero)
                : LinuxDaemonHandshakeProbeResult.Failed(SocketPath, TimeSpan.Zero, handshake),
            LinuxDirectInputFallbackResult.FromAccess(canWriteUInput: false, canReadInputEvents: false),
            default,
            default,
            GsrVirtualKeyboardDetected: false);

        public ValueTask<LinuxInputDiagnosticSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(_snapshot);
    }
}
