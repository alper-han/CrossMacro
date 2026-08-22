namespace CrossMacro.Mcp.Tests;

public sealed class McpClipboardToolsTests
{
    [Fact]
    public async Task GetClipboardImageAsync_ShouldReturnImageOnlyWhenExplicitlyRequested()
    {
        var pngBytes = McpTestData.CreatePngBytes();
        var clipboardReader = new TestImageClipboardReader { PngBytes = pngBytes };
        var tools = McpToolTestFactory.CreateClipboardTools(
            imageAssetCodec: new TestImageAssetCodec { Frame = McpTestData.CreateImageFrame() },
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
        var noImageTools = McpToolTestFactory.CreateClipboardTools(imageClipboardReader: new TestImageClipboardReader());

        var noImage = await noImageTools.GetClipboardImageAsync(cancellationToken: CancellationToken.None);

        Assert.NotEqual(true, noImage.IsError);
        var noImageStructured = Assert.IsType<JsonElement>(noImage.StructuredContent);
        Assert.False(noImageStructured.GetProperty("imageAvailable").GetBoolean());
        Assert.False(noImageStructured.GetProperty("imageIncluded").GetBoolean());

        var unsupportedReader = new TestImageClipboardReader { IsSupported = false };
        var unsupportedTools = McpToolTestFactory.CreateClipboardTools(imageClipboardReader: unsupportedReader);

        var unsupported = await unsupportedTools.GetClipboardImageAsync(cancellationToken: CancellationToken.None);

        Assert.Equal(true, unsupported.IsError);
        Assert.Equal(0, unsupportedReader.CallCount);
        var unsupportedStructured = Assert.IsType<JsonElement>(unsupported.StructuredContent);
        Assert.Equal("environment_error", unsupportedStructured.GetProperty("outcome").GetProperty("errors")[0].GetProperty("code").GetString());
    }

    [Fact]
    public async Task GetClipboardImageAsync_ShouldMapValidationAndInlineBoundsWithoutImageContent()
    {
        var invalidTools = McpToolTestFactory.CreateClipboardTools(imageClipboardReader: new TestImageClipboardReader
        {
            Exception = new InvalidDataException("clipboard bytes are invalid"),
        });

        var invalid = await invalidTools.GetClipboardImageAsync(cancellationToken: CancellationToken.None);

        Assert.Equal(true, invalid.IsError);
        var invalidStructured = Assert.IsType<JsonElement>(invalid.StructuredContent);
        Assert.Equal("validation_error", invalidStructured.GetProperty("outcome").GetProperty("errors")[0].GetProperty("code").GetString());

        var oversizedTools = McpToolTestFactory.CreateClipboardTools(
            imageAssetCodec: new TestImageAssetCodec { Frame = McpTestData.CreateImageFrame() },
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
    public async Task GetClipboardTextAsync_ShouldReturnBoundedTextWithoutIncludingItInTheFallbackContent()
    {
        var clipboard = new TestClipboardCliService
        {
            GetResult = CliCommandExecutionResult.Ok("Clipboard text read.", new ClipboardTextData("sensitive text")),
        };
        var tools = McpToolTestFactory.CreateClipboardTools(clipboardCliService: clipboard);

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
        var tools = McpToolTestFactory.CreateClipboardTools(clipboardCliService: clipboard);

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
        var tools = McpToolTestFactory.CreateClipboardTools(clipboardCliService: clipboard);

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
        var tools = McpToolTestFactory.CreateClipboardTools(clipboardCliService: clipboard);

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
        var tools = McpToolTestFactory.CreateClipboardTools(clipboardCliService: clipboard);

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
        var tools = McpToolTestFactory.CreateClipboardTools(clipboardCliService: clipboard);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => tools.GetClipboardTextAsync(cancellation.Token));
        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => tools.SetClipboardTextAsync("text", cancellation.Token));
    }

    [Fact]
    public async Task SetClipboardImageAsync_ShouldValidateAndWritePngWithoutReturningThePath()
    {
        var path = McpTestData.CreateTemporaryPngFile();
        try
        {
            var clipboard = new TestImageClipboardService();
            var codec = new TestImageAssetCodec
            {
                PngBytes = McpTestData.CreatePngBytes(),
                Frame = McpTestData.CreateImageFrame(),
            };
            var tools = McpToolTestFactory.CreateClipboardTools(imageAssetCodec: codec, imageClipboardService: clipboard);

            var result = await tools.SetClipboardImageAsync(path, CancellationToken.None);

            Assert.NotEqual(true, result.IsError);
            Assert.Equal(1, clipboard.SetCallCount);
            Assert.Equal(McpTestData.CreatePngBytes(), clipboard.LastPngBytes);
            var structured = Assert.IsType<JsonElement>(result.StructuredContent);
            Assert.Equal(2, structured.GetProperty("width").GetInt32());
            Assert.Equal(1, structured.GetProperty("height").GetInt32());
            Assert.Equal(McpTestData.CreatePngBytes().Length, structured.GetProperty("pngByteCount").GetInt32());
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
        var codec = new TestImageAssetCodec { PngBytes = McpTestData.CreatePngBytes(), Frame = McpTestData.CreateImageFrame() };
        var clipboard = new TestImageClipboardService();
        var tools = McpToolTestFactory.CreateClipboardTools(imageAssetCodec: codec, imageClipboardService: clipboard);

        var result = await tools.SetClipboardImageAsync("relative.png", CancellationToken.None);

        Assert.Equal(true, result.IsError);
        Assert.Equal(0, codec.ReadCallCount);
        Assert.Equal(0, clipboard.SetCallCount);
    }
}
