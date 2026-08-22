namespace CrossMacro.Mcp.Tests;

public sealed class McpScreenToolsTests
{
    [Fact]
    public async Task ReadImageAsync_ShouldReturnImageOnlyWhenExplicitlyRequested()
    {
        var imagePath = McpTestData.CreateTemporaryPngFile();
        try
        {
            var pngBytes = McpTestData.CreatePngBytes();
            var imageAssetCodec = new TestImageAssetCodec
            {
                PngBytes = pngBytes,
                Frame = McpTestData.CreateImageFrame(),
            };
            var tools = McpToolTestFactory.CreateScreenTools(imageAssetCodec: imageAssetCodec);

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
        var imagePath = McpTestData.CreateTemporaryPngFile();
        try
        {
            var validationCodec = new TestImageAssetCodec { Failure = TestImageAssetFailure.Validation };
            var validationTools = McpToolTestFactory.CreateScreenTools(imageAssetCodec: validationCodec);

            var validation = await validationTools.ReadImageAsync(imagePath, cancellationToken: CancellationToken.None);

            Assert.Equal(true, validation.IsError);
            var validationStructured = Assert.IsType<JsonElement>(validation.StructuredContent);
            Assert.Equal("validation_error", validationStructured.GetProperty("outcome").GetProperty("errors")[0].GetProperty("code").GetString());
            Assert.DoesNotContain(imagePath, validationStructured.GetRawText(), StringComparison.Ordinal);

            var fileCodec = new TestImageAssetCodec { Failure = TestImageAssetFailure.File };
            var fileTools = McpToolTestFactory.CreateScreenTools(imageAssetCodec: fileCodec);

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
        var tools = McpToolTestFactory.CreateScreenTools(imageAssetCodec: imageAssetCodec);

        var result = await tools.ReadImageAsync("relative.png", cancellationToken: CancellationToken.None);

        Assert.Equal(true, result.IsError);
        Assert.Equal(0, imageAssetCodec.ReadCallCount);
    }

    [Fact]
    public async Task CaptureScreenshotAsync_ShouldReturnImageOnlyWhenExplicitlyRequested()
    {
        var pngBytes = McpTestData.CreatePngBytes();
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
        var tools = McpToolTestFactory.CreateScreenTools(screenshotCaptureService: screenshotService);

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
        var tools = McpToolTestFactory.CreateScreenTools(screenshotCaptureService: screenshotService);

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
                McpTestData.CreatePngBytes(),
                OutputPath: outputPath,
                Width: 2,
                Height: 1,
                Provider: "test",
                IsRegion: true,
                CopiedToClipboard: true)),
        };
        var tools = McpToolTestFactory.CreateScreenTools(screenshotCaptureService: screenshotService);

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
        var tools = McpToolTestFactory.CreateScreenTools(screenshotCaptureService: screenshotService);

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
        var tools = McpToolTestFactory.CreateScreenTools(screenshotCaptureService: screenshotService);

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
        var tools = McpToolTestFactory.CreateScreenTools(screenshotCaptureService: screenshotService);
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
        var tools = McpToolTestFactory.CreateScreenTools(screenCliService: screenService);

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
        var waitTools = McpToolTestFactory.CreateScreenTools(screenCliService: waitService);

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
        var searchTools = McpToolTestFactory.CreateScreenTools(screenCliService: searchService);

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
        var tools = McpToolTestFactory.CreateScreenTools(screenCliService: screenService);

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
        var tools = McpToolTestFactory.CreateScreenTools(screenCliService: screenService);

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
        var imagePath = McpTestData.CreateTemporaryPngFile();
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
            var tools = McpToolTestFactory.CreateScreenTools(screenCliService: screenService);

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
        var tools = McpToolTestFactory.CreateScreenTools(screenCliService: screenService);

        var result = await tools.FindScreenImageAsync(imagePath, cancellationToken: CancellationToken.None);

        Assert.Equal(true, result.IsError);
        Assert.Equal(0, screenService.CallCount);
    }

    [Fact]
    public async Task GetCursorPositionAsync_ShouldReturnTheCurrentGlobalLogicalPosition()
    {
        var provider = new TestMousePositionProvider
        {
            Position = (123, 456),
        };
        var tools = McpToolTestFactory.CreateScreenTools(mousePositionProvider: provider);

        var result = await tools.GetCursorPositionAsync(CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var structured = Assert.IsType<JsonElement>(result.StructuredContent);
        Assert.Equal(123, structured.GetProperty("point").GetProperty("x").GetInt32());
        Assert.Equal(456, structured.GetProperty("point").GetProperty("y").GetInt32());
        Assert.Equal("test-cursor", structured.GetProperty("providerName").GetString());
    }
}
