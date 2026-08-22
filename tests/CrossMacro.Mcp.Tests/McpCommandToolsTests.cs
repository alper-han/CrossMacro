namespace CrossMacro.Mcp.Tests;

public sealed class McpCommandToolsTests
{
    [Fact]
    public async Task ExecuteCommandAsync_SettingsGet_ShouldUseTheReadCapabilityAndCliHandlerPath()
    {
        var handler = new TestCliCommandHandler<SettingsGetCliOptions>(
            CliCommandExecutionResult.Ok("Settings loaded."));
        var resolver = new TestCliCommandHandlerResolver(handler);
        var tools = McpToolTestFactory.CreateCommandTools(
            cliCommandExecutor: McpToolTestFactory.CreateCliCommandExecutor(resolver));

        var result = await tools.ExecuteCommandAsync(
            "settings",
            ["get", "mcp.commandExecute", "--json"],
            CancellationToken.None);

        var structured = Assert.IsType<JsonElement>(result.StructuredContent);
        Assert.Equal("settings", structured.GetProperty("command").GetString());
        Assert.False(result.IsError);
        Assert.Equal(1, resolver.ResolveCallCount);
        var options = Assert.IsType<SettingsGetCliOptions>(handler.LastOptions);
        Assert.Equal("mcp.commandExecute", options.Key);
        Assert.True(options.JsonOutput);
    }

