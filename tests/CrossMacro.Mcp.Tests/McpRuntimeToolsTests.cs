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
        var scheduleCliService = new TestScheduleCliService();
        var shortcutCliService = new TestShortcutCliService();
        var triggerCliService = new TestTriggerCliService();
        var operationCoordinator = McpToolTestFactory.CreateOperationCoordinator();
        var tools = McpToolTestFactory.CreateRuntimeTools(
            runtimeContext: new TestRuntimeContext(),
            doctorService: doctorService,
            profileManager: profileManager,
            operationCoordinator: operationCoordinator,
            scheduleCliService: scheduleCliService,
            shortcutCliService: shortcutCliService,
            triggerCliService: triggerCliService);

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
        Assert.Equal(
            [
                 "status.get",
                 "help.get",
                 "setup.status",
                 "setup.run",
                 "daemon.status",
                 "settings.get",
                "settings.set",
                 "settings.list_keys",
                 "settings.reset",
                 "profile.list",
                 "profile.current",
                 "profile.create",
                 "profile.switch",
                 "profile.rename",
                 "profile.delete",
                 "text_expansion.list",
                 "text_expansion.add",
                 "text_expansion.remove",
                 "text_expansion.enable",
                 "text_expansion.disable",
                 "text_expansion.test",
                 "schedule.list",
                 "schedule.run",
                 "schedule.add",
                 "schedule.edit",
                 "schedule.remove",
                 "schedule.enable",
                 "schedule.disable",
                 "schedule.next",
                 "shortcut.list",
                 "shortcut.run",
                 "shortcut.add",
                 "shortcut.edit",
                 "shortcut.remove",
                 "shortcut.enable",
                 "shortcut.disable",
                 "shortcut.bind",
                 "trigger.list",
                 "trigger.add",
                 "trigger.edit",
                 "trigger.remove",
                 "trigger.enable",
                 "trigger.disable",
                  "command.execute",
                "automation.start",
                "automation.get",
                "automation.stop",
                "macro.list",
                "macro.inspect",
                "macro.validate",
                "clipboard.get_text",
                 "clipboard.set_text",
                 "clipboard.get_image",
                 "clipboard.set_image",
                 "window.query",
                 "window.control",
                 "screen.read",
                 "cursor.position",
                 "screen.find_image",
                "image.read",
                "screenshot.capture",
            ],
            result.AvailableTools);
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
                return CliCommandExecutionResult.Ok("Playback complete.");
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
        var tools = McpToolTestFactory.CreateRuntimeTools(quickSetupCliService: new TestQuickSetupCliService
        {
            Status = new QuickSetupStatus(true, "appimage", ShouldPrompt: true),
        });

        var result = tools.GetSetupStatus();

        Assert.True(result.Applicable);
        Assert.Equal("appimage", result.Provider);
        Assert.True(result.ShouldPrompt);
        Assert.False(result.Executed);
        Assert.True(result.Outcome.Success);
    }

    [Fact]
    public async Task SetupRun_ShouldBeDeniedWhenPrivilegeElevationIsNotExplicitlyEnabled()
    {
        var tools = McpToolTestFactory.CreateRuntimeTools(
            quickSetupCliService: new TestQuickSetupCliService(),
            capabilityPolicy: new McpCapabilityPolicy(new TestSettingsService(new AppSettings())));

        var result = await tools.RunSetupAsync(CancellationToken.None);

        Assert.False(result.Executed);
        Assert.False(result.Outcome.Success);
        Assert.Equal("capability_denied", Assert.Single(result.Outcome.Errors).Code);
    }

    [Fact]
    public async Task DaemonStatus_ShouldReturnUnavailableOutsideLinuxWithoutOpeningRawIpc()
    {
        var result = await McpToolTestFactory.CreateRuntimeTools().GetDaemonStatusAsync(CancellationToken.None);

        if (!OperatingSystem.IsLinux())
        {
            Assert.False(result.Outcome.Success);
            Assert.Equal("unavailable", result.HandshakeStatus);
            Assert.Equal("unavailable", result.SocketAccessStatus);
            Assert.True(result.LinuxOnly);
        }
    }

    [Fact]
    public void GetHelp_ShouldDescribeTheLocalStdioContractAndOnlyImplementedTools()
    {
        var result = McpToolTestFactory.CreateRuntimeTools().GetHelp();

        Assert.Equal("local-stdio", result.Transport);
        Assert.Contains("Multiple MCP sessions may run", result.RuntimeRule, StringComparison.Ordinal);
        Assert.Contains("cursor.position", result.SafetyNote, StringComparison.Ordinal);
        Assert.Equal(
        [
             "status.get",
             "help.get",
             "setup.status",
             "setup.run",
             "daemon.status",
             "settings.get",
            "settings.set",
             "settings.list_keys",
             "settings.reset",
             "profile.list",
             "profile.current",
             "profile.create",
             "profile.switch",
             "profile.rename",
              "profile.delete",
              "text_expansion.list",
              "text_expansion.add",
              "text_expansion.remove",
              "text_expansion.enable",
              "text_expansion.disable",
              "text_expansion.test",
              "schedule.list",
              "schedule.run",
              "schedule.add",
              "schedule.edit",
              "schedule.remove",
              "schedule.enable",
              "schedule.disable",
              "schedule.next",
              "shortcut.list",
              "shortcut.run",
              "shortcut.add",
              "shortcut.edit",
              "shortcut.remove",
              "shortcut.enable",
              "shortcut.disable",
              "shortcut.bind",
              "trigger.list",
              "trigger.add",
              "trigger.edit",
              "trigger.remove",
              "trigger.enable",
              "trigger.disable",
              "command.execute",
            "automation.start",
            "automation.get",
            "automation.stop",
            "macro.list",
            "macro.inspect",
            "macro.validate",
            "clipboard.get_text",
            "clipboard.set_text",
             "clipboard.get_image",
             "clipboard.set_image",
             "window.query",
            "window.control",
             "screen.read",
             "cursor.position",
             "screen.find_image",
            "image.read",
            "screenshot.capture",
        ],
         result.AvailableTools.Select(static tool => tool.Name),
         StringComparer.Ordinal);
        Assert.All(
              result.AvailableTools.Where(static tool => tool.Name is not "command.execute" and not "clipboard.set_text" and not "clipboard.set_image" and not "screenshot.capture" and not "automation.start" and not "automation.stop" and not "window.control" and not "settings.set" and not "settings.reset" and not "profile.create" and not "profile.switch" and not "profile.rename" and not "profile.delete" and not "text_expansion.add" and not "text_expansion.remove" and not "text_expansion.enable" and not "text_expansion.disable" and not "schedule.run" and not "schedule.add" and not "schedule.edit" and not "schedule.remove" and not "schedule.enable" and not "schedule.disable" and not "shortcut.run" and not "shortcut.add" and not "shortcut.edit" and not "shortcut.remove" and not "shortcut.enable" and not "shortcut.disable" and not "shortcut.bind" and not "trigger.add" and not "trigger.edit" and not "trigger.remove" and not "trigger.enable" and not "trigger.disable" and not "setup.run"),
             tool => Assert.Equal("ReadOnly", tool.Access));
        Assert.Equal(
            "Effectful",
            Assert.Single(result.AvailableTools, static tool => tool.Name is "clipboard.set_text").Access);
        Assert.Equal(
            "Effectful",
            Assert.Single(result.AvailableTools, static tool => tool.Name is "screenshot.capture").Access);
        Assert.Equal(
            "Effectful",
            Assert.Single(result.AvailableTools, static tool => tool.Name is "automation.start").Access);
        Assert.Equal(
            "Effectful",
            Assert.Single(result.AvailableTools, static tool => tool.Name is "automation.stop").Access);
        Assert.Equal(
            "Effectful",
            Assert.Single(result.AvailableTools, static tool => tool.Name is "window.control").Access);
        Assert.Equal(
            "Effectful",
            Assert.Single(result.AvailableTools, static tool => tool.Name is "command.execute").Access);

        var automation = Assert.Single(result.AvailableTools, static tool => tool.Name is "automation.start");
        Assert.Equal(
            ["play", "run", "record"],
            automation.OperationCapabilities.Select(static item => item.Operation),
            StringComparer.Ordinal);
        Assert.All(automation.OperationCapabilities, static item => Assert.True(item.Enabled));
        Assert.Equal(["MacroRead", "InputAutomation"], automation.OperationCapabilities[0].RequiredCapabilities);
        Assert.Equal(["CommandExecute"], automation.OperationCapabilities[1].RequiredCapabilities);
        Assert.Equal(["Recording", "FileWrite"], automation.OperationCapabilities[2].RequiredCapabilities);
    }

    [Fact]
    public async Task RegisteredTools_ShouldExposeAndServeTheImplementedMcpTools()
    {
        var clientToServer = new Pipe();
        var serverToClient = new Pipe();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var scheduleCliService = new TestScheduleCliService();
        var shortcutCliService = new TestShortcutCliService();
        var triggerCliService = new TestTriggerCliService();
        var operationCoordinator = McpToolTestFactory.CreateOperationCoordinator();
        var commandTools = McpToolTestFactory.CreateCommandTools(
            operationCoordinator: operationCoordinator,
            scheduleCliService: scheduleCliService,
            shortcutCliService: shortcutCliService,
            triggerCliService: triggerCliService);
        var screenTools = McpToolTestFactory.CreateScreenTools(
            screenCliService: new TestScreenCliService
            {
                Result = CliCommandExecutionResult.Ok("Pixel 0,0: 000000", new ScreenPixelData(0, 0, "000000", "test", Relative: false)),
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
            scheduleCliService: scheduleCliService,
            shortcutCliService: shortcutCliService,
            triggerCliService: triggerCliService);
        var services = new ServiceCollection();
        var runtimeTools = McpToolTestFactory.CreateRuntimeTools(
            operationCoordinator: operationCoordinator,
            imageClipboardReader: new TestImageClipboardReader(),
            scheduleCliService: scheduleCliService,
            shortcutCliService: shortcutCliService,
            triggerCliService: triggerCliService);
        var settingsTools = McpToolTestFactory.CreateSettingsTools(
            scheduleCliService: scheduleCliService,
            shortcutCliService: shortcutCliService,
            triggerCliService: triggerCliService);
        var profileTools = McpToolTestFactory.CreateProfileTools(
            scheduleCliService: scheduleCliService,
            shortcutCliService: shortcutCliService,
            triggerCliService: triggerCliService);
        var textExpansionTools = McpToolTestFactory.CreateTextExpansionTools(
            scheduleCliService: scheduleCliService,
            shortcutCliService: shortcutCliService,
            triggerCliService: triggerCliService);
        var taskTools = McpToolTestFactory.CreateTaskTools(
            scheduleCliService: scheduleCliService,
            shortcutCliService: shortcutCliService,
            triggerCliService: triggerCliService);
        var automationTools = McpToolTestFactory.CreateAutomationTools(
            operationCoordinator: operationCoordinator,
            scheduleCliService: scheduleCliService,
            shortcutCliService: shortcutCliService,
            triggerCliService: triggerCliService);
        var macroTools = McpToolTestFactory.CreateMacroTools(
            scheduleCliService: scheduleCliService,
            shortcutCliService: shortcutCliService,
            triggerCliService: triggerCliService);
        var clipboardTools = McpToolTestFactory.CreateClipboardTools(
            clipboardCliService: new TestClipboardCliService
            {
                GetResult = CliCommandExecutionResult.Ok("Clipboard text read.", new ClipboardTextData("protocol text")),
                SetResult = CliCommandExecutionResult.Ok("Clipboard text set.", new ClipboardSetData(13, "text")),
            },
            imageAssetCodec: new TestImageAssetCodec
            {
                PngBytes = McpTestData.CreatePngBytes(),
                Frame = McpTestData.CreateImageFrame(),
            },
            imageClipboardReader: new TestImageClipboardReader(),
            scheduleCliService: scheduleCliService,
            shortcutCliService: shortcutCliService,
            triggerCliService: triggerCliService);
        var windowTools = McpToolTestFactory.CreateWindowTools(
            windowCliService: new TestWindowCliService
            {
                Result = CliCommandExecutionResult.Ok("Windows listed.", new WindowListData([], Count: 0)),
            },
            scheduleCliService: scheduleCliService,
            shortcutCliService: shortcutCliService,
            triggerCliService: triggerCliService);
        _ = services.AddSingleton(runtimeTools);
        _ = services.AddSingleton(settingsTools);
        _ = services.AddSingleton(profileTools);
        _ = services.AddSingleton(textExpansionTools);
        _ = services.AddSingleton(taskTools);
        _ = services.AddSingleton(automationTools);
        _ = services.AddSingleton(commandTools);
        _ = services.AddSingleton(macroTools);
        _ = services.AddSingleton(clipboardTools);
        _ = services.AddSingleton(windowTools);
        _ = services.AddSingleton(screenTools);
        _ = services
            .AddMcpServer(options => options.ProtocolVersion = "2026-07-28")
            .WithStreamServerTransport(
                clientToServer.Reader.AsStream(),
                serverToClient.Writer.AsStream())
            .WithCrossMacroToolsForTests(
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
                screenTools);

        await using var provider = services.BuildServiceProvider(validateScopes: true);
        var serverTask = provider.GetRequiredService<McpServer>().RunAsync(cancellation.Token);
        await using var client = await McpClient.CreateAsync(
            new StreamClientTransport(
                clientToServer.Writer.AsStream(),
                serverToClient.Reader.AsStream()),
            cancellationToken: cancellation.Token);

        var discoveredTools = await client.ListToolsAsync(cancellationToken: cancellation.Token);
        var discoveredNames = discoveredTools
            .Select(static tool => tool.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var discoveredMetadata = discoveredTools
            .OrderBy(static tool => tool.Name, StringComparer.Ordinal)
            .Select(static tool =>
                $"{tool.Name}|{tool.Title}|{tool.ProtocolTool.Annotations?.ReadOnlyHint?.ToString() ?? "null"}|{tool.ProtocolTool.Annotations?.DestructiveHint?.ToString() ?? "null"}|{tool.ProtocolTool.Annotations?.IdempotentHint?.ToString() ?? "null"}")
            .ToArray();

        Assert.Equal(
        [
            "automation.get|Get automation status|True|False|True",
            "automation.start|Start automation|False|False|False",
            "automation.stop|Stop automation|False|False|True",
            "clipboard.get_image|Read image clipboard|True|False|True",
            "clipboard.get_text|Read text clipboard|True|False|True",
             "clipboard.set_image|Set image clipboard|False|False|True",
             "clipboard.set_text|Set text clipboard|False|False|True",
             "command.execute|Execute a CrossMacro command|False|True|False",
             "cursor.position|Read cursor position|True|False|True",
             "daemon.status|Get Linux daemon status|True|False|True",
             "help.get|Get CrossMacro MCP help|True|False|True",
            "image.read|Read a PNG image|True|False|True",
            "macro.inspect|Inspect a macro|True|False|True",
            "macro.list|List macro files|True|False|True",
             "macro.validate|Validate a macro|True|False|True",
             "profile.create|Create a profile|False|True|False",
             "profile.current|Get current profile|True|False|True",
             "profile.delete|Delete a profile|False|True|True",
             "profile.list|List profiles|True|False|True",
              "profile.rename|Rename a profile|False|True|True",
              "profile.switch|Switch profile|False|True|True",
              "schedule.add|Add a schedule|False|True|False",
             "schedule.disable|Disable a schedule|False|True|True",
             "schedule.edit|Edit a schedule|False|True|True",
             "schedule.enable|Enable a schedule|False|True|True",
             "schedule.list|List schedules|True|False|True",
              "schedule.next|Get next schedule run|True|False|True",
              "schedule.remove|Remove a schedule|False|True|True",
              "schedule.run|Run a schedule|False|True|False",
               "screen.find_image|Find an image on screen|True|False|True",
             "screen.read|Read screen data|True|False|True",
             "screenshot.capture|Capture a screenshot|False|False|False",
             "settings.get|Get settings|True|False|True",
             "settings.list_keys|List setting keys|True|False|True",
             "settings.reset|Reset a setting|False|True|True",
              "settings.set|Set a setting|False|True|True",
               "setup.run|Run temporary setup|False|True|True",
             "setup.status|Get setup status|True|False|True",
               "shortcut.add|Add a shortcut|False|True|False",
              "shortcut.bind|Bind a shortcut|False|True|True",
              "shortcut.disable|Disable a shortcut|False|True|True",
              "shortcut.edit|Edit a shortcut|False|True|True",
              "shortcut.enable|Enable a shortcut|False|True|True",
              "shortcut.list|List shortcuts|True|False|True",
              "shortcut.remove|Remove a shortcut|False|True|True",
              "shortcut.run|Run a shortcut|False|True|False",
               "status.get|Get CrossMacro status|True|False|True",
             "text_expansion.add|Add a text expansion|False|True|False",
             "text_expansion.disable|Disable a text expansion|False|True|True",
             "text_expansion.enable|Enable a text expansion|False|True|True",
             "text_expansion.list|List text expansions|True|False|True",
             "text_expansion.remove|Remove a text expansion|False|True|True",
             "text_expansion.test|Test a text expansion|True|False|True",
             "trigger.add|Add a trigger|False|True|False",
             "trigger.disable|Disable a trigger|False|True|True",
             "trigger.edit|Edit a trigger|False|True|True",
             "trigger.enable|Enable a trigger|False|True|True",
             "trigger.list|List triggers|True|False|True",
             "trigger.remove|Remove a trigger|False|True|True",
             "window.control|Control desktop windows|False|True|False",
              "window.query|Query desktop windows|True|False|True",
        ],
        discoveredMetadata);
        Assert.All(discoveredTools, static tool =>
        {
            Assert.Equal(JsonValueKind.Object, tool.ProtocolTool.InputSchema.ValueKind);
            _ = Assert.NotNull(tool.ProtocolTool.OutputSchema);
        });
        var status = await client.CallToolAsync("status.get", cancellationToken: cancellation.Token);
        var help = await client.CallToolAsync("help.get", cancellationToken: cancellation.Token);
        var invalidMacro = await client.CallToolAsync(
            "macro.inspect",
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["macroPath"] = "relative.macro" },
            cancellationToken: cancellation.Token);
        var invalidDirectory = await client.CallToolAsync(
            "macro.list",
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["directoryPath"] = "relative" },
            cancellationToken: cancellation.Token);
        var clipboardText = await client.CallToolAsync(
            "clipboard.get_text",
            cancellationToken: cancellation.Token);
        var clipboardSet = await client.CallToolAsync(
            "clipboard.set_text",
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["text"] = "protocol text" },
            cancellationToken: cancellation.Token);
         var clipboardImage = await client.CallToolAsync(
             "clipboard.get_image",
             cancellationToken: cancellation.Token);
         var invalidClipboardImageWrite = await client.CallToolAsync(
             "clipboard.set_image",
             new Dictionary<string, object?>(StringComparer.Ordinal) { ["imagePath"] = "relative.png" },
             cancellationToken: cancellation.Token);
        var windows = await client.CallToolAsync(
            "window.query",
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["mode"] = "list" },
            cancellationToken: cancellation.Token);
        var pixel = await client.CallToolAsync(
            "screen.read",
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["mode"] = "pixel",
                ["x"] = 0,
                ["y"] = 0,
            },
            cancellationToken: cancellation.Token);
        var invalidImage = await client.CallToolAsync(
            "screen.find_image",
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["imagePath"] = "relative.png" },
            cancellationToken: cancellation.Token);
        var screenshot = await client.CallToolAsync(
            "screenshot.capture",
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["includeImage"] = true },
            cancellationToken: cancellation.Token);
        var invalidImageRead = await client.CallToolAsync(
            "image.read",
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["imagePath"] = "relative.png" },
            cancellationToken: cancellation.Token);
        var invalidAutomation = await client.CallToolAsync(
            "automation.start",
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["kind"] = "shell" },
            cancellationToken: cancellation.Token);
        var invalidAutomationGet = await client.CallToolAsync(
            "automation.get",
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["operationId"] = "bad" },
            cancellationToken: cancellation.Token);
        var invalidAutomationStop = await client.CallToolAsync(
            "automation.stop",
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["operationId"] = "bad" },
            cancellationToken: cancellation.Token);
        var invalidCommand = await client.CallToolAsync(
            "command.execute",
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["command"] = "mcp" },
            cancellationToken: cancellation.Token);

         Assert.Equal(61, discoveredNames.Length);
        Assert.Contains("command.execute", discoveredNames, StringComparer.Ordinal);
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

        await cancellation.CancelAsync();
        await serverTask;
    }
}
