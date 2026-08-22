namespace CrossMacro.Mcp.Tests;

public sealed class McpAutomationToolsTests
{
    [Fact]
    public async Task StartAutomationAsync_Play_ShouldUsePreflightAndRetainOnlyRedactedOperationData()
    {
        var macroPath = McpTestData.CreateTemporaryMacroFile();
        try
        {
            using var coordinator = new McpOperationCoordinator();
            var preflight = new TestCliPreflightService();
            var execution = new TestMacroExecutionService
            {
                ExecutionResult = new MacroExecutionResult
                {
                    Success = true,
                    ExitCode = CliExitCode.Success,
                    Message = "Playback complete.",
                    Data = new { MacroPath = macroPath },
                },
            };
            var tools = McpToolTestFactory.CreateAutomationTools(
                macroExecutionService: execution,
                operationCoordinator: coordinator,
                cliPreflightService: preflight);

            var started = await tools.StartAutomationAsync(
                kind: "play",
                macroPath: macroPath,
                speedMultiplier: 2,
                repeatCount: 2,
                repeatDelayMs: 50,
                countdownSeconds: 1,
                timeoutSeconds: 2,
                cancellationToken: CancellationToken.None);

            Assert.NotEqual(true, started.IsError);
            var startStructured = Assert.IsType<JsonElement>(started.StructuredContent);
            Assert.Contains(
                "Operation ID:",
                Assert.IsType<TextContentBlock>(Assert.Single(started.Content)).Text,
                StringComparison.Ordinal);
            var operationId = Assert.IsType<string>(startStructured.GetProperty("operation").GetProperty("operationId").GetString());
            Assert.DoesNotContain(macroPath, startStructured.GetRawText(), StringComparison.Ordinal);
            Assert.Equal([CliPreflightTarget.Play], preflight.Targets);
            var completed = await McpTestData.WaitForAutomationCompletionAsync(tools, operationId);
            Assert.Equal("succeeded", completed.GetProperty("operation").GetProperty("state").GetString());
            Assert.DoesNotContain(macroPath, completed.GetRawText(), StringComparison.Ordinal);
            Assert.Equal(2d, execution.LastExecutionRequest!.SpeedMultiplier);
            Assert.True(execution.LastExecutionRequest.Loop);
            Assert.Equal(2, execution.LastExecutionRequest.RepeatCount);
            Assert.Equal(50, execution.LastExecutionRequest.RepeatDelayMs);
        }
        finally
        {
            File.Delete(macroPath);
        }
    }

    [Fact]
    public async Task StartAutomationAsync_RunAndRecord_ShouldDispatchBoundedRequests()
    {
        var outputPath = Path.Combine(Path.GetTempPath(), $"crossmacro-mcp-{Guid.NewGuid():N}.macro");
        try
        {
            using var coordinator = new McpOperationCoordinator();
            var run = new TestRunScriptExecutionService
            {
                Result = new MacroExecutionResult
                {
                    Success = true,
                    ExitCode = CliExitCode.Success,
                    Message = "Run script execution complete.",
                },
            };
            var record = new TestRecordExecutionService
            {
                Result = new RecordExecutionResult
                {
                    Success = true,
                    ExitCode = CliExitCode.Success,
                    Message = "Recording completed.",
                },
            };
            var preflight = new TestCliPreflightService();
            var tools = McpToolTestFactory.CreateAutomationTools(
                operationCoordinator: coordinator,
                runScriptExecutionService: run,
                recordExecutionService: record,
                cliPreflightService: preflight);

            var runStart = await tools.StartAutomationAsync(
                kind: "run",
                steps: ["move abs 10 20", "click left"],
                speedMultiplier: 1.5,
                dryRun: true,
                cancellationToken: CancellationToken.None);
            var runId = Assert.IsType<string>(Assert.IsType<JsonElement>(runStart.StructuredContent).GetProperty("operation").GetProperty("operationId").GetString());
            Assert.NotEqual(true, runStart.IsError);
            _ = await McpTestData.WaitForAutomationCompletionAsync(tools, runId);
            Assert.Equal(["move abs 10 20", "click left"], run.LastRequest!.Steps);
            Assert.Equal(1.5d, run.LastRequest.SpeedMultiplier);
            Assert.True(run.LastRequest.DryRun);
            Assert.Empty(preflight.Targets);

            var recordStart = await tools.StartAutomationAsync(
                kind: "record",
                outputPath: outputPath,
                recordMouse: true,
                recordKeyboard: false,
                coordinateMode: "relative",
                skipInitialZero: true,
                durationSeconds: 1,
                cancellationToken: CancellationToken.None);
            var recordId = Assert.IsType<string>(Assert.IsType<JsonElement>(recordStart.StructuredContent).GetProperty("operation").GetProperty("operationId").GetString());
            Assert.NotEqual(true, recordStart.IsError);
            _ = await McpTestData.WaitForAutomationCompletionAsync(tools, recordId);
            Assert.Equal([CliPreflightTarget.Record], preflight.Targets);
            Assert.Equal(Path.GetFullPath(outputPath), record.LastRequest!.OutputFilePath);
            Assert.True(record.LastRequest.RecordMouse);
            Assert.False(record.LastRequest.RecordKeyboard);
            Assert.Equal(RecordCoordinateMode.Relative, record.LastRequest.CoordinateMode);
            Assert.True(record.LastRequest.SkipInitialZero);
            Assert.Equal(1, record.LastRequest.DurationSeconds);
        }
        finally
        {
            File.Delete(outputPath);
        }
    }