    [Theory]
    [InlineData("set", "mcp.inputAutomation", "true")]
    [InlineData("reset", "mcp.inputAutomation", null)]
    public async Task ExecuteCommandAsync_ShouldRejectMcpSecurityPolicyMutation(
        string action,
        string key,
        string? value)
    {
        var resolver = new TestCliCommandHandlerResolver();
        var tools = McpToolTestFactory.CreateCommandTools(cliCommandExecutor: McpToolTestFactory.CreateCliCommandExecutor(resolver));
        string[] arguments = value is null ? [action, key] : [action, key, value];

        var result = await tools.ExecuteCommandAsync("settings", arguments, CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal(0, resolver.ResolveCallCount);
        var outcome = Assert.IsType<JsonElement>(result.StructuredContent).GetProperty("outcome");
        Assert.Equal("capability_denied", outcome.GetProperty("errors")[0].GetProperty("code").GetString());
    }

    [Fact]
    public async Task ExecuteCommandAsync_ScheduleRun_ShouldAuthorizeTheStoredMacroPathBeforeDispatch()
    {
        var allowedRoot = McpTestData.CreateTemporaryDirectory();
        var outsideRoot = McpTestData.CreateTemporaryDirectory();
        var taskId = Guid.NewGuid();
        var outsideMacro = Path.Combine(outsideRoot, "outside.macro");
        File.WriteAllText(outsideMacro, "macro");
        try
        {
            var settings = new AppSettings();
            settings.McpSecurity.Paths = settings.McpSecurity.Paths.WithRoots(McpPathSetting.MacroRead, [allowedRoot]);
            var schedule = new TestScheduleCliService
            {
                ListResult = CliCommandExecutionResult.Ok(
                    "Loaded 1 schedule task(s).",
                    new TaskListData<ScheduleTaskData>(
                        1,
                        [new ScheduleTaskData(taskId, "Daily", true, "Interval", outsideMacro, 1, 1, "Minutes", null, null, null, null, null, null)])),
            };
            var resolver = new TestCliCommandHandlerResolver();
            var tools = McpToolTestFactory.CreateCommandTools(
                scheduleCliService: schedule,
                cliCommandExecutor: McpToolTestFactory.CreateCliCommandExecutor(resolver),
                pathPolicy: new McpPathPolicy(new TestSettingsService(settings)));

            var result = await tools.ExecuteCommandAsync("schedule", ["run", taskId.ToString()], CancellationToken.None);

            Assert.True(result.IsError);
            Assert.Equal(0, resolver.ResolveCallCount);
            var outcome = Assert.IsType<JsonElement>(result.StructuredContent).GetProperty("outcome");
            Assert.Equal("path_not_allowed", outcome.GetProperty("errors")[0].GetProperty("code").GetString());
        }
        finally
        {
            Directory.Delete(allowedRoot, recursive: true);
            Directory.Delete(outsideRoot, recursive: true);
        }
    }

    [Fact]
    public async Task ExecuteCommandAsync_TriggerEnable_ShouldRequireMacroReadForAStoredRunMacro()
    {
        var taskId = Guid.NewGuid();
        var settings = new AppSettings();
        settings.McpSecurity.AllowMacroRead = false;
        var trigger = new TestTriggerCliService
        {
            ListResult = CliCommandExecutionResult.Ok(
                "Loaded 1 trigger task(s).",
                new TaskListData<TriggerTaskData>(
                    1,
                    [new TriggerTaskData(taskId, "Focus", false, "WindowTitle", "Equals", "Editor", "RunMacro", null, "/tmp/focus.macro", "OnceOnChange", null, null, null, null)])),
        };
        var resolver = new TestCliCommandHandlerResolver();
        var tools = McpToolTestFactory.CreateCommandTools(
            cliCommandExecutor: McpToolTestFactory.CreateCliCommandExecutor(resolver),
            capabilityPolicy: new McpCapabilityPolicy(new TestSettingsService(settings)),
            triggerCliService: trigger);

        var result = await tools.ExecuteCommandAsync("trigger", ["enable", taskId.ToString()], CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal(0, resolver.ResolveCallCount);
        var outcome = Assert.IsType<JsonElement>(result.StructuredContent).GetProperty("outcome");
        Assert.Equal("capability_denied", outcome.GetProperty("errors")[0].GetProperty("code").GetString());
    }

    [Fact]
    public async Task ExecuteCommandAsync_TriggerEnable_ShouldAuthorizeTheStoredRunMacroPathBeforeDispatch()
    {
        var allowedRoot = McpTestData.CreateTemporaryDirectory();
        var outsideRoot = McpTestData.CreateTemporaryDirectory();
        var taskId = Guid.NewGuid();
        var outsideMacro = Path.Combine(outsideRoot, "outside.macro");
        await File.WriteAllTextAsync(outsideMacro, "macro");
        try
        {
            var settings = new AppSettings();
            settings.McpSecurity.Paths = settings.McpSecurity.Paths.WithRoots(McpPathSetting.MacroRead, [allowedRoot]);
            var trigger = new TestTriggerCliService
            {
                ListResult = CliCommandExecutionResult.Ok(
                    "Loaded 1 trigger task(s).",
                    new TaskListData<TriggerTaskData>(
                        1,
                        [new TriggerTaskData(taskId, "Focus", false, "WindowTitle", "Equals", "Editor", "RunMacro", null, outsideMacro, "OnceOnChange", null, null, null, null)])),
            };
            var resolver = new TestCliCommandHandlerResolver();
            var tools = McpToolTestFactory.CreateCommandTools(
                cliCommandExecutor: McpToolTestFactory.CreateCliCommandExecutor(resolver),
                pathPolicy: new McpPathPolicy(new TestSettingsService(settings)),
                triggerCliService: trigger);

            var result = await tools.ExecuteCommandAsync("trigger", ["enable", taskId.ToString()], CancellationToken.None);

            Assert.True(result.IsError);
            Assert.Equal(0, resolver.ResolveCallCount);
            var outcome = Assert.IsType<JsonElement>(result.StructuredContent).GetProperty("outcome");
            Assert.Equal("path_not_allowed", outcome.GetProperty("errors")[0].GetProperty("code").GetString());
        }
        finally
        {
            Directory.Delete(allowedRoot, recursive: true);
            Directory.Delete(outsideRoot, recursive: true);
        }
    }

    [Fact]
    public async Task ExecuteCommandAsync_ShouldRejectLifecycleAndPrivilegeCommandsBeforeParsing()
    {
        var resolver = new TestCliCommandHandlerResolver();
        var tools = McpToolTestFactory.CreateCommandTools(cliCommandExecutor: McpToolTestFactory.CreateCliCommandExecutor(resolver));

        foreach (var command in new[] { "mcp", "headless", "setup", "quick-setup", "gui", "sudo", "pkexec", "run0" })
        {
            var result = await tools.ExecuteCommandAsync(command, cancellationToken: CancellationToken.None);

            Assert.Equal(true, result.IsError);
            Assert.Equal(0, resolver.ResolveCallCount);
            var structured = Assert.IsType<JsonElement>(result.StructuredContent);
            Assert.Equal(command, structured.GetProperty("command").GetString());
            Assert.False(structured.GetProperty("operationStarted").GetBoolean());
            Assert.False(structured.TryGetProperty("operationId", out var operationId) && operationId.ValueKind is not JsonValueKind.Null);
        }
    }

    [Fact]
    public async Task ExecuteCommandAsync_ShouldDispatchFiniteCommandsAndRedactHandlerDetails()
    {
        var handler = new TestCliCommandHandler<DoctorCliOptions>(
            CliCommandExecutionResult.Fail(
                CliExitCode.EnvironmentError,
                "Doctor checks found blocking issues.",
                errors: ["secret backend path /home/user/private"]));
        var resolver = new TestCliCommandHandlerResolver(handler);
        var tools = McpToolTestFactory.CreateCommandTools(cliCommandExecutor: McpToolTestFactory.CreateCliCommandExecutor(resolver));

        var result = await tools.ExecuteCommandAsync(
            "doctor",
            ["--verbose"],
            CancellationToken.None);

        Assert.Equal(true, result.IsError);
        Assert.Equal(1, resolver.ResolveCallCount);
        var options = Assert.IsType<DoctorCliOptions>(handler.LastOptions);
        Assert.True(options.Verbose);
        var structured = Assert.IsType<JsonElement>(result.StructuredContent);
        Assert.Equal("doctor", structured.GetProperty("command").GetString());
        Assert.False(structured.GetProperty("operationStarted").GetBoolean());
        Assert.Equal(JsonValueKind.Null, structured.GetProperty("operationId").ValueKind);
        var outcome = structured.GetProperty("outcome");
        Assert.Equal("environment_error", outcome.GetProperty("errors")[0].GetProperty("code").GetString());
        Assert.DoesNotContain("secret backend path", outcome.GetRawText(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteCommandAsync_WithJsonOption_ShouldKeepCliJsonSemanticsInsideStructuredMcpContent()
    {
        var handler = new TestCliCommandHandler<DoctorCliOptions>(
            CliCommandExecutionResult.Ok("Doctor completed."));
        var tools = McpToolTestFactory.CreateCommandTools(cliCommandExecutor: McpToolTestFactory.CreateCliCommandExecutor(new TestCliCommandHandlerResolver(handler)));

        var result = await tools.ExecuteCommandAsync(
            "doctor",
            ["--json", "--verbose"],
            CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        Assert.True(Assert.IsType<DoctorCliOptions>(handler.LastOptions).JsonOutput);
        var structured = Assert.IsType<JsonElement>(result.StructuredContent);
        Assert.Equal("doctor", structured.GetProperty("command").GetString());
        Assert.Equal("Doctor completed.", structured.GetProperty("outcome").GetProperty("message").GetString());
    }

    [Fact]
    public async Task ExecuteCommandAsync_WhenHandlerThrows_ShouldReturnStableRuntimeError()
    {
        var handler = new ThrowingCliCommandHandler("secret backend detail");
        var tools = McpToolTestFactory.CreateCommandTools(cliCommandExecutor: McpToolTestFactory.CreateCliCommandExecutor(new TestCliCommandHandlerResolver(handler)));

        var result = await tools.ExecuteCommandAsync("doctor", cancellationToken: CancellationToken.None);

        Assert.True(result.IsError);
        var outcome = Assert.IsType<JsonElement>(result.StructuredContent).GetProperty("outcome");
        Assert.Equal("runtime_error", outcome.GetProperty("errors")[0].GetProperty("code").GetString());
        Assert.DoesNotContain("secret backend detail", outcome.GetRawText(), StringComparison.Ordinal);
    }


    public static IEnumerable<object[]> RepresentativeCompatibilityInvocations()
    {
        yield return ["macro", new[] { "validate", "demo.macro", "--json" }];
        yield return ["play", new[] { "demo.macro", "--dry-run", "--json" }];
        yield return ["doctor", new[] { "--verbose", "--json" }];
        yield return ["record", new[] { "--output", "recorded.macro", "--duration", "0", "--json" }];
        yield return ["run", new[] { "--step", "delay 1ms", "--dry-run", "--json" }];
        yield return ["move", new[] { "abs", "1", "2", "--dry-run", "--json" }];
        yield return ["click", new[] { "left", "--dry-run", "--json" }];
        yield return ["down", new[] { "left", "--dry-run", "--json" }];
        yield return ["up", new[] { "left", "--dry-run", "--json" }];
        yield return ["scroll", new[] { "up", "1", "--dry-run", "--json" }];
        yield return ["key", new[] { "down", "A", "--dry-run", "--json" }];
        yield return ["tap", new[] { "CTRL+A", "--dry-run", "--json" }];
        yield return ["type", new[] { "hello", "--dry-run", "--json" }];
        yield return ["delay", new[] { "1ms", "--dry-run", "--json" }];
        yield return ["clipboard", new[] { "get", "--json" }];
        yield return ["window", new[] { "active", "--json" }];
        yield return ["screen", new[] { "pixel", "1", "2", "--json" }];
        yield return ["screenshot", new[] { "--clipboard", "--json" }];
    }

    [Theory]
    [MemberData(nameof(RepresentativeCompatibilityInvocations))]
    public async Task ExecuteCommandAsync_ShouldPreserveCliInvocationSemantics(string command, IReadOnlyList<string> arguments)
    {
        var invocationArguments = arguments.ToArray(); // Preserve the exact CLI token order while adapting secure test paths.
        var temporaryMacroPath = McpTestData.CreateTemporaryMacroFile();
        try
        {
            if (command is "macro" or "play")
            {
                invocationArguments[command is "macro" ? 1 : 0] = temporaryMacroPath;
            }
            else if (command is "record")
            {
                invocationArguments[1] = Path.Combine(Path.GetDirectoryName(temporaryMacroPath)!, $"recorded-{Guid.NewGuid():N}.macro");
            }

            var cliParse = CliCommandRouter.Parse(invocationArguments.Prepend(command).ToArray());
            Assert.True(cliParse.IsSuccess, $"{command}: {cliParse.ErrorMessage}");
            var cliOptions = Assert.IsAssignableFrom<CliCommandOptions>(cliParse.Options);

            using var coordinator = new McpOperationCoordinator();
            var handler = new RecordingCliCommandHandler();
            var result = await McpToolTestFactory.CreateCommandTools(
                    operationCoordinator: coordinator,
                    macroExecutionService: new TestMacroExecutionService
                    {
                        ExecutionResult = new MacroExecutionResult { Success = true, ExitCode = CliExitCode.Success, Message = "Play completed." },
                    },
                    runScriptExecutionService: new TestRunScriptExecutionService
                    {
                        Result = new MacroExecutionResult { Success = true, ExitCode = CliExitCode.Success, Message = "Run completed." },
                    },
                    recordExecutionService: new TestRecordExecutionService
                    {
                        Result = new RecordExecutionResult { Success = true, ExitCode = CliExitCode.Success, Message = "Record completed." },
                    },
                    cliCommandExecutor: McpToolTestFactory.CreateCliCommandExecutor(new TestCliCommandHandlerResolver(handler)))
                .ExecuteCommandAsync(command, invocationArguments, CancellationToken.None);

            var structured = Assert.IsType<JsonElement>(result.StructuredContent);
            Assert.Equal(command, structured.GetProperty("command").GetString());
            var outcome = structured.GetProperty("outcome");
            Assert.DoesNotContain(
                "invalid_arguments",
                outcome.GetProperty("errors").EnumerateArray().Select(error => error.GetProperty("code").GetString()),
                StringComparer.Ordinal);

            if (command is not ("play" or "run" or "record"))
            {
                var lastOptions = Assert.IsAssignableFrom<CliCommandOptions>(handler.LastOptions);
                Assert.Equal(cliOptions.GetType(), lastOptions.GetType());
                Assert.True(lastOptions.JsonOutput);
            }
        }
        finally
        {
            File.Delete(temporaryMacroPath);
        }
    }

    [Fact]
    public async Task ExecuteCommandAsync_ShouldRequireTheCapabilityOfTheParsedCommand()
    {
        var settings = new AppSettings();
        settings.McpSecurity.AllowCommandExecute = true;
        settings.McpSecurity.AllowInputAutomation = true;
        settings.McpSecurity.AllowClipboardRead = false;
        var policy = new McpCapabilityPolicy(new TestSettingsService(settings));
        var resolver = new TestCliCommandHandlerResolver();
        var tools = McpToolTestFactory.CreateCommandTools(
            cliCommandExecutor: McpToolTestFactory.CreateCliCommandExecutor(resolver),
            capabilityPolicy: policy);

        var result = await tools.ExecuteCommandAsync(
            "clipboard",
            ["get"],
            CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal(0, resolver.ResolveCallCount);
        var outcome = Assert.IsType<JsonElement>(result.StructuredContent).GetProperty("outcome");
        Assert.Equal("capability_denied", outcome.GetProperty("errors")[0].GetProperty("code").GetString());
    }

    [Fact]
    public async Task ExecuteCommandAsync_ShouldAuthorizeTaskMacroPathsBeforeDispatch()
    {
        var allowedRoot = McpTestData.CreateTemporaryDirectory();
        var outsideRoot = McpTestData.CreateTemporaryDirectory();
        var outsideMacro = Path.Combine(outsideRoot, "outside.macro");
        File.WriteAllText(outsideMacro, "macro");
        try
        {
            var settings = new AppSettings();
            settings.McpSecurity.Paths = settings.McpSecurity.Paths.WithRoots(McpPathSetting.MacroRead, [allowedRoot]);
            var resolver = new TestCliCommandHandlerResolver();
            var tools = McpToolTestFactory.CreateCommandTools(
                cliCommandExecutor: McpToolTestFactory.CreateCliCommandExecutor(resolver),
                pathPolicy: new McpPathPolicy(new TestSettingsService(settings)));

            var results = new[]
            {
                await tools.ExecuteCommandAsync(
                    "schedule",
                    ["add", "--name", "Task", "--macro", outsideMacro],
                    CancellationToken.None),
                await tools.ExecuteCommandAsync(
                    "shortcut",
                    ["add", "--name", "Task", "--macro", outsideMacro, "--hotkey", "Ctrl+Alt+T"],
                    CancellationToken.None),
                await tools.ExecuteCommandAsync(
                    "trigger",
                    ["add", "--name", "Task", "--field", "WindowTitle", "--match-mode", "Equals", "--value", "Editor", "--action", "RunMacro", "--macro", outsideMacro],
                    CancellationToken.None),
            };

            Assert.All(results, result =>
            {
                Assert.True(result.IsError);
                var structured = Assert.IsType<JsonElement>(result.StructuredContent);
                Assert.Equal("path_not_allowed", structured.GetProperty("outcome").GetProperty("errors")[0].GetProperty("code").GetString());
            });
            Assert.Equal(0, resolver.ResolveCallCount);
        }
        finally
        {
            Directory.Delete(allowedRoot, recursive: true);
            Directory.Delete(outsideRoot, recursive: true);
        }
    }

    [Fact]
    public async Task ExecuteCommandAsync_ShouldReturnAnOperationIdForRunCommands()
    {
        using var coordinator = new McpOperationCoordinator();
        var run = new TestRunScriptExecutionService
        {
            Result = new MacroExecutionResult
            {
                Success = true,
                ExitCode = CliExitCode.Success,
                Message = "Run command complete.",
            },
        };
        var tools = McpToolTestFactory.CreateCommandTools(
            operationCoordinator: coordinator,
            runScriptExecutionService: run);
        var automationTools = McpToolTestFactory.CreateAutomationTools(operationCoordinator: coordinator);

        var result = await tools.ExecuteCommandAsync(
            "run",
            ["--step", "delay 1s"],
            CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var structured = Assert.IsType<JsonElement>(result.StructuredContent);
        Assert.Equal("run", structured.GetProperty("command").GetString());
        Assert.True(structured.GetProperty("operationStarted").GetBoolean());
        var operationId = Assert.IsType<string>(structured.GetProperty("operationId").GetString());
        Assert.False(string.IsNullOrWhiteSpace(operationId));
        var completed = await McpTestData.WaitForAutomationCompletionAsync(automationTools, operationId);
        Assert.Equal("succeeded", completed.GetProperty("operation").GetProperty("state").GetString());
        Assert.Equal(["delay 1s"], run.LastRequest!.Steps);
    }

    [Fact]
    public async Task ExecuteCommandAsync_ShouldRequireShellCapabilityForInlineShellRun()
    {
        var settings = new AppSettings();
        settings.McpSecurity.AllowCommandExecute = true;
        settings.McpSecurity.AllowShellExecute = false;
        var policy = new McpCapabilityPolicy(new TestSettingsService(settings));
        var tools = McpToolTestFactory.CreateCommandTools(capabilityPolicy: policy);

        var result = await tools.ExecuteCommandAsync(
            "run",
            ["--step", "shell \"printf hello\""],
            CancellationToken.None);

        Assert.True(result.IsError);
        var outcome = Assert.IsType<JsonElement>(result.StructuredContent).GetProperty("outcome");
        Assert.Equal("capability_denied", outcome.GetProperty("errors")[0].GetProperty("code").GetString());
    }
}
