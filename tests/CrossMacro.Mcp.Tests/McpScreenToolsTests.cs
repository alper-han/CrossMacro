namespace CrossMacro.Mcp.Tests;

public sealed class McpScreenToolsTests
{
    [Theory]
    [InlineData(null, "auto", ScreenImageMatchMode.Automatic)]
    [InlineData("", "auto", ScreenImageMatchMode.Automatic)]
    [InlineData("   ", "auto", ScreenImageMatchMode.Automatic)]
    [InlineData(" FiRsT ", "first", ScreenImageMatchMode.First)]
    [InlineData("BEST", "best", ScreenImageMatchMode.Best)]
    public async Task ImageMatchMode_PreservesMcpDefaultAndWhitespacePolicy(string? token, string expectedToken, ScreenImageMatchMode expectedMode)
    {
        using var directory = new TempDirectory();
        var imagePath = directory.WriteFile("template.png");
        var reader = new FakeScreenReader { ImageResult = ScreenImageAutomationResult.NotFound("Not found.") };
        var tools = McpToolTestFactory.CreateScreenTools(screenReader: reader);

        var result = await tools.FindScreenImageAsync(imagePath, matchMode: token, cancellationToken: CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var request = Assert.IsType<ScreenImageAutomationRequest>(reader.LastImageRequest);
        Assert.Equal(ScreenImageMatchDefaults.Similarity, request.Similarity);
        Assert.Equal(expectedMode, request.MatchMode);
        var structured = Assert.IsType<JsonElement>(result.StructuredContent);
        Assert.Equal(expectedToken, structured.GetProperty("matchMode").GetString());
    }

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
        var reader = new FakeScreenReader { PixelResult = ScreenReadResultFactory.Success(new ScreenPixelColor(0x12, 0x34, 0x56)) };
        var tools = McpToolTestFactory.CreateScreenTools(screenReader: reader);

        var result = await tools.ReadScreenAsync(
            mode: "pixel",
            x: 3,
            y: 4,
            cancellationToken: CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        Assert.Equal(new ScreenPoint(3, 4), reader.LastPoint);
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
        Assert.Equal(1, reader.CallCount);
    }

    [Fact]
    public async Task ReadScreenAsync_ShouldMapWaitAndBoundColorSearch()
    {
        var red = new ScreenPixelColor(0xFF, 0x00, 0x00);
        var green = new ScreenPixelColor(0x00, 0xFF, 0x00);
        var waitReader = new FakeScreenReader { PixelResult = ScreenReadResultFactory.Success(red) };
        var waitTools = McpToolTestFactory.CreateScreenTools(screenReader: waitReader);

        var wait = await waitTools.ReadScreenAsync(
            mode: "wait_color",
            x: 1,
            y: 2,
            color: "ff0000",
            timeoutMs: 30_000,
            cancellationToken: CancellationToken.None);

        Assert.NotEqual(true, wait.IsError);
        Assert.Equal(new ScreenPoint(1, 2), waitReader.LastPoint);
        Assert.Equal(red, waitReader.LastExpected);
        Assert.Equal(TimeSpan.FromMilliseconds(30_000), waitReader.LastOptions?.Timeout);

        var searchReader = new FakeScreenReader
        {
            SearchPixelResult = ScreenReadResultFactory.Success(new ScreenPixelSearchMatch(new ScreenPoint(2, 3), green)),
        };
        var searchTools = McpToolTestFactory.CreateScreenTools(screenReader: searchReader);

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
        Assert.Equal(new ScreenRect(0, 0, 100, 100), searchReader.LastRegion);
        Assert.Equal(green, searchReader.LastExpected);
        Assert.Equal(12, searchReader.LastTolerance);
        var structured = Assert.IsType<JsonElement>(search.StructuredContent);
        Assert.True(structured.GetProperty("found").GetBoolean());
        Assert.Equal(2, structured.GetProperty("point").GetProperty("x").GetInt32());
        Assert.Equal(100, structured.GetProperty("region").GetProperty("width").GetInt32());
    }

    [Theory]
    [InlineData("wait_color", "FF0000", null, null, 30_001)]
    [InlineData("search_color", "FF0000", 0, 10, null)]
    [InlineData("search_color", "FF0000", 8_000, 8_000, null)]
    [InlineData("search_color", "invalid", 10, 10, null)]
    public async Task ReadScreenAsync_ShouldRejectInvalidOrUnboundedInputsWithoutReadingTheScreen(
        string mode,
        string color,
        int? x2,
        int? y2,
        int? timeoutMs)
    {
        var reader = new FakeScreenReader();
        var tools = McpToolTestFactory.CreateScreenTools(screenReader: reader);

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
        Assert.Equal(0, reader.CallCount);
    }

    [Theory]
    [InlineData(ScreenReadErrorKind.Unsupported, "environment_error")]
    [InlineData(ScreenReadErrorKind.PermissionDenied, "environment_error")]
    [InlineData(ScreenReadErrorKind.BackendUnavailable, "environment_error")]
    [InlineData(ScreenReadErrorKind.CaptureFailed, "runtime_error")]
    [InlineData(ScreenReadErrorKind.CaptureTimeout, "runtime_error")]
    [InlineData(ScreenReadErrorKind.InvalidArguments, "invalid_arguments")]
    public async Task ReadScreenAsync_ShouldMapFailureKindsToOutcomeCodesAndRedactBackendDetails(ScreenReadErrorKind kind, string expectedCode)
    {
        const string secret = "screen provider detail should not leak";
        var reader = new FakeScreenReader { PixelResult = ScreenReadResultFactory.Failure<ScreenPixelColor>(kind, secret) };
        var tools = McpToolTestFactory.CreateScreenTools(screenReader: reader);

        var result = await tools.ReadScreenAsync(
            mode: "pixel",
            x: 0,
            y: 0,
            cancellationToken: CancellationToken.None);

        Assert.Equal(true, result.IsError);
        var structured = Assert.IsType<JsonElement>(result.StructuredContent);
        Assert.Equal(expectedCode, structured.GetProperty("outcome").GetProperty("errors")[0].GetProperty("code").GetString());
        Assert.DoesNotContain(secret, structured.GetRawText(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReadScreenAsync_WhenReaderIsUnsupported_ReturnsAnEnvironmentError()
    {
        var reader = new FakeScreenReader { IsSupported = false };
        var tools = McpToolTestFactory.CreateScreenTools(screenReader: reader);

        var result = await tools.ReadScreenAsync(mode: "pixel", x: 0, y: 0, cancellationToken: CancellationToken.None);

        Assert.Equal(true, result.IsError);
        var structured = Assert.IsType<JsonElement>(result.StructuredContent);
        Assert.Equal("environment_error", structured.GetProperty("outcome").GetProperty("errors")[0].GetProperty("code").GetString());
        Assert.Equal(0, reader.CallCount);
    }

    [Fact]
    public async Task ReadScreenAsync_PropagatesCancellation()
    {
        var tools = McpToolTestFactory.CreateScreenTools(screenReader: new FakeScreenReader());
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
        using var directory = new TempDirectory();
        var imagePath = directory.WriteFile("template.png");
        var reader = new FakeScreenReader { ImageResult = ScreenImageAutomationResult.FoundAt(new ScreenPoint(7, 8), 0.9) };
        var tools = McpToolTestFactory.CreateScreenTools(screenReader: reader);

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
        var request = Assert.IsType<ScreenImageAutomationRequest>(reader.LastImageRequest);
        Assert.Equal(imagePath, request.ImagePath);
        Assert.Equal(new ScreenRect(0, 0, 10, 10), request.Region);
        Assert.Equal(0.8, request.Similarity);
        Assert.Equal(ScreenImageMatchMode.Best, request.MatchMode);
        var structured = Assert.IsType<JsonElement>(result.StructuredContent);
        Assert.True(structured.GetProperty("found").GetBoolean());
        Assert.Equal(7, structured.GetProperty("point").GetProperty("x").GetInt32());
        Assert.DoesNotContain(imagePath, structured.GetRawText(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task FindScreenImageAsync_WhenAutomationFails_ReportsTheFailureWithoutBackendDetails()
    {
        const string secret = "image backend detail should not leak";
        using var directory = new TempDirectory();
        var imagePath = directory.WriteFile("template.png");
        var reader = new FakeScreenReader { ImageResult = ScreenImageAutomationResult.Failure(ScreenReadErrorKind.BackendUnavailable, secret) };
        var tools = McpToolTestFactory.CreateScreenTools(screenReader: reader);

        var result = await tools.FindScreenImageAsync(imagePath, cancellationToken: CancellationToken.None);

        Assert.Equal(true, result.IsError);
        var structured = Assert.IsType<JsonElement>(result.StructuredContent);
        Assert.Equal("environment_error", structured.GetProperty("outcome").GetProperty("errors")[0].GetProperty("code").GetString());
        Assert.DoesNotContain(secret, structured.GetRawText(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("relative.png")]
    [InlineData("/tmp/template.jpg")]
    public async Task FindScreenImageAsync_ShouldRejectInvalidPathsWithoutSearching(string imagePath)
    {
        var reader = new FakeScreenReader();
        var tools = McpToolTestFactory.CreateScreenTools(screenReader: reader);

        var result = await tools.FindScreenImageAsync(imagePath, cancellationToken: CancellationToken.None);

        Assert.Equal(true, result.IsError);
        Assert.Equal(0, reader.CallCount);
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
