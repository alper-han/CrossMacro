namespace CrossMacro.Mcp.Tools;

public sealed class McpClipboardTools(
    IClipboardCliService clipboardCliService,
    IImageAssetCodec imageAssetCodec,
    IImageClipboardReader imageClipboardReader,
    IImageClipboardService imageClipboardService,
    McpToolAuthorization authorization,
    McpPathAuthorizer pathAuthorizer)
{
    private const int MaximumClipboardTextCharacters = 65_536;
    private const int MaximumClipboardImageBytes = ScreenshotPngCaptureLimits.MaximumEncodedBytes;
    private const int MaximumInlineClipboardImageBytes = 8 * 1024 * 1024;

    private readonly IClipboardCliService _clipboardCliService = clipboardCliService;
    private readonly IImageAssetCodec _imageAssetCodec = imageAssetCodec;
    private readonly IImageClipboardReader _imageClipboardReader = imageClipboardReader;
    private readonly IImageClipboardService _imageClipboardService = imageClipboardService;
    private readonly McpToolAuthorization _authorization = authorization;
    private readonly McpPathAuthorizer _pathAuthorizer = pathAuthorizer;

    [McpServerTool(Name = "clipboard.get_text", Title = "Read text clipboard", ReadOnly = true, Destructive = false, Idempotent = true, UseStructuredContent = true, OutputSchemaType = typeof(McpClipboardTextResult))]
    [Description("Reads up to 65,536 characters of text from the system clipboard.")]
    public async Task<CallToolResult> GetClipboardTextAsync(CancellationToken cancellationToken)
    {
        var capability = _authorization.Require(McpCapability.ClipboardRead);
        if (capability is not null)
        {
            return CreateTextResult(capability, text: null, length: null);
        }

        var result = await _clipboardCliService.GetAsync(cancellationToken).ConfigureAwait(false);
        var outcome = McpToolOutcomeMapper.FromCliResultRedactingErrorDetails(result);
        if (!outcome.Success)
        {
            return CreateTextResult(outcome, text: null, length: null);
        }

        if (result.Data is not ClipboardTextData { Value: { } text })
        {
            return CreateTextResult(McpToolOutcomeMapper.RuntimeError("Clipboard text could not be read."), text: null, length: null);
        }

        if (text.Length > MaximumClipboardTextCharacters)
        {
            return CreateTextResult(McpToolOutcomeMapper.RuntimeError("Clipboard text exceeds the maximum allowed length."), text: null, length: null);
        }

        return CreateTextResult(outcome, text, text.Length);
    }

    [McpServerTool(Name = "clipboard.set_text", Title = "Set text clipboard", ReadOnly = false, Destructive = false, Idempotent = true, UseStructuredContent = true, OutputSchemaType = typeof(McpClipboardSetTextResult))]
    [Description("Sets up to 65,536 characters of text on the system clipboard without returning the text.")]
    public async Task<CallToolResult> SetClipboardTextAsync(string text, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(text);
        var capability = _authorization.Require(McpCapability.ClipboardWrite);
        if (capability is not null)
        {
            return CreateSetTextResult(capability, length: null);
        }

        if (text.Length > MaximumClipboardTextCharacters)
        {
            return CreateSetTextResult(McpToolOutcomeMapper.InvalidArguments("Clipboard text exceeds the maximum allowed length."), length: null);
        }

        var result = await _clipboardCliService.SetTextAsync(text, cancellationToken).ConfigureAwait(false);
        var outcome = McpToolOutcomeMapper.FromCliResultRedactingErrorDetails(result);
        var length = outcome.Success && result.Data is ClipboardSetData clipboardSet
            ? clipboardSet.Length
            : (int?)null;
        return CreateSetTextResult(outcome, length);
    }

    [McpServerTool(Name = "clipboard.get_image", Title = "Read image clipboard", ReadOnly = true, Destructive = false, Idempotent = true, UseStructuredContent = true, OutputSchemaType = typeof(McpClipboardImageResult))]
    [Description("Reads a validated PNG clipboard image only when the platform supports image reads. MCP image content is returned only when explicitly requested.")]
    public async Task<CallToolResult> GetClipboardImageAsync(bool includeImage = false, CancellationToken cancellationToken = default)
    {
        var capability = _authorization.Require(McpCapability.ClipboardRead);
        if (capability is not null)
        {
            return CreateImageResult(capability, imageAvailable: false, width: null, height: null, pngBytes: null, imageIncluded: false);
        }

        if (!_imageClipboardReader.IsSupported)
        {
            return CreateImageResult(McpToolOutcomeMapper.EnvironmentError("PNG image clipboard reading is not supported in this runtime."), imageAvailable: false, width: null, height: null, pngBytes: null, imageIncluded: false);
        }

        try
        {
            var pngBytes = await _imageClipboardReader.GetPngAsync(MaximumClipboardImageBytes, cancellationToken).ConfigureAwait(false);
            if (pngBytes is null)
            {
                return CreateImageResult(McpToolOutcomeMapper.Success("No PNG image is available on the clipboard."), imageAvailable: false, width: null, height: null, pngBytes: null, imageIncluded: false);
            }

            using var frame = await _imageAssetCodec.DecodePngAsync(pngBytes, cancellationToken: cancellationToken).ConfigureAwait(false);
            if (includeImage && pngBytes.Length > MaximumInlineClipboardImageBytes)
            {
                return CreateImageResult(McpToolOutcomeMapper.RuntimeError("Clipboard PNG exceeds the maximum inline image size."), imageAvailable: true, frame.Width, frame.Height, pngBytes, imageIncluded: false);
            }

            return CreateImageResult(McpToolOutcomeMapper.Success("PNG image read from the clipboard."), imageAvailable: true, frame.Width, frame.Height, pngBytes, includeImage);
        }
        catch (ImageClipboardUnavailableException)
        {
            return CreateImageResult(McpToolOutcomeMapper.EnvironmentError("PNG image clipboard reading is not supported in this runtime."), imageAvailable: false, width: null, height: null, pngBytes: null, imageIncluded: false);
        }
        catch (InvalidDataException)
        {
            return CreateImageResult(McpToolOutcomeMapper.ValidationError("Clipboard PNG could not be validated."), imageAvailable: false, width: null, height: null, pngBytes: null, imageIncluded: false);
        }
        catch (NotSupportedException)
        {
            return CreateImageResult(McpToolOutcomeMapper.ValidationError("Clipboard PNG could not be validated."), imageAvailable: false, width: null, height: null, pngBytes: null, imageIncluded: false);
        }
        catch (ArgumentException)
        {
            return CreateImageResult(McpToolOutcomeMapper.ValidationError("Clipboard PNG could not be validated."), imageAvailable: false, width: null, height: null, pngBytes: null, imageIncluded: false);
        }
        catch (InvalidOperationException)
        {
            return CreateImageResult(McpToolOutcomeMapper.RuntimeError("Clipboard PNG could not be read."), imageAvailable: false, width: null, height: null, pngBytes: null, imageIncluded: false);
        }
    }

    [McpServerTool(Name = "clipboard.set_image", Title = "Set image clipboard", ReadOnly = false, Destructive = false, Idempotent = true, UseStructuredContent = true, OutputSchemaType = typeof(McpClipboardSetImageResult))]
    [Description("Validates an absolute regular PNG file and sets it on the system image clipboard without returning image bytes.")]
    public async Task<CallToolResult> SetClipboardImageAsync(string imagePath, CancellationToken cancellationToken = default)
    {
        var capability = _authorization.Require(McpCapability.ClipboardWrite) ?? _authorization.Require(McpCapability.FileRead);
        if (capability is not null)
        {
            return CreateSetImageResult(capability, width: null, height: null, pngByteCount: null);
        }

        if (!_imageClipboardService.IsSupported)
        {
            return CreateSetImageResult(McpToolOutcomeMapper.EnvironmentError("PNG image clipboard writing is not supported in this runtime."), width: null, height: null, pngByteCount: null);
        }

        if (!_pathAuthorizer.TryNormalizeScreenImagePath(imagePath, out var normalizedImagePath, out var error))
        {
            return CreateSetImageResult(error, width: null, height: null, pngByteCount: null);
        }

        try
        {
            var pngBytes = await _imageAssetCodec.ReadFileAsync(normalizedImagePath, cancellationToken: cancellationToken).ConfigureAwait(false);
            using var frame = await _imageAssetCodec.DecodePngAsync(pngBytes, cancellationToken: cancellationToken).ConfigureAwait(false);
            await _imageClipboardService.SetPngAsync(pngBytes, cancellationToken).ConfigureAwait(false);
            return CreateSetImageResult(McpToolOutcomeMapper.Success("PNG image set on the clipboard."), frame.Width, frame.Height, pngBytes.Length);
        }
        catch (InvalidDataException)
        {
            return CreateSetImageResult(McpToolOutcomeMapper.ValidationError("PNG image could not be validated."), width: null, height: null, pngByteCount: null);
        }
        catch (NotSupportedException)
        {
            return CreateSetImageResult(McpToolOutcomeMapper.ValidationError("PNG image could not be validated."), width: null, height: null, pngByteCount: null);
        }
        catch (ArgumentException)
        {
            return CreateSetImageResult(McpToolOutcomeMapper.ValidationError("PNG image could not be validated."), width: null, height: null, pngByteCount: null);
        }
        catch (IOException)
        {
            return CreateSetImageResult(McpToolOutcomeMapper.FileError("PNG image could not be read."), width: null, height: null, pngByteCount: null);
        }
        catch (UnauthorizedAccessException)
        {
            return CreateSetImageResult(McpToolOutcomeMapper.FileError("PNG image could not be read."), width: null, height: null, pngByteCount: null);
        }
        catch (ImageClipboardUnavailableException)
        {
            return CreateSetImageResult(McpToolOutcomeMapper.EnvironmentError("PNG image clipboard writing is not supported in this runtime."), width: null, height: null, pngByteCount: null);
        }
        catch (InvalidOperationException)
        {
            return CreateSetImageResult(McpToolOutcomeMapper.RuntimeError("PNG image could not be written to the clipboard."), width: null, height: null, pngByteCount: null);
        }
    }

    private static CallToolResult CreateTextResult(McpToolOutcome outcome, string? text, int? length) =>
        CreateResult(new McpClipboardTextResult(outcome, text, length, MaximumClipboardTextCharacters));

    private static CallToolResult CreateSetTextResult(McpToolOutcome outcome, int? length) =>
        CreateResult(new McpClipboardSetTextResult(outcome, length, MaximumClipboardTextCharacters));

    private static CallToolResult CreateSetImageResult(McpToolOutcome outcome, int? width, int? height, int? pngByteCount) =>
        CreateResult(new McpClipboardSetImageResult(outcome, width, height, pngByteCount, MaximumClipboardImageBytes));

    private static CallToolResult CreateImageResult(McpToolOutcome outcome, bool imageAvailable, int? width, int? height, ReadOnlyMemory<byte>? pngBytes, bool imageIncluded) =>
        CreateResult(new McpClipboardImageResult(outcome, imageAvailable, width, height, imageIncluded, pngBytes?.Length, MaximumClipboardImageBytes, MaximumInlineClipboardImageBytes), pngBytes);

    private static CallToolResult CreateResult(McpClipboardTextResult result) =>
        new()
        {
            Content = [new TextContentBlock { Text = result.Outcome.Message }],
            StructuredContent = JsonSerializer.SerializeToElement(result, McpJsonContext.Default.McpClipboardTextResult),
            IsError = !result.Outcome.Success,
        };

    private static CallToolResult CreateResult(McpClipboardSetTextResult result) =>
        new()
        {
            Content = [new TextContentBlock { Text = result.Outcome.Message }],
            StructuredContent = JsonSerializer.SerializeToElement(result, McpJsonContext.Default.McpClipboardSetTextResult),
            IsError = !result.Outcome.Success,
        };

    private static CallToolResult CreateResult(McpClipboardSetImageResult result) =>
        new()
        {
            Content = [new TextContentBlock { Text = result.Outcome.Message }],
            StructuredContent = JsonSerializer.SerializeToElement(result, McpJsonContext.Default.McpClipboardSetImageResult),
            IsError = !result.Outcome.Success,
        };

    private static CallToolResult CreateResult(McpClipboardImageResult result, ReadOnlyMemory<byte>? pngBytes)
    {
        IList<ContentBlock> content = [new TextContentBlock { Text = result.Outcome.Message }];
        if (result.ImageIncluded && pngBytes is { } image)
        {
            content.Add(new ImageContentBlock
            {
                Data = System.Text.Encoding.UTF8.GetBytes(Convert.ToBase64String(image.Span)),
                MimeType = "image/png",
            });
        }

        return new CallToolResult
        {
            Content = content,
            StructuredContent = JsonSerializer.SerializeToElement(result, McpJsonContext.Default.McpClipboardImageResult),
            IsError = !result.Outcome.Success,
        };
    }
}