    [Fact]
    public async Task StartAutomationAsync_Play_ShouldForwardMotionParityOptions()
    {
        var macroPath = McpTestData.CreateTemporaryMacroFile();
        try
        {
            using var coordinator = new McpOperationCoordinator();
            var execution = new TestMacroExecutionService
            {
                ExecutionResult = new MacroExecutionResult { Success = true, ExitCode = CliExitCode.Success, Message = "Playback complete." },
            };
            var tools = McpToolTestFactory.CreateAutomationTools(macroExecutionService: execution, operationCoordinator: coordinator);

            var started = await tools.StartAutomationAsync(
                "play",
                macroPath: macroPath,
                motionMode: "strict-speed",
                strictSpeedMotionEventsPerSecond: 1200,
                precisionMotionEventsPerSecond: 400,
                maximumMotionErrorPixels: 3,
                cancellationToken: CancellationToken.None);

            var operationId = Assert.IsType<string>(Assert.IsType<JsonElement>(started.StructuredContent).GetProperty("operation").GetProperty("operationId").GetString());
            _ = await McpTestData.WaitForAutomationCompletionAsync(tools, operationId);
            Assert.Equal(MotionPlaybackMode.StrictSpeed, execution.LastExecutionRequest!.MotionMode);
            Assert.Equal(1200, execution.LastExecutionRequest.StrictSpeedMotionEventsPerSecond);
            Assert.Equal(400, execution.LastExecutionRequest.PrecisionMotionEventsPerSecond);
            Assert.Equal(3, execution.LastExecutionRequest.MaximumMotionErrorPixels);
        }
        finally
        {
            File.Delete(macroPath);
        }
    }

    [Fact]
    public async Task StartAutomationAsync_Run_ShouldForwardStepFileAndImageAssets()
    {
        var stepFile = Path.Combine(Path.GetTempPath(), $"crossmacro-mcp-{Guid.NewGuid():N}.steps");
        var imagePath = McpTestData.CreateTemporaryPngFile();
        File.WriteAllText(stepFile, "click left");
        try
        {
            using var coordinator = new McpOperationCoordinator();
            var run = new TestRunScriptExecutionService
            {
                Result = new MacroExecutionResult { Success = true, ExitCode = CliExitCode.Success, Message = "Run complete." },
            };
            var tools = McpToolTestFactory.CreateAutomationTools(runScriptExecutionService: run, operationCoordinator: coordinator);

            var started = await tools.StartAutomationAsync(
                "run",
                stepFilePath: stepFile,
                imageAssets: [new McpRunImageAsset("target", imagePath)],
                dryRun: true,
                cancellationToken: CancellationToken.None);

            var operationId = Assert.IsType<string>(Assert.IsType<JsonElement>(started.StructuredContent).GetProperty("operation").GetProperty("operationId").GetString());
            _ = await McpTestData.WaitForAutomationCompletionAsync(tools, operationId);
            Assert.Equal(Path.GetFullPath(stepFile), run.LastRequest!.StepFilePath);
            Assert.Equal("target", Assert.Single(run.LastRequest.ImageAssets).Name);
            Assert.Equal(Path.GetFullPath(imagePath), run.LastRequest.ImageAssets[0].FilePath);
        }
        finally
        {
            File.Delete(stepFile);
            File.Delete(imagePath);
        }
    }

