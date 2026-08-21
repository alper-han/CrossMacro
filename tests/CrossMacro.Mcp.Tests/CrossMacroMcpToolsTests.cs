namespace CrossMacro.Mcp.Tests;

public sealed class CrossMacroMcpToolsTests
{
    [Fact]
    public async Task StartAutomationAsync_Play_ShouldUsePreflightAndRetainOnlyRedactedOperationData()
    {
        var macroPath = CreateTemporaryMacroFile();
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
            var tools = CreateTools(
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
            var completed = await WaitForAutomationCompletionAsync(tools, operationId);
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
            var tools = CreateTools(
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
            _ = await WaitForAutomationCompletionAsync(tools, runId);
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
            _ = await WaitForAutomationCompletionAsync(tools, recordId);
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
        var macroPath = CreateTemporaryMacroFile();
        try
        {
            using var coordinator = new McpOperationCoordinator();
            var execution = new TestMacroExecutionService
            {
                ExecutionResult = new MacroExecutionResult { Success = true, ExitCode = CliExitCode.Success, Message = "Playback complete." },
            };
            var tools = CreateTools(macroExecutionService: execution, operationCoordinator: coordinator);

            var started = await tools.StartAutomationAsync(
                "play",
                macroPath: macroPath,
                motionMode: "strict-speed",
                strictSpeedMotionEventsPerSecond: 1200,
                precisionMotionEventsPerSecond: 400,
                maximumMotionErrorPixels: 3,
                cancellationToken: CancellationToken.None);

            var operationId = Assert.IsType<string>(Assert.IsType<JsonElement>(started.StructuredContent).GetProperty("operation").GetProperty("operationId").GetString());
            _ = await WaitForAutomationCompletionAsync(tools, operationId);
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
        var imagePath = CreateTemporaryPngFile();
        File.WriteAllText(stepFile, "click left");
        try
        {
            using var coordinator = new McpOperationCoordinator();
            var run = new TestRunScriptExecutionService
            {
                Result = new MacroExecutionResult { Success = true, ExitCode = CliExitCode.Success, Message = "Run complete." },
            };
            var tools = CreateTools(runScriptExecutionService: run, operationCoordinator: coordinator);

            var started = await tools.StartAutomationAsync(
                "run",
                stepFilePath: stepFile,
                imageAssets: [new McpRunImageAsset("target", imagePath)],
                dryRun: true,
                cancellationToken: CancellationToken.None);

            var operationId = Assert.IsType<string>(Assert.IsType<JsonElement>(started.StructuredContent).GetProperty("operation").GetProperty("operationId").GetString());
            _ = await WaitForAutomationCompletionAsync(tools, operationId);
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
        var tools = CreateTools(macroExecutionService: execution, operationCoordinator: coordinator);
        var macroPath = CreateTemporaryMacroFile();
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
            var completed = await WaitForAutomationCompletionAsync(tools, operationId);
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
        var tools = CreateTools(macroExecutionService: execution, operationCoordinator: coordinator);
        var macroPath = CreateTemporaryMacroFile();
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
        var macroPath = CreateTemporaryMacroFile();
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
            var tools = CreateTools(macroExecutionService: execution, operationCoordinator: coordinator);

            var started = await tools.StartAutomationAsync(
                "play",
                macroPath: macroPath,
                timeoutSeconds: 1,
                cancellationToken: CancellationToken.None);
            var operationId = Assert.IsType<string>(Assert.IsType<JsonElement>(started.StructuredContent).GetProperty("operation").GetProperty("operationId").GetString());

            var completed = await WaitForAutomationCompletionAsync(tools, operationId, maximumAttempts: 200);

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
        var macroPath = CreateTemporaryMacroFile();
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
            var tools = CreateTools(macroExecutionService: execution, operationCoordinator: coordinator);

            var defaultTimeout = await tools.StartAutomationAsync("play", macroPath: macroPath, cancellationToken: CancellationToken.None);
            var defaultId = Assert.IsType<string>(Assert.IsType<JsonElement>(defaultTimeout.StructuredContent).GetProperty("operation").GetProperty("operationId").GetString());
            _ = await WaitForAutomationCompletionAsync(tools, defaultId);
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
        var macroPath = CreateTemporaryMacroFile();
        try
        {
            var preflight = new TestCliPreflightService
            {
                Result = CliPreflightResult.Fail(
                    CliExitCode.EnvironmentError,
                    "Preflight check failed: input simulation backend is unavailable.",
                    ["native permission detail should not leak"]),
            };
            var tools = CreateTools(cliPreflightService: preflight);

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
    public async Task GetClipboardImageAsync_ShouldReturnImageOnlyWhenExplicitlyRequested()
    {
        var pngBytes = CreatePngBytes();
        var clipboardReader = new TestImageClipboardReader { PngBytes = pngBytes };
        var tools = CreateTools(
            imageAssetCodec: new TestImageAssetCodec { Frame = CreateImageFrame() },
            imageClipboardReader: clipboardReader);

        var metadataOnly = await tools.GetClipboardImageAsync(includeImage: false, cancellationToken: CancellationToken.None);
        var inlineImage = await tools.GetClipboardImageAsync(includeImage: true, cancellationToken: CancellationToken.None);

        Assert.NotEqual(true, metadataOnly.IsError);
        _ = Assert.Single(metadataOnly.Content);
        Assert.NotEqual(true, inlineImage.IsError);
        var image = Assert.IsType<ImageContentBlock>(inlineImage.Content[1]);
        Assert.Equal("image/png", image.MimeType);
        Assert.Equal(pngBytes, image.DecodedData.ToArray());
        var structured = Assert.IsType<JsonElement>(inlineImage.StructuredContent);
        Assert.True(structured.GetProperty("imageAvailable").GetBoolean());
        Assert.Equal(2, structured.GetProperty("width").GetInt32());
        Assert.Equal(1, structured.GetProperty("height").GetInt32());
        Assert.True(structured.GetProperty("imageIncluded").GetBoolean());
        Assert.Equal(48 * 1024 * 1024, clipboardReader.LastMaximumBytes);
    }

    [Fact]
    public async Task GetClipboardImageAsync_ShouldDistinguishEmptyClipboardAndUnsupportedReadCapability()
    {
        var noImageTools = CreateTools(imageClipboardReader: new TestImageClipboardReader());

        var noImage = await noImageTools.GetClipboardImageAsync(cancellationToken: CancellationToken.None);

        Assert.NotEqual(true, noImage.IsError);
        var noImageStructured = Assert.IsType<JsonElement>(noImage.StructuredContent);
        Assert.False(noImageStructured.GetProperty("imageAvailable").GetBoolean());
        Assert.False(noImageStructured.GetProperty("imageIncluded").GetBoolean());

        var unsupportedReader = new TestImageClipboardReader { IsSupported = false };
        var unsupportedTools = CreateTools(imageClipboardReader: unsupportedReader);

        var unsupported = await unsupportedTools.GetClipboardImageAsync(cancellationToken: CancellationToken.None);

        Assert.Equal(true, unsupported.IsError);
        Assert.Equal(0, unsupportedReader.CallCount);
        var unsupportedStructured = Assert.IsType<JsonElement>(unsupported.StructuredContent);
        Assert.Equal("environment_error", unsupportedStructured.GetProperty("outcome").GetProperty("errors")[0].GetProperty("code").GetString());
    }

    [Fact]
    public async Task GetClipboardImageAsync_ShouldMapValidationAndInlineBoundsWithoutImageContent()
    {
        var invalidTools = CreateTools(imageClipboardReader: new TestImageClipboardReader
        {
            Exception = new InvalidDataException("clipboard bytes are invalid"),
        });

        var invalid = await invalidTools.GetClipboardImageAsync(cancellationToken: CancellationToken.None);

        Assert.Equal(true, invalid.IsError);
        var invalidStructured = Assert.IsType<JsonElement>(invalid.StructuredContent);
        Assert.Equal("validation_error", invalidStructured.GetProperty("outcome").GetProperty("errors")[0].GetProperty("code").GetString());

        var oversizedTools = CreateTools(
            imageAssetCodec: new TestImageAssetCodec { Frame = CreateImageFrame() },
            imageClipboardReader: new TestImageClipboardReader { PngBytes = new byte[(8 * 1024 * 1024) + 1] });

        var oversized = await oversizedTools.GetClipboardImageAsync(includeImage: true, cancellationToken: CancellationToken.None);

        Assert.Equal(true, oversized.IsError);
        _ = Assert.Single(oversized.Content);
        var oversizedStructured = Assert.IsType<JsonElement>(oversized.StructuredContent);
        Assert.True(oversizedStructured.GetProperty("imageAvailable").GetBoolean());
        Assert.False(oversizedStructured.GetProperty("imageIncluded").GetBoolean());
        Assert.Equal("runtime_error", oversizedStructured.GetProperty("outcome").GetProperty("errors")[0].GetProperty("code").GetString());
    }

    [Fact]
    public async Task ReadImageAsync_ShouldReturnImageOnlyWhenExplicitlyRequested()
    {
        var imagePath = CreateTemporaryPngFile();
        try
        {
            var pngBytes = CreatePngBytes();
            var imageAssetCodec = new TestImageAssetCodec
            {
                PngBytes = pngBytes,
                Frame = CreateImageFrame(),
            };
            var tools = CreateTools(imageAssetCodec: imageAssetCodec);

            var metadataOnly = await tools.ReadImageAsync(imagePath, includeImage: false, cancellationToken: CancellationToken.None);
            var inlineImage = await tools.ReadImageAsync(imagePath, includeImage: true, cancellationToken: CancellationToken.None);

            Assert.NotEqual(true, metadataOnly.IsError);
            _ = Assert.Single(metadataOnly.Content);
            Assert.NotEqual(true, inlineImage.IsError);
            var image = Assert.IsType<ImageContentBlock>(inlineImage.Content[1]);
            Assert.Equal("image/png", image.MimeType);
            Assert.Equal(pngBytes, image.DecodedData.ToArray());
            var structured = Assert.IsType<JsonElement>(inlineImage.StructuredContent);
            Assert.Equal(2, structured.GetProperty("width").GetInt32());
            Assert.Equal(1, structured.GetProperty("height").GetInt32());
            Assert.True(structured.GetProperty("imageIncluded").GetBoolean());
            Assert.DoesNotContain(imagePath, structured.GetRawText(), StringComparison.Ordinal);
            Assert.Equal(2, imageAssetCodec.ReadCallCount);
        }
        finally
        {
            File.Delete(imagePath);
        }
    }

    [Fact]
    public async Task ReadImageAsync_ShouldPreserveValidationAndFileErrorCategoriesWithoutLeakingPaths()
    {
        var imagePath = CreateTemporaryPngFile();
        try
        {
            var validationCodec = new TestImageAssetCodec { Failure = TestImageAssetFailure.Validation };
            var validationTools = CreateTools(imageAssetCodec: validationCodec);

            var validation = await validationTools.ReadImageAsync(imagePath, cancellationToken: CancellationToken.None);

            Assert.Equal(true, validation.IsError);
            var validationStructured = Assert.IsType<JsonElement>(validation.StructuredContent);
            Assert.Equal("validation_error", validationStructured.GetProperty("outcome").GetProperty("errors")[0].GetProperty("code").GetString());
            Assert.DoesNotContain(imagePath, validationStructured.GetRawText(), StringComparison.Ordinal);

            var fileCodec = new TestImageAssetCodec { Failure = TestImageAssetFailure.File };
            var fileTools = CreateTools(imageAssetCodec: fileCodec);

            var file = await fileTools.ReadImageAsync(imagePath, cancellationToken: CancellationToken.None);

            Assert.Equal(true, file.IsError);
            var fileStructured = Assert.IsType<JsonElement>(file.StructuredContent);
            Assert.Equal("file_error", fileStructured.GetProperty("outcome").GetProperty("errors")[0].GetProperty("code").GetString());
        }
        finally
        {
            File.Delete(imagePath);
        }
    }

    [Fact]
    public async Task ReadImageAsync_ShouldRejectInvalidPathsBeforeCallingTheCodec()
    {
        var imageAssetCodec = new TestImageAssetCodec();
        var tools = CreateTools(imageAssetCodec: imageAssetCodec);

        var result = await tools.ReadImageAsync("relative.png", cancellationToken: CancellationToken.None);

        Assert.Equal(true, result.IsError);
        Assert.Equal(0, imageAssetCodec.ReadCallCount);
    }

    [Fact]
    public async Task CaptureScreenshotAsync_ShouldReturnImageOnlyWhenExplicitlyRequested()
    {
        var pngBytes = CreatePngBytes();
        var screenshotService = new TestScreenshotCaptureService
        {
            Result = ScreenshotPngCaptureResult.Ok(new ScreenshotPngCaptureData(
                pngBytes,
                OutputPath: null,
                Width: 2,
                Height: 1,
                Provider: "test",
                IsRegion: false,
                CopiedToClipboard: false)),
        };
        var tools = CreateTools(screenshotCaptureService: screenshotService);

        var metadataOnly = await tools.CaptureScreenshotAsync(includeImage: false, copyToClipboard: true, cancellationToken: CancellationToken.None);
        var inlineImage = await tools.CaptureScreenshotAsync(includeImage: true, cancellationToken: CancellationToken.None);

        Assert.NotEqual(true, metadataOnly.IsError);
        _ = Assert.Single(metadataOnly.Content);
        Assert.NotEqual(true, inlineImage.IsError);
        var image = Assert.IsType<ImageContentBlock>(inlineImage.Content[1]);
        Assert.Equal("image/png", image.MimeType);
        Assert.Equal(pngBytes, image.DecodedData.ToArray());
        var structured = Assert.IsType<JsonElement>(inlineImage.StructuredContent);
        Assert.True(structured.GetProperty("imageIncluded").GetBoolean());
        Assert.Equal(pngBytes.Length, structured.GetProperty("pngByteCount").GetInt32());
        Assert.Equal(8 * 1024 * 1024, structured.GetProperty("maximumInlineImageBytes").GetInt32());
        Assert.Equal(2, screenshotService.CallCount);
        Assert.True(screenshotService.LastRequest?.MaximumEncodedBytes <= 8 * 1024 * 1024);
    }

    [Fact]
    public async Task CaptureScreenshotAsync_ShouldValidateDestinationsAndRegionBeforeCapture()
    {
        var screenshotService = new TestScreenshotCaptureService();
        var tools = CreateTools(screenshotCaptureService: screenshotService);

        var missingDestination = await tools.CaptureScreenshotAsync(cancellationToken: CancellationToken.None);
        var relativeOutput = await tools.CaptureScreenshotAsync(outputPath: "shot.png", cancellationToken: CancellationToken.None);
        var oversizedRegion = await tools.CaptureScreenshotAsync(
            includeImage: true,
            regionX: 0,
            regionY: 0,
            regionWidth: 8_000,
            regionHeight: 8_000,
            cancellationToken: CancellationToken.None);

        Assert.Equal(true, missingDestination.IsError);
        Assert.Equal(true, relativeOutput.IsError);
        Assert.Equal(true, oversizedRegion.IsError);
        Assert.Equal(0, screenshotService.CallCount);
    }

    [Fact]
    public async Task CaptureScreenshotAsync_ShouldForwardFileClipboardAndRegionWithoutAddingImageContent()
    {
        var outputPath = Path.Combine(Path.GetTempPath(), $"crossmacro-mcp-{Guid.NewGuid():N}.png");
        var screenshotService = new TestScreenshotCaptureService
        {
            Result = ScreenshotPngCaptureResult.Ok(new ScreenshotPngCaptureData(
                CreatePngBytes(),
                OutputPath: outputPath,
                Width: 2,
                Height: 1,
                Provider: "test",
                IsRegion: true,
                CopiedToClipboard: true)),
        };
        var tools = CreateTools(screenshotCaptureService: screenshotService);

        var result = await tools.CaptureScreenshotAsync(
            outputPath: outputPath,
            copyToClipboard: true,
            regionX: 4,
            regionY: 5,
            regionWidth: 2,
            regionHeight: 1,
            cancellationToken: CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        _ = Assert.Single(result.Content);
        var request = Assert.IsType<ScreenshotPngCaptureRequest>(screenshotService.LastRequest);
        Assert.Equal(outputPath, request.OutputPath);
        Assert.True(request.CopyToClipboard);
        Assert.Equal(new ScreenRect(4, 5, 2, 1), request.Region);
        Assert.Equal(ScreenshotPngCaptureRequest.DefaultMaximumEncodedBytes, request.MaximumEncodedBytes);
        var structured = Assert.IsType<JsonElement>(result.StructuredContent);
        Assert.False(structured.GetProperty("imageIncluded").GetBoolean());
        Assert.Equal(outputPath, structured.GetProperty("outputPath").GetString());
        Assert.True(structured.GetProperty("copiedToClipboard").GetBoolean());
    }

    [Fact]
    public async Task CaptureScreenshotAsync_ShouldRejectAnOversizedInlineResultWithoutAddingImageContent()
    {
        var screenshotService = new TestScreenshotCaptureService
        {
            Result = ScreenshotPngCaptureResult.Ok(new ScreenshotPngCaptureData(
                new byte[(8 * 1024 * 1024) + 1],
                OutputPath: null,
                Width: 1,
                Height: 1,
                Provider: "test",
                IsRegion: false,
                CopiedToClipboard: false)),
        };
        var tools = CreateTools(screenshotCaptureService: screenshotService);

        var result = await tools.CaptureScreenshotAsync(includeImage: true, cancellationToken: CancellationToken.None);

        Assert.Equal(true, result.IsError);
        _ = Assert.Single(result.Content);
        var structured = Assert.IsType<JsonElement>(result.StructuredContent);
        Assert.False(structured.GetProperty("imageIncluded").GetBoolean());
        Assert.Equal("runtime_error", structured.GetProperty("outcome").GetProperty("errors")[0].GetProperty("code").GetString());
    }

    [Fact]
    public async Task CaptureScreenshotAsync_ShouldPreserveFailureCategoryAndRedactDetails()
    {
        const string secret = "screenshot backend detail should not leak";
        var screenshotService = new TestScreenshotCaptureService
        {
            Result = ScreenshotPngCaptureResult.Fail(
                ScreenshotCaptureFailureKind.ProviderUnsupported,
                "Screenshot capture is not supported in this runtime.",
                [secret]),
        };
        var tools = CreateTools(screenshotCaptureService: screenshotService);

        var result = await tools.CaptureScreenshotAsync(includeImage: true, cancellationToken: CancellationToken.None);

        Assert.Equal(true, result.IsError);
        var structured = Assert.IsType<JsonElement>(result.StructuredContent);
        Assert.Equal("environment_error", structured.GetProperty("outcome").GetProperty("errors")[0].GetProperty("code").GetString());
        Assert.DoesNotContain(secret, structured.GetRawText(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task CaptureScreenshotAsync_ShouldMapOutputWriteFailuresToFileErrors()
    {
        var screenshotService = new TestScreenshotCaptureService
        {
            Result = ScreenshotPngCaptureResult.Fail(
                ScreenshotCaptureFailureKind.FileWriteFailed,
                "Failed to write screenshot file.",
                ["directory permission denied"]),
        };
        var tools = CreateTools(screenshotCaptureService: screenshotService);
        var outputPath = Path.Combine(Path.GetTempPath(), $"crossmacro-mcp-{Guid.NewGuid():N}.png");

        var result = await tools.CaptureScreenshotAsync(outputPath: outputPath, cancellationToken: CancellationToken.None);

        Assert.Equal(true, result.IsError);
        var structured = Assert.IsType<JsonElement>(result.StructuredContent);
        Assert.Equal("file_error", structured.GetProperty("outcome").GetProperty("errors")[0].GetProperty("code").GetString());
        Assert.DoesNotContain("directory permission denied", structured.GetRawText(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReadScreenAsync_ShouldMapPixelAndRejectUnsupportedPixelInputs()
    {
        var screenService = new TestScreenCliService
        {
            Result = CliCommandExecutionResult.Ok("Pixel 3,4: 123456", new ScreenPixelData(3, 4, "123456", "test", Relative: false)),
        };
        var tools = CreateTools(screenCliService: screenService);

        var result = await tools.ReadScreenAsync(
            mode: "pixel",
            x: 3,
            y: 4,
            cancellationToken: CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        Assert.Equal(ScreenCliAction.Pixel, screenService.LastOptions?.Action);
        var structured = Assert.IsType<JsonElement>(result.StructuredContent);
        Assert.Equal("pixel", structured.GetProperty("mode").GetString());
        Assert.Equal(3, structured.GetProperty("point").GetProperty("x").GetInt32());
        Assert.Equal("123456", structured.GetProperty("color").GetString());

        var invalid = await tools.ReadScreenAsync(
            mode: "pixel",
            x: 3,
            y: 4,
            color: "123456",
            cancellationToken: CancellationToken.None);

        Assert.Equal(true, invalid.IsError);
        Assert.Equal(1, screenService.CallCount);
    }

    [Fact]
    public async Task ReadScreenAsync_ShouldMapWaitAndBoundColorSearch()
    {
        var waitService = new TestScreenCliService
        {
            Result = CliCommandExecutionResult.Ok(
                "Pixel 1,2 matched FF0000.",
                new ScreenWaitColorData(1, 2, "FF0000", "FF0000", "test", Matched: true, TimeoutMs: 30_000)),
        };
        var waitTools = CreateTools(screenCliService: waitService);

        var wait = await waitTools.ReadScreenAsync(
            mode: "wait_color",
            x: 1,
            y: 2,
            color: "ff0000",
            timeoutMs: 30_000,
            cancellationToken: CancellationToken.None);

        Assert.NotEqual(true, wait.IsError);
        Assert.Equal(ScreenCliAction.WaitColor, waitService.LastOptions?.Action);
        Assert.Equal("FF0000", waitService.LastOptions?.ExpectedColor?.ToString());
        Assert.Equal(30_000, waitService.LastOptions?.TimeoutMs);

        var searchService = new TestScreenCliService
        {
            Result = CliCommandExecutionResult.Ok(
                "Color 00FF00 found at 2,3.",
                new ScreenSearchColorData(
                    Found: true,
                    X: 2,
                    Y: 3,
                    Color: "00FF00",
                    ExpectedColor: "00FF00",
                    RegionX: 0,
                    RegionY: 0,
                    RegionWidth: 100,
                    RegionHeight: 100,
                    Tolerance: 12,
                    ProviderName: "test")),
        };
        var searchTools = CreateTools(screenCliService: searchService);

        var search = await searchTools.ReadScreenAsync(
            mode: "search_color",
            x: 0,
            y: 0,
            color: "00ff00",
            x2: 100,
            y2: 100,
            tolerance: 12,
            timeoutMs: 1_000,
            cancellationToken: CancellationToken.None);

        Assert.NotEqual(true, search.IsError);
        Assert.Equal(ScreenCliAction.SearchColor, searchService.LastOptions?.Action);
        Assert.Equal(12, searchService.LastOptions?.Tolerance);
        var structured = Assert.IsType<JsonElement>(search.StructuredContent);
        Assert.True(structured.GetProperty("found").GetBoolean());
        Assert.Equal(100, structured.GetProperty("region").GetProperty("width").GetInt32());
    }

    [Theory]
    [InlineData("wait_color", "FF0000", null, null, 30_001)]
    [InlineData("search_color", "FF0000", 0, 10, null)]
    [InlineData("search_color", "FF0000", 8_000, 8_000, null)]
    [InlineData("search_color", "invalid", 10, 10, null)]
    public async Task ReadScreenAsync_ShouldRejectInvalidOrUnboundedInputsWithoutCallingTheCliService(
        string mode,
        string color,
        int? x2,
        int? y2,
        int? timeoutMs)
    {
        var screenService = new TestScreenCliService();
        var tools = CreateTools(screenCliService: screenService);

        var result = await tools.ReadScreenAsync(
            mode: mode,
            x: 0,
            y: 0,
            color: color,
            x2: x2,
            y2: y2,
            timeoutMs: timeoutMs,
            cancellationToken: CancellationToken.None);

        Assert.Equal(true, result.IsError);
        var structured = Assert.IsType<JsonElement>(result.StructuredContent);
        Assert.Equal("invalid_arguments", structured.GetProperty("outcome").GetProperty("errors")[0].GetProperty("code").GetString());
        Assert.Equal(0, screenService.CallCount);
    }

    [Fact]
    public async Task ReadScreenAsync_ShouldRedactBackendErrorDetailsAndPropagateCancellation()
    {
        const string secret = "screen provider detail should not leak";
        var screenService = new TestScreenCliService
        {
            Result = CliCommandExecutionResult.Fail(CliExitCode.EnvironmentError, "Screen pixel reading is not supported in this runtime.", [secret]),
        };
        var tools = CreateTools(screenCliService: screenService);

        var result = await tools.ReadScreenAsync(
            mode: "pixel",
            x: 0,
            y: 0,
            cancellationToken: CancellationToken.None);

        Assert.Equal(true, result.IsError);
        var structured = Assert.IsType<JsonElement>(result.StructuredContent);
        Assert.DoesNotContain(secret, structured.GetRawText(), StringComparison.Ordinal);

        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => tools.ReadScreenAsync(
            mode: "pixel",
            x: 0,
            y: 0,
            cancellationToken: cancellation.Token));
    }

    [Fact]
    public async Task FindScreenImageAsync_ShouldMapResultWithoutEchoingTheImagePath()
    {
        var imagePath = CreateTemporaryPngFile();
        try
        {
            var screenService = new TestScreenCliService
            {
                Result = CliCommandExecutionResult.Ok(
                    "Image found at 7,8 with score 0.9.",
                    new ScreenSearchImageData(
                        Found: true,
                        X: 7,
                        Y: 8,
                        Score: 0.9,
                        ImagePath: imagePath,
                        RegionX: 0,
                        RegionY: 0,
                        RegionWidth: 10,
                        RegionHeight: 10,
                        Similarity: 0.8,
                        MatchMode: "best",
                        ProviderName: "test")),
            };
            var tools = CreateTools(screenCliService: screenService);

            var result = await tools.FindScreenImageAsync(
                imagePath: imagePath,
                regionX: 0,
                regionY: 0,
                regionWidth: 10,
                regionHeight: 10,
                similarity: 0.8,
                matchMode: "best",
                cancellationToken: CancellationToken.None);

            Assert.NotEqual(true, result.IsError);
            Assert.Equal(ScreenCliAction.SearchImage, screenService.LastOptions?.Action);
            Assert.Equal(imagePath, screenService.LastOptions?.ImagePath);
            var structured = Assert.IsType<JsonElement>(result.StructuredContent);
            Assert.True(structured.GetProperty("found").GetBoolean());
            Assert.Equal(7, structured.GetProperty("point").GetProperty("x").GetInt32());
            Assert.DoesNotContain(imagePath, structured.GetRawText(), StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(imagePath);
        }
    }

    [Theory]
    [InlineData("relative.png")]
    [InlineData("/tmp/template.jpg")]
    public async Task FindScreenImageAsync_ShouldRejectInvalidPathsWithoutCallingTheCliService(string imagePath)
    {
        var screenService = new TestScreenCliService();
        var tools = CreateTools(screenCliService: screenService);

        var result = await tools.FindScreenImageAsync(imagePath, cancellationToken: CancellationToken.None);

        Assert.Equal(true, result.IsError);
        Assert.Equal(0, screenService.CallCount);
    }

    [Fact]
    public async Task QueryWindowsAsync_ShouldReturnBoundedListAndPreserveTheTotalCount()
    {
        var windows = Enumerable.Range(0, 101)
            .Select(index => CreateWindow(index))
            .ToArray();
        var windowService = new TestWindowCliService
        {
            Result = CliCommandExecutionResult.Ok("Windows listed.", new WindowListData(windows, windows.Length)),
        };
        var tools = CreateTools(windowCliService: windowService);

        var result = await tools.QueryWindowsAsync(
            mode: "list",
            selectorKind: null,
            selectorValue: null,
            timeoutMs: null,
            cancellationToken: CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var structured = Assert.IsType<JsonElement>(result.StructuredContent);
        Assert.Equal("list", structured.GetProperty("mode").GetString());
        Assert.Equal(100, structured.GetProperty("windows").GetArrayLength());
        Assert.Equal(101, structured.GetProperty("totalCount").GetInt32());
        Assert.True(structured.GetProperty("isTruncated").GetBoolean());
        Assert.Equal(WindowCliAction.List, windowService.LastOptions?.Action);
    }

    [Fact]
    public async Task QueryWindowsAsync_ShouldMapActiveWindowThroughTheCliService()
    {
        var windowService = new TestWindowCliService
        {
            Result = CliCommandExecutionResult.Ok("Active window read.", CreateWindow(1)),
        };
        var tools = CreateTools(windowCliService: windowService);

        var result = await tools.QueryWindowsAsync(
            mode: "active",
            selectorKind: null,
            selectorValue: null,
            timeoutMs: null,
            cancellationToken: CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var structured = Assert.IsType<JsonElement>(result.StructuredContent);
        Assert.Equal("active", structured.GetProperty("mode").GetString());
        Assert.Equal("0x1", structured.GetProperty("windows")[0].GetProperty("address").GetString());
        Assert.Equal(1, structured.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task QueryWindowsAsync_ShouldUseTitleSelectorAndBoundedWaitTimeout()
    {
        var windowService = new TestWindowCliService
        {
            Result = CliCommandExecutionResult.Ok(
                "Window wait matched.",
                new WindowWaitData(
                    Found: true,
                    Window: CreateWindow(4),
                    TimeoutMs: 30_000)),
        };
        var tools = CreateTools(windowCliService: windowService);

        var result = await tools.QueryWindowsAsync(
            mode: "wait",
            selectorKind: "title",
            selectorValue: "Editor",
            timeoutMs: 30_000,
            cancellationToken: CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        Assert.Equal(WindowCliAction.Wait, windowService.LastOptions?.Action);
        Assert.Equal(WindowSelectorKind.Title, windowService.LastOptions?.Selector?.Kind);
        Assert.Equal("Editor", windowService.LastOptions?.Selector?.Value);
        Assert.Equal(30_000, windowService.LastOptions?.TimeoutMs);
        var structured = Assert.IsType<JsonElement>(result.StructuredContent);
        Assert.True(structured.GetProperty("found").GetBoolean());
        Assert.Equal(30_000, structured.GetProperty("timeoutMs").GetInt32());
    }

    [Fact]
    public async Task QueryWindowsAsync_ShouldUseClassSelectorForSearchAndDefaultWaitTimeout()
    {
        var searchService = new TestWindowCliService
        {
            Result = CliCommandExecutionResult.Ok(
                "Window search complete.",
                new WindowListData([CreateWindow(2)], Count: 1)),
        };
        var searchTools = CreateTools(windowCliService: searchService);

        var search = await searchTools.QueryWindowsAsync(
            mode: "search",
            selectorKind: "class",
            selectorValue: "TestApp",
            timeoutMs: null,
            cancellationToken: CancellationToken.None);

        Assert.NotEqual(true, search.IsError);
        Assert.Equal(WindowCliAction.Search, searchService.LastOptions?.Action);
        Assert.Equal(WindowSelectorKind.Class, searchService.LastOptions?.Selector?.Kind);
        Assert.Null(searchService.LastOptions?.TimeoutMs);

        var waitService = new TestWindowCliService
        {
            Result = CliCommandExecutionResult.Ok("Window wait timed out.", new WindowWaitData(Found: false, Window: null, TimeoutMs: 5_000)),
        };
        var waitTools = CreateTools(windowCliService: waitService);

        var wait = await waitTools.QueryWindowsAsync(
            mode: "wait",
            selectorKind: "class",
            selectorValue: "TestApp",
            timeoutMs: null,
            cancellationToken: CancellationToken.None);

        Assert.NotEqual(true, wait.IsError);
        Assert.Equal(5_000, waitService.LastOptions?.TimeoutMs);
        var structured = Assert.IsType<JsonElement>(wait.StructuredContent);
        Assert.False(structured.GetProperty("found").GetBoolean());
        Assert.Equal(5_000, structured.GetProperty("timeoutMs").GetInt32());
    }

    [Theory]
    [InlineData("unknown", null, null, null)]
    [InlineData("active", "title", "Editor", null)]
    [InlineData("search", null, null, null)]
    [InlineData("search", "address", "0x1", null)]
    [InlineData("wait", "class", "Code", 30_001)]
    public async Task QueryWindowsAsync_ShouldRejectUnsupportedInputsWithoutInvokingTheCliService(
        string mode,
        string? selectorKind,
        string? selectorValue,
        int? timeoutMs)
    {
        var windowService = new TestWindowCliService();
        var tools = CreateTools(windowCliService: windowService);

        var result = await tools.QueryWindowsAsync(
            mode: mode,
            selectorKind: selectorKind,
            selectorValue: selectorValue,
            timeoutMs: timeoutMs,
            cancellationToken: CancellationToken.None);

        Assert.Equal(true, result.IsError);
        var structured = Assert.IsType<JsonElement>(result.StructuredContent);
        Assert.Equal("invalid_arguments", structured.GetProperty("outcome").GetProperty("errors")[0].GetProperty("code").GetString());
        Assert.Equal(0, windowService.CallCount);
    }

    [Fact]
    public async Task QueryWindowsAsync_ShouldRedactUnsupportedBackendDetails()
    {
        const string secret = "window backend detail should not leak";
        var windowService = new TestWindowCliService
        {
            Result = CliCommandExecutionResult.Fail(
                CliExitCode.EnvironmentError,
                "Window management is not supported in this runtime.",
                [secret]),
        };
        var tools = CreateTools(windowCliService: windowService);

        var result = await tools.QueryWindowsAsync(
            mode: "list",
            selectorKind: null,
            selectorValue: null,
            timeoutMs: null,
            cancellationToken: CancellationToken.None);

        Assert.Equal(true, result.IsError);
        var structured = Assert.IsType<JsonElement>(result.StructuredContent);
        Assert.Equal("environment_error", structured.GetProperty("outcome").GetProperty("errors")[0].GetProperty("code").GetString());
        Assert.DoesNotContain(secret, structured.GetRawText(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task QueryWindowsAsync_ShouldPropagateCancellation()
    {
        var tools = CreateTools(windowCliService: new TestWindowCliService());
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => tools.QueryWindowsAsync(
                mode: "list",
                selectorKind: null,
                selectorValue: null,
                timeoutMs: null,
                cancellationToken: cancellation.Token));
    }

    [Fact]
    public async Task GetClipboardTextAsync_ShouldReturnBoundedTextWithoutIncludingItInTheFallbackContent()
    {
        var clipboard = new TestClipboardCliService
        {
            GetResult = CliCommandExecutionResult.Ok("Clipboard text read.", new ClipboardTextData("sensitive text")),
        };
        var tools = CreateTools(clipboardCliService: clipboard);

        var result = await tools.GetClipboardTextAsync(CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        Assert.Equal("Clipboard text read.", Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text);
        var structured = Assert.IsType<JsonElement>(result.StructuredContent);
        Assert.Equal("sensitive text", structured.GetProperty("text").GetString());
        Assert.Equal(14, structured.GetProperty("length").GetInt32());
        Assert.Equal(65_536, structured.GetProperty("maximumCharacters").GetInt32());
        Assert.Equal(1, clipboard.GetCallCount);
    }

    [Fact]
    public async Task GetClipboardTextAsync_ShouldRedactBackendErrorDetails()
    {
        const string secret = "clipboard token should not leak";
        var clipboard = new TestClipboardCliService
        {
            GetResult = CliCommandExecutionResult.Fail(
                CliExitCode.EnvironmentError,
                "Clipboard text is not supported in this runtime.",
                [secret]),
        };
        var tools = CreateTools(clipboardCliService: clipboard);

        var result = await tools.GetClipboardTextAsync(CancellationToken.None);

        Assert.Equal(true, result.IsError);
        var structured = Assert.IsType<JsonElement>(result.StructuredContent);
        Assert.Equal("environment_error", structured.GetProperty("outcome").GetProperty("errors")[0].GetProperty("code").GetString());
        Assert.Equal("Clipboard text is not supported in this runtime.", structured.GetProperty("outcome").GetProperty("errors")[0].GetProperty("message").GetString());
        Assert.Equal(JsonValueKind.Null, structured.GetProperty("text").ValueKind);
        Assert.DoesNotContain(secret, structured.GetRawText(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetClipboardTextAsync_ShouldRejectTextBeyondTheMaximumLength()
    {
        var clipboard = new TestClipboardCliService
        {
            GetResult = CliCommandExecutionResult.Ok(
                "Clipboard text read.",
                new ClipboardTextData(new string('x', 65_537))),
        };
        var tools = CreateTools(clipboardCliService: clipboard);

        var result = await tools.GetClipboardTextAsync(CancellationToken.None);

        Assert.Equal(true, result.IsError);
        var structured = Assert.IsType<JsonElement>(result.StructuredContent);
        Assert.Equal("runtime_error", structured.GetProperty("outcome").GetProperty("errors")[0].GetProperty("code").GetString());
        Assert.Equal(JsonValueKind.Null, structured.GetProperty("text").ValueKind);
    }

    [Fact]
    public async Task SetClipboardTextAsync_ShouldReturnOnlyLengthAndNotEchoTheText()
    {
        const string text = "clipboard write should not be echoed";
        var clipboard = new TestClipboardCliService
        {
            SetResult = CliCommandExecutionResult.Ok("Clipboard text set.", new ClipboardSetData(text.Length, "text")),
        };
        var tools = CreateTools(clipboardCliService: clipboard);

        var result = await tools.SetClipboardTextAsync(text, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        Assert.Equal("Clipboard text set.", Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text);
        var structured = Assert.IsType<JsonElement>(result.StructuredContent);
        Assert.Equal(text.Length, structured.GetProperty("length").GetInt32());
        Assert.Equal(65_536, structured.GetProperty("maximumCharacters").GetInt32());
        Assert.DoesNotContain(text, structured.GetRawText(), StringComparison.Ordinal);
        Assert.Equal(text, clipboard.LastSetText);
    }

    [Fact]
    public async Task SetClipboardTextAsync_ShouldRejectTextBeyondTheMaximumLengthWithoutCallingCliService()
    {
        var clipboard = new TestClipboardCliService();
        var tools = CreateTools(clipboardCliService: clipboard);

        var result = await tools.SetClipboardTextAsync(new string('x', 65_537), CancellationToken.None);

        Assert.Equal(true, result.IsError);
        var structured = Assert.IsType<JsonElement>(result.StructuredContent);
        Assert.Equal("invalid_arguments", structured.GetProperty("outcome").GetProperty("errors")[0].GetProperty("code").GetString());
        Assert.Equal(0, clipboard.SetCallCount);
    }

    [Fact]
    public async Task ClipboardTextTools_ShouldPropagateCancellation()
    {
        var clipboard = new TestClipboardCliService();
        var tools = CreateTools(clipboardCliService: clipboard);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => tools.GetClipboardTextAsync(cancellation.Token));
        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => tools.SetClipboardTextAsync("text", cancellation.Token));
    }

    [Fact]
    public void ListMacros_ShouldReturnSortedRegularMacroFilesAndIgnoreOtherEntries()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var alphaPath = Path.Combine(directory, "alpha.macro");
            var betaPath = Path.Combine(directory, "beta.macro");
            File.WriteAllText(alphaPath, "alpha");
            File.WriteAllText(betaPath, "beta");
            File.WriteAllText(Path.Combine(directory, "ignored.txt"), "ignored");

            var result = CreateTools().ListMacros(directory, CancellationToken.None);

            Assert.NotEqual(true, result.IsError);
            var structured = Assert.IsType<JsonElement>(result.StructuredContent);
            Assert.True(structured.GetProperty("outcome").GetProperty("success").GetBoolean());
            Assert.Equal(Path.GetFullPath(directory), structured.GetProperty("directoryPath").GetString());
            Assert.False(structured.GetProperty("isTruncated").GetBoolean());
            Assert.Equal(
                ["alpha.macro", "beta.macro"],
                structured.GetProperty("macros").EnumerateArray().Select(static macro => macro.GetProperty("fileName").GetString()),
                StringComparer.Ordinal);
            Assert.Equal(new FileInfo(alphaPath).Length, structured.GetProperty("macros")[0].GetProperty("sizeBytes").GetInt64());
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("relative")]
    public void ListMacros_ShouldReturnStructuredInvalidArgumentsForInvalidDirectoryPaths(string directoryPath)
    {
        var result = CreateTools().ListMacros(directoryPath, CancellationToken.None);

        Assert.Equal(true, result.IsError);
        var structured = Assert.IsType<JsonElement>(result.StructuredContent);
        Assert.Equal((int)CliExitCode.InvalidArguments, structured.GetProperty("outcome").GetProperty("exitCode").GetInt32());
        Assert.Equal("invalid_arguments", structured.GetProperty("outcome").GetProperty("errors")[0].GetProperty("code").GetString());
    }

    [Fact]
    public void ListMacros_ShouldReturnStructuredFileErrorForMissingDirectory()
    {
        var directoryPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

        var result = CreateTools().ListMacros(directoryPath, CancellationToken.None);

        Assert.Equal(true, result.IsError);
        var structured = Assert.IsType<JsonElement>(result.StructuredContent);
        Assert.Equal((int)CliExitCode.FileError, structured.GetProperty("outcome").GetProperty("exitCode").GetInt32());
        Assert.Equal("file_error", structured.GetProperty("outcome").GetProperty("errors")[0].GetProperty("code").GetString());
    }

    [Fact]
    public async Task InspectMacroAsync_ShouldReturnMacroInfoAndPreserveValidationWarnings()
    {
        var macroPath = CreateTemporaryMacroFile();
        try
        {
            var service = new TestMacroExecutionService
            {
                InfoResult = new MacroExecutionResult
                {
                    Success = true,
                    ExitCode = CliExitCode.Success,
                    Message = "Macro info loaded.",
                    Warnings = ["Position provider unavailable."],
                    Data = new MacroInfoData(
                        macroPath,
                        "Demo",
                        DateTime.UnixEpoch,
                        4,
                        300,
                        "relative",
                        IsAbsoluteCoordinates: false,
                        SkipInitialZeroZero: true,
                        TrailingDelayMicroseconds: 50,
                        TrailingDelayMs: 0,
                        HasTrailingRandomDelay: false,
                        TrailingDelayMinMs: 0,
                        TrailingDelayMaxMs: 0,
                        new MacroEventBreakdownData(1, 1, 0, 0, 1, 1)),
                },
            };
            var tools = CreateTools(service);

            var result = await tools.InspectMacroAsync(macroPath, CancellationToken.None);

            Assert.NotEqual(true, result.IsError);
            Assert.Equal("Macro info loaded.", Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text);
            var structured = Assert.IsType<JsonElement>(result.StructuredContent);
            Assert.Equal("Demo", structured.GetProperty("macro").GetProperty("macroName").GetString());
            Assert.Equal(1, structured.GetProperty("macro").GetProperty("eventBreakdown").GetProperty("mouseMove").GetInt32());
            Assert.Equal("Position provider unavailable.", structured.GetProperty("outcome").GetProperty("warnings")[0].GetString());
            Assert.Equal(1, service.GetInfoCallCount);
            Assert.Equal(macroPath, service.LastMacroPath);
        }
        finally
        {
            File.Delete(macroPath);
        }
    }

    [Fact]
    public async Task InspectMacroAsync_ShouldReturnToolErrorForInvalidMacroPathWithoutInvokingCliService()
    {
        var service = new TestMacroExecutionService();
        var tools = CreateTools(service);

        var result = await tools.InspectMacroAsync("relative.macro", CancellationToken.None);

        Assert.Equal(true, result.IsError);
        var structured = Assert.IsType<JsonElement>(result.StructuredContent);
        Assert.Equal("invalid_arguments", structured.GetProperty("outcome").GetProperty("errors")[0].GetProperty("code").GetString());
        Assert.Equal(0, service.GetInfoCallCount);
    }

    [Fact]
    public async Task ValidateMacroAsync_ShouldReturnToolErrorAndCliValidationResult()
    {
        var macroPath = CreateTemporaryMacroFile();
        try
        {
            var service = new TestMacroExecutionService
            {
                ValidationResult = new MacroExecutionResult
                {
                    Success = false,
                    ExitCode = CliExitCode.ValidationError,
                    Message = "Macro validation failed.",
                    Errors = ["Macro is empty."],
                    Warnings = ["Position provider unavailable."],
                    Data = new MacroValidationData(macroPath, 0),
                },
            };
            var tools = CreateTools(service);

            var result = await tools.ValidateMacroAsync(macroPath, CancellationToken.None);

            Assert.Equal(true, result.IsError);
            Assert.Equal("Macro validation failed.", Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text);
            var structured = Assert.IsType<JsonElement>(result.StructuredContent);
            Assert.Equal(4, structured.GetProperty("outcome").GetProperty("exitCode").GetInt32());
            Assert.Equal("validation_error", structured.GetProperty("outcome").GetProperty("errors")[0].GetProperty("code").GetString());
            Assert.Equal(JsonValueKind.Null, structured.GetProperty("macro").ValueKind);
        }
        finally
        {
            File.Delete(macroPath);
        }
    }

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
        var profileManager = CreateProfileManager();
        var tools = new CrossMacroMcpTools(
            new TestRuntimeContext(),
            doctorService,
             profileManager,
             CreateSettingsCliService(),
             new ProfileCliService(profileManager),
             new TestTextExpansionCliService(),
             new TestScheduleCliService(),
             new TestShortcutCliService(),
             new TestTriggerCliService(),
             new TestQuickSetupCliService(),
            CreateMacroExecutionService(),
            CreateClipboardCliService(),
            CreateWindowCliService(),
            CreateScreenCliService(),
            CreateScreenshotCaptureService(),
            CreateImageAssetCodec(),
            CreateImageClipboardReader(),
            CreateImageClipboardService(),
            CreateOperationCoordinator(),
            CreateRunScriptExecutionService(),
            CreateRecordExecutionService(),
            CreatePreflightService(),
             CreateCliCommandExecutor(),
             new McpCommandPolicy(),
             new AllowAllMcpCapabilityPolicy(),
             new AllowAllMcpPathPolicy());

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
        var tools = CreateTools(operationCoordinator: coordinator);

        var status = await tools.GetStatusAsync(CancellationToken.None);

        var reportedOperation = Assert.IsType<McpAutomationOperation>(status.ActiveOperation);
        Assert.Equal(activeOperation.OperationId, reportedOperation.OperationId);
        Assert.Equal(McpAutomationOperationKind.Play, reportedOperation.Kind);
        Assert.Equal(McpAutomationOperationState.Running, reportedOperation.State);
        Assert.Null(reportedOperation.Outcome);
    }

    [Fact]
    public async Task SettingsTools_ShouldAllowSettingsButRejectMcpSecurityPolicyChanges()
    {
        var settings = new AppSettings();
        settings.McpSecurity.AllowCommandExecute = true;
        var tools = CreateTools(settingsCliService: new SettingsCliService(new TestSettingsService(settings)));

        var all = await tools.GetSettingsAsync(all: true, cancellationToken: CancellationToken.None);
        var commandExecute = Assert.Single(all.Settings, static entry => entry.Key == "mcp.commandExecute");
        var restoreToken = Assert.Single(all.Settings, static entry => entry.Key == "screen.portalRestoreToken");

        Assert.True(all.Outcome.Success);
        Assert.Equal("True", commandExecute.Value);
        Assert.False(commandExecute.Redacted);
        Assert.Null(restoreToken.Value);
        Assert.True(restoreToken.Redacted);

        var set = await tools.SetSettingsAsync("mcp.commandExecute", "false", CancellationToken.None);
        Assert.False(set.Outcome.Success);
        Assert.Equal("capability_denied", Assert.Single(set.Outcome.Errors).Code);
        Assert.True(settings.McpSecurity.AllowCommandExecute);

        var keys = await tools.ListSettingsKeysAsync(CancellationToken.None);
        Assert.True(keys.Outcome.Success);
        Assert.Equal(SettingsCliService.SupportedKeys, keys.Keys);

        var reset = await tools.ResetSettingsAsync("mcp.commandExecute", CancellationToken.None);
        Assert.False(reset.Outcome.Success);
        Assert.Equal("capability_denied", Assert.Single(reset.Outcome.Errors).Code);
        Assert.True(settings.McpSecurity.AllowCommandExecute);

        settings.McpSecurity.AllowCommandExecute = false;
        var denied = await tools.ExecuteCommandAsync(
            "settings",
            ["get", "mcp.commandExecute", "--json"],
            CancellationToken.None);
        Assert.True(denied.IsError);
    }

    [Fact]
    public async Task SettingsTools_ShouldRequireTheMatchingCapability()
    {
        var capabilityPolicy = new McpCapabilityPolicy(new TestSettingsService(new AppSettings()));
        capabilityPolicy.SetRestricted(true);
        var tools = CreateTools(capabilityPolicy: capabilityPolicy);

        var result = await tools.GetSettingsAsync(cancellationToken: CancellationToken.None);

        Assert.False(result.Outcome.Success);
        Assert.Equal("capability_denied", Assert.Single(result.Outcome.Errors).Code);
    }

    [Fact]
    public async Task ExecuteCommandAsync_SettingsGet_ShouldUseTheReadCapabilityAndCliHandlerPath()
    {
        var handler = new TestCliCommandHandler<SettingsGetCliOptions>(
            CliCommandExecutionResult.Ok("Settings loaded."));
        var resolver = new TestCliCommandHandlerResolver(handler);
        var tools = CreateTools(
            cliCommandExecutor: CreateCliCommandExecutor(resolver),
            settingsCliService: new SettingsCliService(new TestSettingsService(new AppSettings())));

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
        var tools = CreateTools(cliCommandExecutor: CreateCliCommandExecutor(resolver));
        string[] arguments = value is null ? [action, key] : [action, key, value];

        var result = await tools.ExecuteCommandAsync("settings", arguments, CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal(0, resolver.ResolveCallCount);
        var outcome = Assert.IsType<JsonElement>(result.StructuredContent).GetProperty("outcome");
        Assert.Equal("capability_denied", outcome.GetProperty("errors")[0].GetProperty("code").GetString());
    }

    [Fact]
    public async Task ProfileTools_ShouldMapCliProfileResultsToStructuredProfiles()
    {
        var service = new TestProfileCliService
        {
            ListResult = CliCommandExecutionResult.Ok(
                "2 profile(s).",
                new ProfileListData(
                [
                    new ProfileData("default", "Default", DateTime.UnixEpoch, true),
                    new ProfileData("work", "Work", DateTime.UnixEpoch.AddDays(1), false),
                ],
                "default")),
        };
        var tools = CreateTools(profileCliService: service);

        var result = await tools.ListProfilesAsync(CancellationToken.None);

        Assert.True(result.Outcome.Success);
        Assert.Equal("default", result.ActiveProfileId);
        Assert.Equal(["default", "work"], result.Profiles.Select(static profile => profile.Id));
        Assert.Equal(1, service.ListCallCount);
    }

    [Fact]
    public async Task ProfileMutation_ShouldRequireProfileManageCapability()
    {
        var policy = new McpCapabilityPolicy(new TestSettingsService(new AppSettings()));
        policy.SetRestricted(true);
        var tools = CreateTools(capabilityPolicy: policy);

        var result = await tools.CreateProfileAsync("Work", CancellationToken.None);

        Assert.False(result.Outcome.Success);
        Assert.Equal("capability_denied", Assert.Single(result.Outcome.Errors).Code);
    }

    [Fact]
    public async Task TextExpansionTools_ShouldMapStructuredResultsAndForwardAddOptions()
    {
        var service = new TestTextExpansionCliService
        {
            ListResult = CliCommandExecutionResult.Ok(
                "1 text expansion(s).",
                new TextExpansionListData(
                [new TextExpansionData(":mail", "me@example.com", true, "CtrlShiftV", "Paste", "FastBatch")],
                "work",
                1)),
        };
        var tools = CreateTools(textExpansionCliService: service);

        var list = await tools.ListTextExpansionsAsync("work", CancellationToken.None);
        var add = await tools.AddTextExpansionAsync(":sig", "Regards", "CtrlShiftV", "DirectTyping", "CompatibleKeyByKey", "work", CancellationToken.None);

        Assert.True(list.Outcome.Success);
        Assert.Equal("work", list.ProfileId);
        Assert.Equal("me@example.com", Assert.Single(list.Expansions).Replacement);
        Assert.True(add.Outcome.Success);
        Assert.Equal(":sig", service.LastTrigger);
        Assert.Equal("Regards", service.LastReplacement);
        Assert.Equal(PasteMethod.CtrlShiftV, service.LastMethod);
        Assert.Equal(TextInsertionMode.DirectTyping, service.LastInsertionMode);
        Assert.Equal(DirectTypingMethod.CompatibleKeyByKey, service.LastDirectTypingMethod);
    }

    [Fact]
    public async Task TextExpansionMutation_ShouldRequireWriteCapability()
    {
        var policy = new McpCapabilityPolicy(new TestSettingsService(new AppSettings()));
        policy.SetRestricted(true);
        var tools = CreateTools(capabilityPolicy: policy);

        var result = await tools.RemoveTextExpansionAsync(":mail", cancellationToken: CancellationToken.None);

        Assert.False(result.Outcome.Success);
        Assert.Equal("capability_denied", Assert.Single(result.Outcome.Errors).Code);
    }

    [Fact]
    public void SetupStatus_ShouldExposeProviderApplicabilityWithoutElevation()
    {
        var tools = CreateTools(quickSetupCliService: new TestQuickSetupCliService
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
        var tools = CreateTools(
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
        var result = await CreateTools().GetDaemonStatusAsync(CancellationToken.None);

        if (!OperatingSystem.IsLinux())
        {
            Assert.False(result.Outcome.Success);
            Assert.Equal("unavailable", result.HandshakeStatus);
            Assert.Equal("unavailable", result.SocketAccessStatus);
            Assert.True(result.LinuxOnly);
        }
    }

    [Fact]
    public async Task TaskTools_ShouldMapScheduleShortcutAndTriggerLists()
    {
        var schedule = new TestScheduleCliService
        {
            ListResult = CliCommandExecutionResult.Ok(
                "Loaded 1 schedule task(s).",
                 new TaskListData<ScheduleTaskData>(1, [new ScheduleTaskData(Guid.NewGuid(), "Daily", true, "Interval", "/tmp/daily.macro", 1, 5, "Minutes", null, null, null, null, null, null)])),
        };
        var shortcut = new TestShortcutCliService
        {
            ListResult = CliCommandExecutionResult.Ok(
                "Loaded 1 shortcut task(s).",
                new TaskListData<ShortcutTaskData>(1, [new ShortcutTaskData(Guid.NewGuid(), "Quick", true, "Ctrl+Alt+Q", "/tmp/quick.macro", 1, false, false, 1, 0, false, null, null, [], null, null)])),
        };
        var trigger = new TestTriggerCliService
        {
            ListResult = CliCommandExecutionResult.Ok(
                "Loaded 1 trigger task(s).",
                new TaskListData<TriggerTaskData>(1, [new TriggerTaskData(Guid.NewGuid(), "Focus", true, "WindowTitle", "Equals", "Editor", "SwitchProfile", "work", null, "OnceOnChange", null, null, null, null)])),
        };
        var tools = CreateTools(scheduleCliService: schedule, shortcutCliService: shortcut, triggerCliService: trigger);

        var schedules = await tools.ListSchedulesAsync(CancellationToken.None);
        var shortcuts = await tools.ListShortcutsAsync(CancellationToken.None);
        var triggers = await tools.ListTriggersAsync(CancellationToken.None);

        Assert.Equal("Daily", Assert.Single(schedules.Tasks).Name);
        Assert.Equal("Quick", Assert.Single(shortcuts.Tasks).Name);
        Assert.Equal("Focus", Assert.Single(triggers.Tasks).Name);
    }

    [Fact]
    public async Task TaskMutation_ShouldRequireTaskManageCapability()
    {
        var policy = new McpCapabilityPolicy(new TestSettingsService(new AppSettings()));
        policy.SetRestricted(true);
        var tools = CreateTools(capabilityPolicy: policy);

        var result = await tools.AddScheduleAsync("Daily", "/tmp/daily.macro", cancellationToken: CancellationToken.None);

        Assert.False(result.Outcome.Success);
        Assert.Equal("capability_denied", Assert.Single(result.Outcome.Errors).Code);
    }

    [Theory]
    [InlineData("schedule")]
    [InlineData("shortcut")]
    [InlineData("trigger")]
    public async Task TaskMutation_ShouldRequireInputAutomationCapability(string taskType)
    {
        var settings = new AppSettings();
        settings.McpSecurity.AllowInputAutomation = false;
        var policy = new McpCapabilityPolicy(new TestSettingsService(settings));
        var schedule = new TestScheduleCliService();
        var shortcut = new TestShortcutCliService();
        var trigger = new TestTriggerCliService();
        var tools = CreateTools(
            capabilityPolicy: policy,
            scheduleCliService: schedule,
            shortcutCliService: shortcut,
            triggerCliService: trigger);

        McpToolOutcome outcome = taskType switch
        {
            "schedule" => (await tools.AddScheduleAsync("Daily", "/tmp/daily.macro", cancellationToken: CancellationToken.None)).Outcome,
            "shortcut" => (await tools.AddShortcutAsync("Quick", "/tmp/quick.macro", "Ctrl+Alt+Q", cancellationToken: CancellationToken.None)).Outcome,
            "trigger" => (await tools.AddTriggerAsync("Focus", "WindowTitle", "Editor", action: "RunMacro", macroPath: "/tmp/focus.macro", cancellationToken: CancellationToken.None)).Outcome,
            _ => throw new ArgumentOutOfRangeException(nameof(taskType), taskType, "Unknown task type."),
        };

        Assert.False(outcome.Success);
        Assert.Equal("capability_denied", Assert.Single(outcome.Errors).Code);
    }

    [Theory]
    [InlineData("schedule")]
    [InlineData("shortcut")]
    public async Task TaskRun_ShouldRequireInputAutomationCapability(string taskType)
    {
        var settings = new AppSettings();
        settings.McpSecurity.AllowInputAutomation = false;
        var policy = new McpCapabilityPolicy(new TestSettingsService(settings));
        var schedule = new TestScheduleCliService();
        var shortcut = new TestShortcutCliService();
        var tools = CreateTools(capabilityPolicy: policy, scheduleCliService: schedule, shortcutCliService: shortcut);

        McpToolOutcome outcome = taskType is "schedule"
            ? (await tools.RunScheduleAsync(Guid.NewGuid().ToString(), CancellationToken.None)).Outcome
            : (await tools.RunShortcutAsync(Guid.NewGuid().ToString(), CancellationToken.None)).Outcome;

        Assert.False(outcome.Success);
        Assert.Equal("capability_denied", Assert.Single(outcome.Errors).Code);
    }

    [Fact]
    public async Task ScheduleRun_ShouldAuthorizeTheStoredMacroPathBeforeExecuting()
    {
        var allowedRoot = CreateTemporaryDirectory();
        var outsideRoot = CreateTemporaryDirectory();
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
            var tools = CreateTools(
                scheduleCliService: schedule,
                pathPolicy: new McpPathPolicy(new TestSettingsService(settings)));

            var result = await tools.RunScheduleAsync(taskId.ToString(), CancellationToken.None);

            Assert.False(result.Outcome.Success);
            Assert.Equal("path_not_allowed", Assert.Single(result.Outcome.Errors).Code);
            Assert.Equal(0, schedule.RunCallCount);
        }
        finally
        {
            Directory.Delete(allowedRoot, recursive: true);
            Directory.Delete(outsideRoot, recursive: true);
        }
    }

    [Fact]
    public async Task ExecuteCommandAsync_ScheduleRun_ShouldAuthorizeTheStoredMacroPathBeforeDispatch()
    {
        var allowedRoot = CreateTemporaryDirectory();
        var outsideRoot = CreateTemporaryDirectory();
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
            var tools = CreateTools(
                scheduleCliService: schedule,
                cliCommandExecutor: CreateCliCommandExecutor(resolver),
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
    public async Task EnableTriggerAsync_ShouldRequireMacroReadForAStoredRunMacro()
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
        var tools = CreateTools(
            capabilityPolicy: new McpCapabilityPolicy(new TestSettingsService(settings)),
            triggerCliService: trigger);

        var result = await tools.EnableTriggerAsync(taskId.ToString(), CancellationToken.None);

        Assert.False(result.Outcome.Success);
        Assert.Equal("capability_denied", Assert.Single(result.Outcome.Errors).Code);
        Assert.Equal(0, trigger.ExecuteCallCount);
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
        var tools = CreateTools(
            cliCommandExecutor: CreateCliCommandExecutor(resolver),
            capabilityPolicy: new McpCapabilityPolicy(new TestSettingsService(settings)),
            triggerCliService: trigger);

        var result = await tools.ExecuteCommandAsync("trigger", ["enable", taskId.ToString()], CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal(0, resolver.ResolveCallCount);
        var outcome = Assert.IsType<JsonElement>(result.StructuredContent).GetProperty("outcome");
        Assert.Equal("capability_denied", outcome.GetProperty("errors")[0].GetProperty("code").GetString());
    }

    [Fact]
    public async Task EnableTriggerAsync_ShouldAuthorizeTheStoredRunMacroPathBeforeExecuting()
    {
        var allowedRoot = CreateTemporaryDirectory();
        var outsideRoot = CreateTemporaryDirectory();
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
            var tools = CreateTools(
                pathPolicy: new McpPathPolicy(new TestSettingsService(settings)),
                triggerCliService: trigger);

            var result = await tools.EnableTriggerAsync(taskId.ToString(), CancellationToken.None);

            Assert.False(result.Outcome.Success);
            Assert.Equal("path_not_allowed", Assert.Single(result.Outcome.Errors).Code);
            Assert.Equal(0, trigger.ExecuteCallCount);
        }
        finally
        {
            Directory.Delete(allowedRoot, recursive: true);
            Directory.Delete(outsideRoot, recursive: true);
        }
    }

    [Fact]
    public async Task ExecuteCommandAsync_TriggerEnable_ShouldAuthorizeTheStoredRunMacroPathBeforeDispatch()
    {
        var allowedRoot = CreateTemporaryDirectory();
        var outsideRoot = CreateTemporaryDirectory();
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
            var tools = CreateTools(
                cliCommandExecutor: CreateCliCommandExecutor(resolver),
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
    public async Task ControlWindowsAsync_ShouldMapMutationsAndSelectorsToTheExistingWindowService()
    {
        var windowService = new TestWindowCliService
        {
            Result = CliCommandExecutionResult.Ok("Window move complete.", new WindowMutationData("move", Result: true)),
        };
        var tools = CreateTools(windowCliService: windowService);

        var moved = await tools.ControlWindowsAsync(
            action: "move",
            x: 120,
            y: 240,
            cancellationToken: CancellationToken.None);

        Assert.NotEqual(true, moved.IsError);
        Assert.Equal(WindowCliAction.Move, windowService.LastOptions!.Action);
        Assert.Equal(120, windowService.LastOptions.X);
        Assert.Equal(240, windowService.LastOptions.Y);
        var movedStructured = Assert.IsType<JsonElement>(moved.StructuredContent);
        Assert.True(movedStructured.GetProperty("changed").GetBoolean());
        Assert.Equal("move", movedStructured.GetProperty("action").GetString());

        var focused = await tools.ControlWindowsAsync(
            action: "focus",
            selectorKind: "class",
            selectorValue: "Editor",
            cancellationToken: CancellationToken.None);

        Assert.NotEqual(true, focused.IsError);
        Assert.Equal(WindowCliAction.Focus, windowService.LastOptions.Action);
        Assert.Equal(WindowSelectorKind.Class, windowService.LastOptions.Selector!.Kind);
        Assert.Equal("Editor", windowService.LastOptions.Selector.Value);
    }

    [Fact]
    public async Task ControlWindowsAsync_ShouldValidateDangerousSelectorsAndGeometryBeforeCallingTheService()
    {
        var windowService = new TestWindowCliService
        {
            Result = CliCommandExecutionResult.Ok("Window control complete.", new WindowMutationData("close", Result: true)),
        };
        var tools = CreateTools(windowCliService: windowService);

        var invalidClose = await tools.ControlWindowsAsync(
            action: "close",
            selectorKind: "class",
            selectorValue: "Editor",
            cancellationToken: CancellationToken.None);
        var invalidResize = await tools.ControlWindowsAsync(
            action: "resize",
            x: 0,
            y: 100,
            cancellationToken: CancellationToken.None);
        var invalidWorkspace = await tools.ControlWindowsAsync(
            action: "workspace_move_window",
            selectorKind: "title",
            selectorValue: "Editor",
            workspaceName: "2",
            cancellationToken: CancellationToken.None);

        Assert.Equal(true, invalidClose.IsError);
        Assert.Equal(true, invalidResize.IsError);
        Assert.Equal(true, invalidWorkspace.IsError);
        Assert.Equal(0, windowService.CallCount);
    }

    [Fact]
    public async Task ExecuteCommandAsync_ShouldRejectLifecycleAndPrivilegeCommandsBeforeParsing()
    {
        var resolver = new TestCliCommandHandlerResolver();
        var tools = CreateTools(cliCommandExecutor: CreateCliCommandExecutor(resolver));

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
        var tools = CreateTools(cliCommandExecutor: CreateCliCommandExecutor(resolver));

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
        var tools = CreateTools(cliCommandExecutor: CreateCliCommandExecutor(new TestCliCommandHandlerResolver(handler)));

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
        var tools = CreateTools(cliCommandExecutor: CreateCliCommandExecutor(new TestCliCommandHandlerResolver(handler)));

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
        var temporaryMacroPath = CreateTemporaryMacroFile();
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
            var result = await CreateTools(
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
                    cliCommandExecutor: CreateCliCommandExecutor(new TestCliCommandHandlerResolver(handler)))
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
        var tools = CreateTools(
            cliCommandExecutor: CreateCliCommandExecutor(resolver),
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
        var allowedRoot = CreateTemporaryDirectory();
        var outsideRoot = CreateTemporaryDirectory();
        var outsideMacro = Path.Combine(outsideRoot, "outside.macro");
        File.WriteAllText(outsideMacro, "macro");
        try
        {
            var settings = new AppSettings();
            settings.McpSecurity.Paths = settings.McpSecurity.Paths.WithRoots(McpPathSetting.MacroRead, [allowedRoot]);
            var resolver = new TestCliCommandHandlerResolver();
            var tools = CreateTools(
                cliCommandExecutor: CreateCliCommandExecutor(resolver),
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
        var tools = CreateTools(
            operationCoordinator: coordinator,
            runScriptExecutionService: run);

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
        var completed = await WaitForAutomationCompletionAsync(tools, operationId);
        Assert.Equal("succeeded", completed.GetProperty("operation").GetProperty("state").GetString());
        Assert.Equal(["delay 1s"], run.LastRequest!.Steps);
    }

    [Fact]
    public async Task StartAutomationAsync_ShouldRequireShellCapabilityForShellSteps()
    {
        var settings = new AppSettings();
        settings.McpSecurity.AllowCommandExecute = true;
        settings.McpSecurity.AllowInputAutomation = true;
        settings.McpSecurity.AllowShellExecute = false;
        var policy = new McpCapabilityPolicy(new TestSettingsService(settings));
        var tools = CreateTools(capabilityPolicy: policy);

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
        var tools = CreateTools(capabilityPolicy: policy, runScriptExecutionService: run, operationCoordinator: coordinator);

        var result = await tools.StartAutomationAsync(
            "run",
            steps: ["delay 1s"],
            cancellationToken: CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
    }

    [Fact]
    public async Task ExecuteCommandAsync_ShouldRequireShellCapabilityForInlineShellRun()
    {
        var settings = new AppSettings();
        settings.McpSecurity.AllowCommandExecute = true;
        settings.McpSecurity.AllowShellExecute = false;
        var policy = new McpCapabilityPolicy(new TestSettingsService(settings));
        var tools = CreateTools(capabilityPolicy: policy);

        var result = await tools.ExecuteCommandAsync(
            "run",
            ["--step", "shell \"printf hello\""],
            CancellationToken.None);

        Assert.True(result.IsError);
        var outcome = Assert.IsType<JsonElement>(result.StructuredContent).GetProperty("outcome");
        Assert.Equal("capability_denied", outcome.GetProperty("errors")[0].GetProperty("code").GetString());
    }

    [Fact]
    public async Task SetClipboardImageAsync_ShouldValidateAndWritePngWithoutReturningThePath()
    {
        var path = CreateTemporaryPngFile();
        try
        {
            var clipboard = new TestImageClipboardService();
            var codec = new TestImageAssetCodec
            {
                PngBytes = CreatePngBytes(),
                Frame = CreateImageFrame(),
            };
            var tools = CreateTools(imageAssetCodec: codec, imageClipboardService: clipboard);

            var result = await tools.SetClipboardImageAsync(path, CancellationToken.None);

            Assert.NotEqual(true, result.IsError);
            Assert.Equal(1, clipboard.SetCallCount);
            Assert.Equal(CreatePngBytes(), clipboard.LastPngBytes);
            var structured = Assert.IsType<JsonElement>(result.StructuredContent);
            Assert.Equal(2, structured.GetProperty("width").GetInt32());
            Assert.Equal(1, structured.GetProperty("height").GetInt32());
            Assert.Equal(CreatePngBytes().Length, structured.GetProperty("pngByteCount").GetInt32());
            Assert.DoesNotContain(path, structured.GetRawText(), StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task SetClipboardImageAsync_ShouldRejectInvalidPathsBeforeReadingOrWriting()
    {
        var codec = new TestImageAssetCodec { PngBytes = CreatePngBytes(), Frame = CreateImageFrame() };
        var clipboard = new TestImageClipboardService();
        var tools = CreateTools(imageAssetCodec: codec, imageClipboardService: clipboard);

        var result = await tools.SetClipboardImageAsync("relative.png", CancellationToken.None);

        Assert.Equal(true, result.IsError);
        Assert.Equal(0, codec.ReadCallCount);
        Assert.Equal(0, clipboard.SetCallCount);
    }

    [Fact]
    public void GetHelp_ShouldDescribeTheLocalStdioContractAndOnlyImplementedTools()
    {
        var result = CreateTools().GetHelp();

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
    public async Task GetCursorPositionAsync_ShouldReturnTheCurrentGlobalLogicalPosition()
    {
        var provider = new TestMousePositionProvider
        {
            Position = (123, 456),
        };
        var tools = CreateTools(mousePositionProvider: provider);

        var result = await tools.GetCursorPositionAsync(CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var structured = Assert.IsType<JsonElement>(result.StructuredContent);
        Assert.Equal(123, structured.GetProperty("point").GetProperty("x").GetInt32());
        Assert.Equal(456, structured.GetProperty("point").GetProperty("y").GetInt32());
        Assert.Equal("test-cursor", structured.GetProperty("providerName").GetString());
    }

    [Fact]
    public async Task RegisteredTools_ShouldExposeAndServeTheImplementedMcpTools()
    {
        var clientToServer = new Pipe();
        var serverToClient = new Pipe();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var tools = new CrossMacroMcpTools(
            new TestRuntimeContext(),
            CreateDoctorService(),
            CreateProfileManager(),
             CreateSettingsCliService(),
             new ProfileCliService(CreateProfileManager()),
             new TestTextExpansionCliService(),
             new TestScheduleCliService(),
             new TestShortcutCliService(),
             new TestTriggerCliService(),
             new TestQuickSetupCliService(),
            CreateMacroExecutionService(),
            new TestClipboardCliService
            {
                GetResult = CliCommandExecutionResult.Ok("Clipboard text read.", new ClipboardTextData("protocol text")),
                SetResult = CliCommandExecutionResult.Ok("Clipboard text set.", new ClipboardSetData(13, "text")),
            },
            new TestWindowCliService
            {
                Result = CliCommandExecutionResult.Ok("Windows listed.", new WindowListData([], Count: 0)),
            },
            new TestScreenCliService
            {
                Result = CliCommandExecutionResult.Ok("Pixel 0,0: 000000", new ScreenPixelData(0, 0, "000000", "test", Relative: false)),
            },
            new TestScreenshotCaptureService
            {
                Result = ScreenshotPngCaptureResult.Ok(new ScreenshotPngCaptureData(
                    CreatePngBytes(),
                    OutputPath: null,
                    Width: 1,
                    Height: 1,
                    Provider: "test",
                    IsRegion: false,
                    CopiedToClipboard: false)),
            },
            new TestImageAssetCodec
            {
                PngBytes = CreatePngBytes(),
                Frame = CreateImageFrame(),
            },
            new TestImageClipboardReader(),
            CreateImageClipboardService(),
            CreateOperationCoordinator(),
            CreateRunScriptExecutionService(),
            CreateRecordExecutionService(),
            CreatePreflightService(),
             CreateCliCommandExecutor(),
             new McpCommandPolicy(),
             new AllowAllMcpCapabilityPolicy(),
             new AllowAllMcpPathPolicy());
        var services = new ServiceCollection();
        _ = services
            .AddMcpServer(options => options.ProtocolVersion = "2026-07-28")
            .WithStreamServerTransport(
                clientToServer.Reader.AsStream(),
                serverToClient.Writer.AsStream())
            .WithTools(tools, McpJsonContext.Default.Options);

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
        Assert.NotEqual(true, status.IsError);
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

    private sealed class TestRuntimeContext : IRuntimeContext
    {
        public bool IsLinux => true;
        public bool IsWindows => false;
        public bool IsMacOS => false;
        public bool IsFlatpak => true;
        public string? SessionType => "wayland";
    }

    private static IDoctorService CreateDoctorService()
    {
        return new TestDoctorService(new DoctorReport { Checks = [] });
    }

    private static IProfileManager CreateProfileManager()
    {
        return new TestProfileManager(new ProfileInfo { Id = "work", Name = "Work" });
    }

    private static ISettingsCliService CreateSettingsCliService()
    {
        return new SettingsCliService(new TestSettingsService(new AppSettings()));
    }

    private static IMacroExecutionService CreateMacroExecutionService()
    {
        return new TestMacroExecutionService();
    }

    private static IClipboardCliService CreateClipboardCliService()
    {
        return new TestClipboardCliService();
    }

    private static IWindowCliService CreateWindowCliService()
    {
        return new TestWindowCliService();
    }

    private static IScreenCliService CreateScreenCliService()
    {
        return new TestScreenCliService();
    }

    private static IScreenshotCaptureService CreateScreenshotCaptureService()
    {
        return new TestScreenshotCaptureService();
    }

    private static IImageAssetCodec CreateImageAssetCodec()
    {
        return new TestImageAssetCodec();
    }

    private static IImageClipboardReader CreateImageClipboardReader()
    {
        return new TestImageClipboardReader { IsSupported = false };
    }

    private static IImageClipboardService CreateImageClipboardService()
    {
        return new TestImageClipboardService();
    }

    private static IMcpOperationCoordinator CreateOperationCoordinator()
    {
        return new McpOperationCoordinator();
    }

    private static IRunScriptExecutionService CreateRunScriptExecutionService()
    {
        return new TestRunScriptExecutionService();
    }

    private static IRecordExecutionService CreateRecordExecutionService()
    {
        return new TestRecordExecutionService();
    }

    private static ICliPreflightService CreatePreflightService()
    {
        return new TestCliPreflightService();
    }

    private static CliCommandExecutor CreateCliCommandExecutor(ICliCommandHandlerResolver? resolver = null)
    {
        return new CliCommandExecutor(resolver ?? new TestCliCommandHandlerResolver());
    }

    private static CrossMacroMcpTools CreateTools(
        IMacroExecutionService? macroExecutionService = null,
        IClipboardCliService? clipboardCliService = null,
        IWindowCliService? windowCliService = null,
        IScreenCliService? screenCliService = null,
        IScreenshotCaptureService? screenshotCaptureService = null,
        IImageAssetCodec? imageAssetCodec = null,
        IImageClipboardReader? imageClipboardReader = null,
        IImageClipboardService? imageClipboardService = null,
        IMcpOperationCoordinator? operationCoordinator = null,
        IRunScriptExecutionService? runScriptExecutionService = null,
        IRecordExecutionService? recordExecutionService = null,
        ICliPreflightService? cliPreflightService = null,
        CliCommandExecutor? cliCommandExecutor = null,
        IMcpCommandPolicy? commandPolicy = null,
        IMcpCapabilityPolicy? capabilityPolicy = null,
        IMcpPathPolicy? pathPolicy = null,
         ISettingsCliService? settingsCliService = null,
             IProfileCliService? profileCliService = null,
             ITextExpansionCliService? textExpansionCliService = null,
             IScheduleCliService? scheduleCliService = null,
             IShortcutCliService? shortcutCliService = null,
         ITriggerCliService? triggerCliService = null,
               IQuickSetupCliService? quickSetupCliService = null,
         IMousePositionProvider? mousePositionProvider = null)
    {
        return new CrossMacroMcpTools(
            new TestRuntimeContext(),
            CreateDoctorService(),
            CreateProfileManager(),
            settingsCliService ?? CreateSettingsCliService(),
            profileCliService ?? new ProfileCliService(CreateProfileManager()),
            textExpansionCliService ?? new TestTextExpansionCliService(),
            scheduleCliService ?? new TestScheduleCliService(),
            shortcutCliService ?? new TestShortcutCliService(),
            triggerCliService ?? new TestTriggerCliService(),
            quickSetupCliService ?? new TestQuickSetupCliService(),
            macroExecutionService ?? CreateMacroExecutionService(),
            clipboardCliService ?? CreateClipboardCliService(),
            windowCliService ?? CreateWindowCliService(),
            screenCliService ?? CreateScreenCliService(),
            screenshotCaptureService ?? CreateScreenshotCaptureService(),
            imageAssetCodec ?? CreateImageAssetCodec(),
            imageClipboardReader ?? CreateImageClipboardReader(),
            imageClipboardService ?? CreateImageClipboardService(),
            operationCoordinator ?? CreateOperationCoordinator(),
            runScriptExecutionService ?? CreateRunScriptExecutionService(),
            recordExecutionService ?? CreateRecordExecutionService(),
            cliPreflightService ?? CreatePreflightService(),
            cliCommandExecutor ?? CreateCliCommandExecutor(),
            commandPolicy ?? new McpCommandPolicy(),
            capabilityPolicy ?? new AllowAllMcpCapabilityPolicy(),
             pathPolicy ?? new AllowAllMcpPathPolicy(),
             mousePositionProvider: mousePositionProvider);
    }

    private sealed class AllowAllMcpPathPolicy : IMcpPathPolicy
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

    private sealed class TestMousePositionProvider : IMousePositionProvider
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

    private sealed class AllowAllMcpCapabilityPolicy : IMcpCapabilityPolicy
    {
        public bool IsRestricted => false;

        public bool IsAllowed(McpCapability capability) => true;

        public bool IsAnyAllowed(params McpCapability[] capabilities) => true;

        public McpToolOutcome Require(McpCapability capability) => McpToolOutcomeMapper.Success(string.Empty);

        public void SetRestricted(bool restricted)
        {
        }
    }

    private sealed class TestSettingsService(AppSettings settings) : ISettingsService
    {
        public AppSettings Current { get; } = settings;

        public AppSettings Load() => Current;

        public Task<AppSettings> LoadAsync() => Task.FromResult(Current);

        public void Save()
        {
        }

        public Task SaveAsync() => Task.CompletedTask;
    }

    private sealed class TestProfileCliService : IProfileCliService
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

    private sealed class TestTextExpansionCliService : ITextExpansionCliService
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

    private sealed class TestScheduleCliService : IScheduleCliService
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

    private sealed class TestShortcutCliService : IShortcutCliService
    {
        public CliCommandExecutionResult ListResult { get; init; } = CliCommandExecutionResult.Ok("Loaded 0 shortcut task(s).", new TaskListData<ShortcutTaskData>(0, []));
        public CliCommandExecutionResult ExecuteResult { get; init; } = CliCommandExecutionResult.Ok("Shortcut task updated.");

        public Task<CliCommandExecutionResult> ListAsync(CancellationToken cancellationToken) => Task.FromResult(ListResult);
        public Task<CliCommandExecutionResult> RunAsync(string taskId, CancellationToken cancellationToken) => Task.FromResult(ExecuteResult);
        public Task<CliCommandExecutionResult> ExecuteAsync(ShortcutCliOptions options, CancellationToken cancellationToken) => Task.FromResult(ExecuteResult);
    }

    private sealed class TestTriggerCliService : ITriggerCliService
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

    private sealed class TestQuickSetupCliService : IQuickSetupCliService
    {
        public QuickSetupStatus Status { get; init; } = new(true, "flatpak", false);
        public QuickSetupResult Result { get; init; } = new(true, "Quick setup completed.");

        public QuickSetupStatus GetStatus() => Status;
        public Task<QuickSetupCliResult> RunAsync(CancellationToken cancellationToken) => Task.FromResult(new QuickSetupCliResult(Status.Applicable, Status.Provider, Result));
    }


    private static string CreateTemporaryDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"crossmacro-mcp-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(directory);
        return directory;
    }

    private static string CreateTemporaryMacroFile()
    {
        var path = Path.Combine(Path.GetTempPath(), $"crossmacro-mcp-{Guid.NewGuid():N}.macro");
        File.WriteAllText(path, "macro");
        return path;
    }

    private static string CreateTemporaryPngFile()
    {
        var path = Path.Combine(Path.GetTempPath(), $"crossmacro-mcp-{Guid.NewGuid():N}.png");
        File.WriteAllBytes(path, [137, 80, 78, 71]);
        return path;
    }

    private static byte[] CreatePngBytes() => [137, 80, 78, 71, 13, 10, 26, 10];

    private static async Task<JsonElement> WaitForAutomationCompletionAsync(
        CrossMacroMcpTools tools,
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

    private static ScreenFrame CreateImageFrame() => new(
        new ScreenRect(0, 0, 2, 1),
        stride: 6,
        ScreenPixelFormat.Rgb24,
        new byte[] { 0, 0, 0, 0, 0, 0 });

    private static WindowInfoData CreateWindow(int index)
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

    private sealed class TestDoctorService(DoctorReport report) : IDoctorService
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

    private sealed class TestMacroExecutionService : IMacroExecutionService
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

    private sealed class WaitingMacroExecutionService : IMacroExecutionService
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

    private sealed class TestRunScriptExecutionService : IRunScriptExecutionService
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

    private sealed class TestRecordExecutionService : IRecordExecutionService
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

    private sealed class TestCliPreflightService : ICliPreflightService
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

    private sealed class TestClipboardCliService : IClipboardCliService
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

    private sealed class TestWindowCliService : IWindowCliService
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

    private sealed class TestCliCommandHandlerResolver(ICliCommandHandler? handler = null) : ICliCommandHandlerResolver
    {
        private readonly ICliCommandHandler? _handler = handler;

        public int ResolveCallCount { get; private set; }

        public ICliCommandHandler? Resolve(CliCommandOptions options)
        {
            ResolveCallCount++;
            return _handler;
        }
    }

    private sealed class TestCliCommandHandler<TOptions>(CliCommandExecutionResult result) : CliCommandHandlerBase<TOptions>
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

    private sealed class ThrowingCliCommandHandler(string detail) : ICliCommandHandler
    {
        private readonly string _detail = detail;

        public bool CanHandle(CliCommandOptions options) => options is DoctorCliOptions;

        public Task<CliCommandExecutionResult> ExecuteAsync(CliCommandOptions options, CancellationToken cancellationToken) =>
            Task.FromException<CliCommandExecutionResult>(new InvalidOperationException(_detail));
    }

    private sealed class RecordingCliCommandHandler : ICliCommandHandler
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

    private sealed class TestScreenCliService : IScreenCliService
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

    private sealed class TestScreenshotCaptureService : IScreenshotCaptureService
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

    private enum TestImageAssetFailure
    {
        None,
        Validation,
        File,
    }

    private sealed class TestImageAssetCodec : IImageAssetCodec
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

    private sealed class TestImageClipboardReader : IImageClipboardReader
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

    private sealed class TestImageClipboardService : IImageClipboardService
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

    private sealed class TestProfileManager(ProfileInfo activeProfile) : IProfileManager
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
}
