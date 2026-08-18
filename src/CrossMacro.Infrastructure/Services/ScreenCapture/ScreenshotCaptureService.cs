
namespace CrossMacro.Infrastructure.Services.ScreenCapture;

public sealed class ScreenshotCaptureService(IScreenFrameProvider? screenFrameProvider, IImageClipboardService? imageClipboardService, IImageAssetCodec? imageAssetCodec = null) : IScreenshotCaptureService
{
    private static readonly TimeSpan CaptureTimeout = TimeSpan.FromSeconds(1);

    private readonly IScreenFrameProvider? _screenFrameProvider = screenFrameProvider;
    private readonly IImageClipboardService? _imageClipboardService = imageClipboardService;
    private readonly IImageAssetCodec _imageAssetCodec = imageAssetCodec ?? new ImageAssetCodec();

    public async Task<ScreenshotCaptureResult> CaptureAsync(
        string? outputPath,
        bool copyToClipboard,
        ScreenRect? region,
        CancellationToken cancellationToken)
    {
        if (_screenFrameProvider is null || !_screenFrameProvider.IsSupported)
        {
            return ScreenshotCaptureResult.Fail(
                ScreenshotCaptureFailureKind.ProviderUnsupported,
                "Screenshot capture is not supported in this runtime.",
                ["No supported IScreenFrameProvider is available for the current platform/session."]);
        }

        if (copyToClipboard && (_imageClipboardService is null || !_imageClipboardService.IsSupported))
        {
            return ScreenshotCaptureResult.Fail(
                ScreenshotCaptureFailureKind.ClipboardUnsupported,
                "Image clipboard is not supported in this runtime.",
                ["No supported IImageClipboardService is available for the current platform/session."]);
        }

        var captureResult = await CaptureFrameAsync(_screenFrameProvider, region, cancellationToken).ConfigureAwait(false);
        if (!captureResult.IsSuccess)
        {
            return captureResult.Failure!;
        }

        using var frame = captureResult.Frame!;

        byte[]? pngBytes = null;
        string? fullOutputPath = null;
        if (copyToClipboard)
        {
            pngBytes = await EncodeToPngBytesAsync(frame, cancellationToken).ConfigureAwait(false);
        }

        if (!string.IsNullOrWhiteSpace(outputPath))
        {
            var writeResult = await WriteOutputAsync(outputPath, frame, pngBytes, cancellationToken).ConfigureAwait(false);
            if (!writeResult.Success)
            {
                return writeResult.Failure!;
            }

            fullOutputPath = writeResult.OutputPath;
        }

        if (copyToClipboard)
        {
            var clipboardResult = await CopyToClipboardAsync(_imageClipboardService!, pngBytes!, cancellationToken).ConfigureAwait(false);
            if (!clipboardResult.Success)
            {
                return clipboardResult;
            }
        }

        return ScreenshotCaptureResult.Ok(new ScreenshotCaptureData(
            fullOutputPath,
            frame.Width,
            frame.Height,
            "png",
            _screenFrameProvider.ProviderName,
            region is not null,
            copyToClipboard));
    }

    private static async Task<(bool IsSuccess, ScreenFrame? Frame, ScreenshotCaptureResult? Failure)> CaptureFrameAsync(
        IScreenFrameProvider provider,
        ScreenRect? region,
        CancellationToken cancellationToken)
    {
        var readOptions = new ScreenReadOptions(CaptureTimeout, pollInterval: null, cancellationToken);

        ScreenReadResult<ScreenFrame> captureResult;
        try
        {
            captureResult = await provider.CaptureFrameAsync(region, readOptions).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return (false, null, ScreenshotCaptureResult.Fail(
                ScreenshotCaptureFailureKind.CaptureFailed,
                "Screenshot capture failed.",
                [ex.Message]));
        }

        if (captureResult.IsSuccess)
        {
            return (true, captureResult.Value!, null);
        }

        return (false, null, ScreenshotCaptureResult.Fail(
            ScreenshotCaptureFailureKind.CaptureFailed,
            "Screenshot capture failed.",
            [captureResult.ErrorMessage ?? captureResult.ErrorKind?.ToString() ?? "Unknown capture error."],
            captureResult.ErrorKind));
    }

    private async Task<(bool Success, string? OutputPath, ScreenshotCaptureResult? Failure)> WriteOutputAsync(
        string outputPath,
        ScreenFrame frame,
        byte[]? pngBytes,
        CancellationToken cancellationToken)
    {
        try
        {
            var fullOutputPath = Path.GetFullPath(outputPath);
            if (pngBytes is null)
            {
                await WriteFrameToFileAsync(frame, fullOutputPath, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await WriteBytesToFileAsync(pngBytes, fullOutputPath, cancellationToken).ConfigureAwait(false);
            }

            return (true, fullOutputPath, null);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return (false, null, ScreenshotCaptureResult.Fail(
                ScreenshotCaptureFailureKind.FileWriteFailed,
                "Failed to write screenshot file.",
                [ex.Message]));
        }
    }

    private static async Task<ScreenshotCaptureResult> CopyToClipboardAsync(
        IImageClipboardService imageClipboardService,
        byte[] pngBytes,
        CancellationToken cancellationToken)
    {
        try
        {
            await imageClipboardService.SetPngAsync(pngBytes, cancellationToken).ConfigureAwait(false);
            return ScreenshotCaptureResult.Ok(new ScreenshotCaptureData(OutputPath: null, 0, 0, "png", string.Empty, IsRegion: false, CopiedToClipboard: true));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (ImageClipboardUnavailableException ex)
        {
            return ScreenshotCaptureResult.Fail(
                ScreenshotCaptureFailureKind.ClipboardUnsupported,
                "Image clipboard is not supported in this runtime.",
                [ex.Message]);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return ScreenshotCaptureResult.Fail(
                ScreenshotCaptureFailureKind.ClipboardWriteFailed,
                "Failed to copy screenshot to clipboard.",
                [ex.Message]);
        }
    }

    private async Task<byte[]> EncodeToPngBytesAsync(ScreenFrame frame, CancellationToken cancellationToken)
    {
        using var pngStream = new MemoryStream();
        await _imageAssetCodec.EncodePngAsync(frame, pngStream, cancellationToken).ConfigureAwait(false);
        return pngStream.ToArray();
    }

    private async Task WriteFrameToFileAsync(ScreenFrame frame, string outputPath, CancellationToken cancellationToken)
    {
        EnsureOutputDirectory(outputPath);
        var fileStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous);
        await using var fileStreamDisposal = fileStream.ConfigureAwait(false);
        await _imageAssetCodec.EncodePngAsync(frame, fileStream, cancellationToken).ConfigureAwait(false);
        await fileStream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task WriteBytesToFileAsync(byte[] pngBytes, string outputPath, CancellationToken cancellationToken)
    {
        EnsureOutputDirectory(outputPath);
        await File.WriteAllBytesAsync(outputPath, pngBytes, cancellationToken).ConfigureAwait(false);
    }

    private static void EnsureOutputDirectory(string outputPath)
    {
        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(directory))
        {
            _ = Directory.CreateDirectory(directory);
        }
    }
}