    [Fact]
    public async Task AutomationGetAndStop_ShouldValidateIdsAndCancelAnActiveOperation()
    {
        using var coordinator = new McpOperationCoordinator();
        var execution = new WaitingMacroExecutionService();
        var tools = McpToolTestFactory.CreateAutomationTools(macroExecutionService: execution, operationCoordinator: coordinator);
        var macroPath = McpTestData.CreateTemporaryMacroFile();
        try
        {
            var started = await tools.StartAutomationAsync("play", macroPath: macroPath, cancellationToken: CancellationToken.None);
            var operationId = Assert.IsType<string>(Assert.IsType<JsonElement>(started.StructuredContent).GetProperty("operation").GetProperty("operationId").GetString());
            await execution.Started.Task;

            var invalidGet = tools.GetAutomation("bad");
            var stopped = tools.StopAutomation(operationId);
            var repeatedStop = tools.StopAutomation(operationId);

            Assert.Equal(true, invalidGet.IsError);
            Assert.NotEqual(true, stopped.IsError);
            Assert.True(Assert.IsType<JsonElement>(stopped.StructuredContent).GetProperty("cancellationInitiated").GetBoolean());
            Assert.False(Assert.IsType<JsonElement>(repeatedStop.StructuredContent).GetProperty("cancellationInitiated").GetBoolean());
            await execution.Cancelled.Task;
            var completed = await McpTestData.WaitForAutomationCompletionAsync(tools, operationId);
            Assert.Equal("cancelled", completed.GetProperty("operation").GetProperty("state").GetString());
        }
        finally
        {
            File.Delete(macroPath);
        }
    }

    [Fact]
    public async Task StartAutomationAsync_ShouldRejectASecondActiveOperation()
    {
        using var coordinator = new McpOperationCoordinator();
        var execution = new WaitingMacroExecutionService();
        var tools = McpToolTestFactory.CreateAutomationTools(macroExecutionService: execution, operationCoordinator: coordinator);
        var macroPath = McpTestData.CreateTemporaryMacroFile();
        try
        {
            var first = await tools.StartAutomationAsync("play", macroPath: macroPath, cancellationToken: CancellationToken.None);
            var operationId = Assert.IsType<string>(Assert.IsType<JsonElement>(first.StructuredContent).GetProperty("operation").GetProperty("operationId").GetString());
            await execution.Started.Task;

            var second = await tools.StartAutomationAsync(
                "run",
                steps: ["click left"],
                dryRun: true,
                cancellationToken: CancellationToken.None);

            Assert.Equal(true, second.IsError);
            var structured = Assert.IsType<JsonElement>(second.StructuredContent);
            Assert.Equal("runtime_error", structured.GetProperty("outcome").GetProperty("errors")[0].GetProperty("code").GetString());
            Assert.Equal(JsonValueKind.Null, structured.GetProperty("operation").ValueKind);
            _ = tools.StopAutomation(operationId);
            await execution.Cancelled.Task;
        }
        finally
        {
            File.Delete(macroPath);
        }
    }

    [Fact]
    public async Task StartAutomationAsync_WhenTheDeadlineExpires_ShouldCompleteWithRuntimeError()
    {
        var macroPath = McpTestData.CreateTemporaryMacroFile();
        try
        {
            using var coordinator = new McpOperationCoordinator();
            var execution = new TestMacroExecutionService
            {
                ExecutionHandler = async (_, cancellationToken) =>
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, TimeProvider.System, cancellationToken).ConfigureAwait(false);
                    throw new InvalidOperationException("The timeout should cancel this operation.");
                },
            };
            var tools = McpToolTestFactory.CreateAutomationTools(macroExecutionService: execution, operationCoordinator: coordinator);

            var started = await tools.StartAutomationAsync(
                "play",
                macroPath: macroPath,
                timeoutSeconds: 1,
                cancellationToken: CancellationToken.None);
            var operationId = Assert.IsType<string>(Assert.IsType<JsonElement>(started.StructuredContent).GetProperty("operation").GetProperty("operationId").GetString());

            var completed = await McpTestData.WaitForAutomationCompletionAsync(tools, operationId, maximumAttempts: 200);

