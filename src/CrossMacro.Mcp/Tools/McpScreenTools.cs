namespace CrossMacro.Mcp.Tools;

public sealed class McpScreenTools(
    IScreenCliService screenCliService,
    IScreenshotCaptureService screenshotCaptureService,
    IImageAssetCodec imageAssetCodec,
    McpToolAuthorization authorization,
    McpPathAuthorizer pathAuthorizer,
    IMousePositionProvider? mousePositionProvider = null)
{
    private const int DefaultScreenTimeoutMs = 5_000;
    private const int MaximumScreenTimeoutMs = 30_000;
    private const int MaximumScreenRegionPixels = 16_777_216;
    private const int MaximumInlineScreenshotBytes = 8 * 1024 * 1024;

    private readonly IScreenCliService _screenCliService = screenCliService;
    private readonly IScreenshotCaptureService _screenshotCaptureService = screenshotCaptureService;
    private readonly IImageAssetCodec _imageAssetCodec = imageAssetCodec;
    private readonly McpToolAuthorization _authorization = authorization;
    private readonly McpPathAuthorizer _pathAuthorizer = pathAuthorizer;
    private readonly IMousePositionProvider? _mousePositionProvider = mousePositionProvider;

    [McpServerTool(Name = "screen.read", Title = "Read screen data", ReadOnly = true, Destructive = false, Idempotent = true, UseStructuredContent = true, OutputSchemaType = typeof(McpScreenReadResult))]
    [Description("Reads one pixel, waits for a color, or searches a bounded screen region without changing the desktop.")]
    public async Task<CallToolResult> ReadScreenAsync(
        string mode,
        int x,
        int y,
        string? color = null,
        int? x2 = null,
        int? y2 = null,
        int? tolerance = null,
        int? timeoutMs = null,
        CancellationToken cancellationToken = default)
    {
        var capability = _authorization.Require(McpCapability.ScreenRead);
        if (capability is not null)
        {
            return CreateScreenReadToolResult(
                outcome: capability,
                mode: string.Empty,
                point: null,
                color: null,
                expectedColor: null,
                region: null,
                tolerance: null,
                found: null,
                timeoutMs: null,
                providerName: null);
        }

        ArgumentNullException.ThrowIfNull(mode);
        if (!TryCreateScreenReadOptions(
                mode,
                x,
                y,
                color,
                x2,
                y2,
                tolerance,
                timeoutMs,
                out var normalizedMode,
                out var options,
                out var error))
        {
            return CreateScreenReadToolResult(
                outcome: error,
                mode: normalizedMode,
                point: null,
                color: null,
                expectedColor: null,
                region: null,
                tolerance: null,
                found: null,
                timeoutMs: null,
                providerName: null);
        }

        var result = await _screenCliService.ExecuteAsync(options, cancellationToken).ConfigureAwait(false);
        var outcome = McpToolOutcomeMapper.FromCliResultRedactingErrorDetails(result);
        if (!outcome.Success)
        {
            return CreateScreenReadToolResult(
                outcome,
                normalizedMode,
                point: null,
                color: null,
                expectedColor: options.ExpectedColor?.ToString(),
                region: ToScreenRegion(options),
                tolerance: options.Action is ScreenCliAction.SearchColor ? options.Tolerance : null,
                found: null,
                timeoutMs: options.TimeoutMs,
                providerName: null);
        }

        return result.Data switch
        {
            ScreenPixelData pixel => CreateScreenReadToolResult(
                outcome,
                normalizedMode,
                new McpScreenPoint(pixel.X, pixel.Y),
                pixel.Color,
                expectedColor: null,
                region: null,
                tolerance: null,
                found: null,
                timeoutMs: null,
                providerName: pixel.ProviderName),
            ScreenWaitColorData wait => CreateScreenReadToolResult(
                outcome,
                normalizedMode,
                new McpScreenPoint(wait.X, wait.Y),
                wait.ActualColor,
                wait.ExpectedColor,
                region: null,
                tolerance: null,
                found: wait.Matched,
                timeoutMs: wait.TimeoutMs,
                providerName: wait.ProviderName),
            ScreenSearchColorData search => CreateScreenReadToolResult(
                outcome,
                normalizedMode,
                search.X is int matchX && search.Y is int matchY ? new McpScreenPoint(matchX, matchY) : null,
                search.Color,
                search.ExpectedColor,
                new McpScreenRegion(search.RegionX, search.RegionY, search.RegionWidth, search.RegionHeight),
                search.Tolerance,
                search.Found,
                options.TimeoutMs,
                search.ProviderName),
            _ => CreateScreenReadToolResult(
                McpToolOutcomeMapper.RuntimeError("Screen data could not be read."),
                normalizedMode,
                point: null,
                color: null,
                expectedColor: null,
                region: null,
                tolerance: null,
                found: null,
                timeoutMs: options.TimeoutMs,
                providerName: null),
        };
    }

    [McpServerTool(Name = "cursor.position", Title = "Read cursor position", ReadOnly = true, Destructive = false, Idempotent = true, UseStructuredContent = true, OutputSchemaType = typeof(McpCursorPositionResult))]
    [Description("Reads the current logical global mouse position without moving the pointer. Use the returned point.x and point.y as coordinates for move abs. Returns an environment error when the active desktop provider cannot expose a global cursor position.")]
    public async Task<CallToolResult> GetCursorPositionAsync(CancellationToken cancellationToken = default)
    {
        var capability = _authorization.Require(McpCapability.ScreenRead);
        if (capability is not null)
        {
            return CreateToolResult(new McpCursorPositionResult(capability, Point: null, ProviderName: null));
        }

        if (_mousePositionProvider is null
            || !_mousePositionProvider.SupportsAbsolutePosition
            || !MousePositionProviderExtensions.HasUsableAbsolutePosition(_mousePositionProvider))
        {
            return CreateToolResult(new McpCursorPositionResult(
                McpToolOutcomeMapper.EnvironmentError("The active desktop session cannot provide a global cursor position."),
                Point: null,
                ProviderName: _mousePositionProvider?.ProviderName));
        }

        cancellationToken.ThrowIfCancellationRequested();
        var position = await _mousePositionProvider.GetAbsolutePositionAsync().ConfigureAwait(false);
        if (position is null)
        {
            return CreateToolResult(new McpCursorPositionResult(
                McpToolOutcomeMapper.EnvironmentError("The active desktop session could not read the global cursor position."),
                Point: null,
                ProviderName: _mousePositionProvider.ProviderName));
        }

        return CreateToolResult(new McpCursorPositionResult(
            McpToolOutcomeMapper.Success($"Cursor position: {position.Value.X.ToString(System.Globalization.CultureInfo.InvariantCulture)},{position.Value.Y.ToString(System.Globalization.CultureInfo.InvariantCulture)}."),
            new McpScreenPoint(position.Value.X, position.Value.Y),
            _mousePositionProvider.ProviderName));
    }

    [McpServerTool(Name = "screen.find_image", Title = "Find an image on screen", ReadOnly = true, Destructive = false, Idempotent = true, UseStructuredContent = true, OutputSchemaType = typeof(McpScreenImageSearchResult))]
    [Description("Searches a bounded screen region for an absolute regular PNG file without returning file content.")]
    public async Task<CallToolResult> FindScreenImageAsync(
        string imagePath,
        int? regionX = null,
        int? regionY = null,
        int? regionWidth = null,
        int? regionHeight = null,
        double? similarity = null,
        string? matchMode = null,
        CancellationToken cancellationToken = default)
    {
        var capability = _authorization.Require(McpCapability.ScreenRead);
        if (capability is not null)
        {
            return CreateScreenImageSearchToolResult(
                outcome: capability,
                found: null,
                point: null,
                score: null,
                region: null,
                similarity: null,
                matchMode: null,
                providerName: null);
        }

        capability = _authorization.Require(McpCapability.FileRead);
        if (capability is not null)
        {
            return CreateScreenImageSearchToolResult(
                outcome: capability,
                found: null,
                point: null,
                score: null,
                region: null,
                similarity: null,
                matchMode: null,
                providerName: null);
        }

        if (!TryCreateImageSearchOptions(
                imagePath,
                regionX,
                regionY,
                regionWidth,
                regionHeight,
                similarity,
                matchMode,
                out var options,
                out var error))
        {
            return CreateScreenImageSearchToolResult(
                outcome: error,
                found: null,
                point: null,
                score: null,
                region: null,
                similarity: null,
                matchMode: null,
                providerName: null);
        }

        var result = await _screenCliService.ExecuteAsync(options, cancellationToken).ConfigureAwait(false);
        var outcome = McpToolOutcomeMapper.FromCliResultRedactingErrorDetails(result);
        if (!outcome.Success)
        {
            return CreateScreenImageSearchToolResult(
                outcome,
                found: null,
                point: null,
                score: null,
                region: ToScreenRegion(options),
                similarity: options.Similarity,
                matchMode: ToMatchModeToken(options.MatchMode),
                providerName: null);
        }

        if (result.Data is not ScreenSearchImageData image)
        {
            return CreateScreenImageSearchToolResult(
                McpToolOutcomeMapper.RuntimeError("Screen image search could not be read."),
                found: null,
                point: null,
                score: null,
                region: ToScreenRegion(options),
                similarity: options.Similarity,
                matchMode: ToMatchModeToken(options.MatchMode),
                providerName: null);
        }

        return CreateScreenImageSearchToolResult(
            outcome,
            image.Found,
            image.X is int matchX && image.Y is int matchY ? new McpScreenPoint(matchX, matchY) : null,
            image.Score,
            ToScreenRegion(options),
            image.Similarity,
            image.MatchMode,
            image.ProviderName);
    }

    [McpServerTool(Name = "image.read", Title = "Read a PNG image", ReadOnly = true, Destructive = false, Idempotent = true, UseStructuredContent = true, OutputSchemaType = typeof(McpImageReadResult))]
    [Description("Validates an absolute regular PNG file and returns image content only when explicitly requested.")]
    public async Task<CallToolResult> ReadImageAsync(
        string imagePath,
        bool includeImage = false,
        CancellationToken cancellationToken = default)
    {
        var capability = _authorization.Require(McpCapability.FileRead);
        if (capability is not null)
        {
            return CreateImageReadToolResult(capability, width: null, height: null, pngBytes: null, imageIncluded: false);
        }

        if (!_pathAuthorizer.TryNormalizeScreenImagePath(imagePath, out var normalizedImagePath, out var error))
        {
            return CreateImageReadToolResult(error, width: null, height: null, pngBytes: null, imageIncluded: false);
        }

        try
        {
            var pngBytes = await _imageAssetCodec
                .ReadFileAsync(normalizedImagePath, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            using var frame = await _imageAssetCodec
                .DecodePngAsync(pngBytes, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            if (includeImage && pngBytes.Length > MaximumInlineScreenshotBytes)
            {
                return CreateImageReadToolResult(
                    McpToolOutcomeMapper.RuntimeError("PNG image exceeds the maximum inline image size."),
                    frame.Width,
                    frame.Height,
                    pngBytes,
                    imageIncluded: false);
            }

            return CreateImageReadToolResult(
                McpToolOutcomeMapper.Success("PNG image read."),
                frame.Width,
                frame.Height,
                pngBytes,
                includeImage);
        }
        catch (InvalidDataException)
        {
            return CreateImageReadToolResult(
                McpToolOutcomeMapper.ValidationError("PNG image could not be validated."),
                width: null,
                height: null,
                pngBytes: null,
                imageIncluded: false);
        }
        catch (NotSupportedException)
        {
            return CreateImageReadToolResult(
                McpToolOutcomeMapper.ValidationError("PNG image could not be validated."),
                width: null,
                height: null,
                pngBytes: null,
                imageIncluded: false);
        }
        catch (ArgumentException)
        {
            return CreateImageReadToolResult(
                McpToolOutcomeMapper.ValidationError("PNG image could not be validated."),
                width: null,
                height: null,
                pngBytes: null,
                imageIncluded: false);
        }
        catch (IOException)
        {
            return CreateImageReadToolResult(
                McpToolOutcomeMapper.FileError("PNG image could not be read."),
                width: null,
                height: null,
                pngBytes: null,
                imageIncluded: false);
        }
        catch (UnauthorizedAccessException)
        {
            return CreateImageReadToolResult(
                McpToolOutcomeMapper.FileError("PNG image could not be read."),
                width: null,
                height: null,
                pngBytes: null,
                imageIncluded: false);
        }
    }

    [McpServerTool(Name = "screenshot.capture", Title = "Capture a screenshot", ReadOnly = false, Destructive = false, Idempotent = false, UseStructuredContent = true, OutputSchemaType = typeof(McpScreenshotCaptureResult))]
    [Description("Captures one bounded screenshot inline only when requested, and optionally writes the same PNG to a file or image clipboard.")]
    public async Task<CallToolResult> CaptureScreenshotAsync(
        bool includeImage = false,
        string? outputPath = null,
        bool copyToClipboard = false,
        int? regionX = null,
        int? regionY = null,
        int? regionWidth = null,
        int? regionHeight = null,
        CancellationToken cancellationToken = default)
    {
        var capability = _authorization.Require(McpCapability.ScreenRead);
        if (capability is not null)
        {
            return CreateScreenshotCaptureToolResult(capability, data: null, imageIncluded: false);
        }

        if (!string.IsNullOrWhiteSpace(outputPath))
        {
            capability = _authorization.Require(McpCapability.FileWrite);
            if (capability is not null)
            {
                return CreateScreenshotCaptureToolResult(capability, data: null, imageIncluded: false);
            }
        }

        if (copyToClipboard)
        {
            capability = _authorization.Require(McpCapability.ClipboardWrite);
            if (capability is not null)
            {
                return CreateScreenshotCaptureToolResult(capability, data: null, imageIncluded: false);
            }
        }

        if (!includeImage && string.IsNullOrWhiteSpace(outputPath) && !copyToClipboard)
        {
            return CreateScreenshotCaptureToolResult(
                McpToolOutcomeMapper.InvalidArguments("Screenshot capture requires includeImage, outputPath, or copyToClipboard."),
                data: null,
                imageIncluded: false);
        }

        if (!_pathAuthorizer.TryNormalizeScreenshotOutputPath(outputPath, out var normalizedOutputPath, out var outputError))
        {
            return CreateScreenshotCaptureToolResult(
                outputError,
                data: null,
                imageIncluded: false);
        }

        if (!TryCreateOptionalBoundedScreenRegion(regionX, regionY, regionWidth, regionHeight, out var region, out var regionError))
        {
            return CreateScreenshotCaptureToolResult(
                regionError,
                data: null,
                imageIncluded: false);
        }

        var maximumEncodedBytes = includeImage
            ? MaximumInlineScreenshotBytes
            : ScreenshotPngCaptureRequest.DefaultMaximumEncodedBytes;
        var capture = await _screenshotCaptureService.CapturePngAsync(
            new ScreenshotPngCaptureRequest(normalizedOutputPath, copyToClipboard, ToScreenRect(region), maximumEncodedBytes),
            cancellationToken).ConfigureAwait(false);
        if (!capture.Success)
        {
            return CreateScreenshotCaptureToolResult(
                McpToolOutcomeMapper.FromScreenshotCaptureFailure(
                    capture.FailureKind!.Value,
                    capture.ScreenReadErrorKind,
                    capture.Message),
                data: null,
                imageIncluded: false);
        }

        var data = capture.Data!;
        if (includeImage && data.PngBytes.Length > MaximumInlineScreenshotBytes)
        {
            return CreateScreenshotCaptureToolResult(
                McpToolOutcomeMapper.RuntimeError("Screenshot PNG exceeds the maximum inline image size."),
                data,
                imageIncluded: false);
        }

        return CreateScreenshotCaptureToolResult(
            McpToolOutcomeMapper.Success("Screenshot captured."),
            data,
            imageIncluded: includeImage);
    }

    private static bool TryCreateScreenReadOptions(
        string mode,
        int x,
        int y,
        string? color,
        int? x2,
        int? y2,
        int? tolerance,
        int? timeoutMs,
        out string normalizedMode,
        out ScreenCliOptions options,
        out McpToolOutcome error)
    {
        normalizedMode = mode.Trim().ToLowerInvariant();
        options = new ScreenCliOptions(ScreenCliAction.Pixel);

        switch (normalizedMode)
        {
            case "pixel":
                if (color is not null || x2 is not null || y2 is not null || tolerance is not null || timeoutMs is not null)
                {
                    error = McpToolOutcomeMapper.InvalidArguments("Pixel mode accepts only x and y coordinates.");
                    return false;
                }

                options = new ScreenCliOptions(ScreenCliAction.Pixel, x, y);
                error = McpToolOutcomeMapper.Success(string.Empty);
                return true;

            case "wait_color":
                if (x2 is not null || y2 is not null || tolerance is not null)
                {
                    error = McpToolOutcomeMapper.InvalidArguments("Wait color mode does not accept search bounds or tolerance.");
                    return false;
                }

                if (!TryParseScreenColor(color, out var expectedColor, out error)
                    || !TryGetBoundedScreenTimeout(timeoutMs, out var waitTimeoutMs, out error))
                {
                    return false;
                }

                options = new ScreenCliOptions(ScreenCliAction.WaitColor, x, y, expectedColor, TimeoutMs: waitTimeoutMs);
                error = McpToolOutcomeMapper.Success(string.Empty);
                return true;

            case "search_color":
                if (!TryParseScreenColor(color, out var searchColor, out error)
                    || !TryCreateBoundedColorSearchRegion(x, y, x2, y2, out _, out error)
                    || !TryGetScreenTolerance(tolerance, out var searchTolerance, out error)
                    || !TryGetBoundedScreenTimeout(timeoutMs, out var searchTimeoutMs, out error))
                {
                    return false;
                }

                options = new ScreenCliOptions(
                    ScreenCliAction.SearchColor,
                    x,
                    y,
                    searchColor,
                    X2: x2,
                    Y2: y2,
                    TimeoutMs: searchTimeoutMs,
                    Tolerance: searchTolerance);
                error = McpToolOutcomeMapper.Success(string.Empty);
                return true;

            default:
                error = McpToolOutcomeMapper.InvalidArguments("Screen read mode must be pixel, wait_color, or search_color.");
                return false;
        }
    }

    private bool TryCreateImageSearchOptions(
        string imagePath,
        int? regionX,
        int? regionY,
        int? regionWidth,
        int? regionHeight,
        double? similarity,
        string? matchMode,
        out ScreenCliOptions options,
        out McpToolOutcome error)
    {
        options = new ScreenCliOptions(ScreenCliAction.SearchImage);
        if (!_pathAuthorizer.TryNormalizeScreenImagePath(imagePath, out var normalizedImagePath, out error)
            || !TryCreateOptionalBoundedScreenRegion(regionX, regionY, regionWidth, regionHeight, out var region, out error)
            || !TryGetImageSimilarity(similarity, out var effectiveSimilarity, out error)
            || !TryGetImageMatchMode(matchMode, out var effectiveMatchMode, out error))
        {
            return false;
        }

        options = new ScreenCliOptions(
            ScreenCliAction.SearchImage,
            ImagePath: normalizedImagePath,
            RegionX: region?.X,
            RegionY: region?.Y,
            RegionWidth: region?.Width,
            RegionHeight: region?.Height,
            Similarity: effectiveSimilarity,
            MatchMode: effectiveMatchMode);
        error = McpToolOutcomeMapper.Success(string.Empty);
        return true;
    }

    private static bool TryParseScreenColor(string? value, out ScreenPixelColor color, out McpToolOutcome error)
    {
        if (!ScreenPixelColor.TryParse(value, out color))
        {
            error = McpToolOutcomeMapper.InvalidArguments("Screen color must be exactly 6 hexadecimal RGB characters.");
            return false;
        }

        error = McpToolOutcomeMapper.Success(string.Empty);
        return true;
    }

    private static bool TryGetBoundedScreenTimeout(int? value, out int timeoutMs, out McpToolOutcome error)
    {
        timeoutMs = value ?? DefaultScreenTimeoutMs;
        if (timeoutMs is < 0 or > MaximumScreenTimeoutMs)
        {
            error = McpToolOutcomeMapper.InvalidArguments("Screen timeout must be between 0 and 30,000 milliseconds.");
            return false;
        }

        error = McpToolOutcomeMapper.Success(string.Empty);
        return true;
    }

    private static bool TryGetScreenTolerance(int? value, out int tolerance, out McpToolOutcome error)
    {
        tolerance = value ?? 0;
        if (tolerance is < 0 or > byte.MaxValue)
        {
            error = McpToolOutcomeMapper.InvalidArguments("Screen color tolerance must be between 0 and 255.");
            return false;
        }

        error = McpToolOutcomeMapper.Success(string.Empty);
        return true;
    }

    private static bool TryCreateBoundedColorSearchRegion(
        int x,
        int y,
        int? x2,
        int? y2,
        out McpScreenRegion region,
        out McpToolOutcome error)
    {
        region = new McpScreenRegion(0, 0, 1, 1);
        if (x2 is null || y2 is null)
        {
            error = McpToolOutcomeMapper.InvalidArguments("Screen color search requires x2 and y2 bounds.");
            return false;
        }

        if (x is var firstX && y is var firstY && x2.Value is var secondX && y2.Value is var secondY)
        {
            var left = Math.Min(firstX, secondX);
            var top = Math.Min(firstY, secondY);
            var width = (long)Math.Max(firstX, secondX) - left;
            var height = (long)Math.Max(firstY, secondY) - top;
            return TryCreateBoundedScreenRegion(left, top, width, height, out region, out error);
        }

        error = McpToolOutcomeMapper.InvalidArguments("Screen color search bounds are invalid.");
        return false;
    }

    private static bool TryCreateOptionalBoundedScreenRegion(
        int? x,
        int? y,
        int? width,
        int? height,
        out McpScreenRegion? region,
        out McpToolOutcome error)
    {
        region = null;
        if (x is null && y is null && width is null && height is null)
        {
            error = McpToolOutcomeMapper.Success(string.Empty);
            return true;
        }

        if (x is null || y is null || width is null || height is null)
        {
            error = McpToolOutcomeMapper.InvalidArguments("Screen region requires x, y, width, and height.");
            return false;
        }

        if (!TryCreateBoundedScreenRegion(x.Value, y.Value, width.Value, height.Value, out var requiredRegion, out error))
        {
            return false;
        }

        region = requiredRegion;
        return true;
    }

    private static bool TryCreateBoundedScreenRegion(
        int x,
        int y,
        long width,
        long height,
        out McpScreenRegion region,
        out McpToolOutcome error)
    {
        region = new McpScreenRegion(0, 0, 1, 1);
        if (width <= 0 || height <= 0)
        {
            error = McpToolOutcomeMapper.InvalidArguments("Screen region width and height must be positive.");
            return false;
        }

        if (width > int.MaxValue || height > int.MaxValue
            || x + width > int.MaxValue || x + width < int.MinValue
            || y + height > int.MaxValue || y + height < int.MinValue)
        {
            error = McpToolOutcomeMapper.InvalidArguments("Screen region endpoint exceeds the supported coordinate range.");
            return false;
        }

        if (width * height > MaximumScreenRegionPixels)
        {
            error = McpToolOutcomeMapper.InvalidArguments("Screen region exceeds the maximum allowed pixel count.");
            return false;
        }

        region = new McpScreenRegion(x, y, (int)width, (int)height);
        error = McpToolOutcomeMapper.Success(string.Empty);
        return true;
    }

    private static bool TryGetImageSimilarity(double? value, out double similarity, out McpToolOutcome error)
    {
        similarity = value ?? 0.95;
        if (!double.IsFinite(similarity) || similarity is < 0.0 or > 1.0)
        {
            error = McpToolOutcomeMapper.InvalidArguments("Screen image similarity must be a finite number between 0 and 1.");
            return false;
        }

        error = McpToolOutcomeMapper.Success(string.Empty);
        return true;
    }

    private static bool TryGetImageMatchMode(string? value, out ScreenImageMatchMode matchMode, out McpToolOutcome error)
    {
        matchMode = value?.Trim().ToLowerInvariant() switch
        {
            null or "" or "auto" => ScreenImageMatchMode.Automatic,
            "first" => ScreenImageMatchMode.First,
            "best" => ScreenImageMatchMode.Best,
            _ => (ScreenImageMatchMode)(-1),
        };
        if (!Enum.IsDefined(matchMode))
        {
            error = McpToolOutcomeMapper.InvalidArguments("Screen image match mode must be auto, first, or best.");
            return false;
        }

        error = McpToolOutcomeMapper.Success(string.Empty);
        return true;
    }

    private static string ToMatchModeToken(ScreenImageMatchMode matchMode) => matchMode switch
    {
        ScreenImageMatchMode.Automatic => "auto",
        ScreenImageMatchMode.First => "first",
        ScreenImageMatchMode.Best => "best",
        _ => "unknown",
    };

    private static McpScreenRegion? ToScreenRegion(ScreenCliOptions options)
    {
        return options.RegionX is int x
            && options.RegionY is int y
            && options.RegionWidth is int width
            && options.RegionHeight is int height
            ? new McpScreenRegion(x, y, width, height)
            : null;
    }

    private static ScreenRect? ToScreenRect(McpScreenRegion? region)
    {
        return region is { } value
            ? new ScreenRect(value.X, value.Y, value.Width, value.Height)
            : null;
    }

    private static CallToolResult CreateScreenReadToolResult(
        McpToolOutcome outcome,
        string mode,
        McpScreenPoint? point,
        string? color,
        string? expectedColor,
        McpScreenRegion? region,
        int? tolerance,
        bool? found,
        int? timeoutMs,
        string? providerName)
    {
        return CreateToolResult(new McpScreenReadResult(
            outcome,
            mode,
            point,
            color,
            expectedColor,
            region,
            tolerance,
            found,
            timeoutMs,
            providerName));
    }

    private static CallToolResult CreateScreenImageSearchToolResult(
        McpToolOutcome outcome,
        bool? found,
        McpScreenPoint? point,
        double? score,
        McpScreenRegion? region,
        double? similarity,
        string? matchMode,
        string? providerName)
    {
        return CreateToolResult(new McpScreenImageSearchResult(
            outcome,
            found,
            point,
            score,
            region,
            similarity,
            matchMode,
            providerName));
    }

    private static CallToolResult CreateScreenshotCaptureToolResult(
        McpToolOutcome outcome,
        ScreenshotPngCaptureData? data,
        bool imageIncluded)
    {
        return CreateToolResult(new McpScreenshotCaptureResult(
            outcome,
            data?.Width,
            data?.Height,
            data?.Provider,
            data?.IsRegion,
            data?.OutputPath,
            data?.CopiedToClipboard,
            imageIncluded,
            data?.PngBytes.Length,
            MaximumInlineScreenshotBytes), data?.PngBytes);
    }

    private static CallToolResult CreateImageReadToolResult(
        McpToolOutcome outcome,
        int? width,
        int? height,
        ReadOnlyMemory<byte>? pngBytes,
        bool imageIncluded)
    {
        return CreateToolResult(new McpImageReadResult(
            outcome,
            width,
            height,
            imageIncluded,
            pngBytes?.Length,
            MaximumInlineScreenshotBytes), pngBytes);
    }

    private static CallToolResult CreateToolResult(McpScreenReadResult result)
    {
        return new CallToolResult
        {
            Content = [new TextContentBlock { Text = result.Outcome.Message }],
            StructuredContent = JsonSerializer.SerializeToElement(result, McpJsonContext.Default.McpScreenReadResult),
            IsError = !result.Outcome.Success,
        };
    }

    private static CallToolResult CreateToolResult(McpCursorPositionResult result)
    {
        return new CallToolResult
        {
            Content = [new TextContentBlock { Text = result.Outcome.Message }],
            StructuredContent = JsonSerializer.SerializeToElement(result, McpJsonContext.Default.McpCursorPositionResult),
            IsError = !result.Outcome.Success,
        };
    }

    private static CallToolResult CreateToolResult(McpScreenImageSearchResult result)
    {
        return new CallToolResult
        {
            Content = [new TextContentBlock { Text = result.Outcome.Message }],
            StructuredContent = JsonSerializer.SerializeToElement(result, McpJsonContext.Default.McpScreenImageSearchResult),
            IsError = !result.Outcome.Success,
        };
    }

    private static CallToolResult CreateToolResult(McpScreenshotCaptureResult result, ReadOnlyMemory<byte>? pngBytes)
    {
        return new CallToolResult
        {
            Content = CreateImageContent(result.Outcome.Message, result.ImageIncluded, pngBytes),
            StructuredContent = JsonSerializer.SerializeToElement(result, McpJsonContext.Default.McpScreenshotCaptureResult),
            IsError = !result.Outcome.Success,
        };
    }

    private static CallToolResult CreateToolResult(McpImageReadResult result, ReadOnlyMemory<byte>? pngBytes)
    {
        return new CallToolResult
        {
            Content = CreateImageContent(result.Outcome.Message, result.ImageIncluded, pngBytes),
            StructuredContent = JsonSerializer.SerializeToElement(result, McpJsonContext.Default.McpImageReadResult),
            IsError = !result.Outcome.Success,
        };
    }

    private static IList<ContentBlock> CreateImageContent(
        string message,
        bool imageIncluded,
        ReadOnlyMemory<byte>? pngBytes)
    {
        IList<ContentBlock> content = [new TextContentBlock { Text = message }];
        if (imageIncluded && pngBytes is { } image)
        {
            content.Add(new ImageContentBlock
            {
                Data = System.Text.Encoding.UTF8.GetBytes(Convert.ToBase64String(image.Span)),
                MimeType = "image/png",
            });
        }

        return content;
    }
}