            Assert.Equal("failed", completed.GetProperty("operation").GetProperty("state").GetString());
            Assert.Equal("runtime_error", completed.GetProperty("operation").GetProperty("outcome").GetProperty("errors")[0].GetProperty("code").GetString());
            Assert.Equal("Automation operation timed out.", completed.GetProperty("operation").GetProperty("outcome").GetProperty("message").GetString());
        }
        finally
        {
            File.Delete(macroPath);
        }
    }

    [Fact]
    public async Task StartAutomationAsync_ShouldApplyFiniteMcpDefaultsAndRejectAnExplicitZeroTimeout()
    {
        var macroPath = McpTestData.CreateTemporaryMacroFile();
        try
        {
            using var coordinator = new McpOperationCoordinator();
            var execution = new TestMacroExecutionService
            {
                ExecutionResult = new MacroExecutionResult
                {
                    Success = true,
                    ExitCode = CliExitCode.Success,
                    Message = "Playback complete.",
                },
            };
            var tools = McpToolTestFactory.CreateAutomationTools(macroExecutionService: execution, operationCoordinator: coordinator);

            var defaultTimeout = await tools.StartAutomationAsync("play", macroPath: macroPath, cancellationToken: CancellationToken.None);
            var defaultId = Assert.IsType<string>(Assert.IsType<JsonElement>(defaultTimeout.StructuredContent).GetProperty("operation").GetProperty("operationId").GetString());
            _ = await McpTestData.WaitForAutomationCompletionAsync(tools, defaultId);
            var zeroTimeout = await tools.StartAutomationAsync("play", macroPath: macroPath, timeoutSeconds: 0, cancellationToken: CancellationToken.None);

            Assert.NotNull(execution.LastExecutionRequest);
            Assert.True(zeroTimeout.IsError);
            Assert.Equal("invalid_arguments", Assert.IsType<JsonElement>(zeroTimeout.StructuredContent).GetProperty("outcome").GetProperty("errors")[0].GetProperty("code").GetString());
        }
        finally
        {
            File.Delete(macroPath);
        }
    }

    [Fact]
    public async Task StartAutomationAsync_ShouldRejectInvalidInputAndRedactPreflightFailure()
    {
        var macroPath = McpTestData.CreateTemporaryMacroFile();
        try
        {
            var preflight = new TestCliPreflightService
            {
                Result = CliPreflightResult.Fail(
                    CliExitCode.EnvironmentError,
                    "Preflight check failed: input simulation backend is unavailable.",
                    ["native permission detail should not leak"]),
            };
            var tools = McpToolTestFactory.CreateAutomationTools(cliPreflightService: preflight);

            var unknown = await tools.StartAutomationAsync("shell", cancellationToken: CancellationToken.None);
            var invalidRun = await tools.StartAutomationAsync("run", steps: [""], cancellationToken: CancellationToken.None);
            var preflightFailure = await tools.StartAutomationAsync("play", macroPath: macroPath, cancellationToken: CancellationToken.None);

            Assert.Equal(true, unknown.IsError);
            Assert.Equal(true, invalidRun.IsError);
            Assert.Equal(true, preflightFailure.IsError);
            var structured = Assert.IsType<JsonElement>(preflightFailure.StructuredContent);
            Assert.Equal("environment_error", structured.GetProperty("outcome").GetProperty("errors")[0].GetProperty("code").GetString());
            Assert.DoesNotContain("native permission detail should not leak", structured.GetRawText(), StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(macroPath);
        }
    }

    [Fact]
    public async Task StartAutomationAsync_ShouldRequireShellCapabilityForShellSteps()
    {
        var settings = new AppSettings();
        settings.McpSecurity.AllowCommandExecute = true;
        settings.McpSecurity.AllowInputAutomation = true;
        settings.McpSecurity.AllowShellExecute = false;
        var policy = new McpCapabilityPolicy(new TestSettingsService(settings));
        var tools = McpToolTestFactory.CreateAutomationTools(capabilityPolicy: policy);

        var result = await tools.StartAutomationAsync(
            "run",
            steps: ["shell \"printf hello\""],
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsError);
        var outcome = Assert.IsType<JsonElement>(result.StructuredContent).GetProperty("outcome");
        Assert.Equal("capability_denied", outcome.GetProperty("errors")[0].GetProperty("code").GetString());
    }

    [Fact]
    public async Task StartAutomationAsync_ShouldAllowNonShellRunStepsWithoutShellCapability()
    {
        var settings = new AppSettings();
        settings.McpSecurity.AllowCommandExecute = true;
        settings.McpSecurity.AllowShellExecute = false;
        settings.McpSecurity.AllowInputAutomation = true;
        var policy = new McpCapabilityPolicy(new TestSettingsService(settings));
        var run = new TestRunScriptExecutionService
        {
            Result = new MacroExecutionResult
            {
                Success = true,
                ExitCode = CliExitCode.Success,
                Message = "Run command complete.",
            },
        };
        using var coordinator = new McpOperationCoordinator();
        var tools = McpToolTestFactory.CreateAutomationTools(capabilityPolicy: policy, runScriptExecutionService: run, operationCoordinator: coordinator);

        var result = await tools.StartAutomationAsync(
            "run",
            steps: ["delay 1s"],
            cancellationToken: CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
    }
}
