
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
        if (!copyToClipboard && !string.IsNullOrWhiteSpace(outputPath))
        {
            return await CaptureToFileAsync(outputPath, region, cancellationToken).ConfigureAwait(false);
        }

        var pngCapture = await CapturePngAsync(
            new ScreenshotPngCaptureRequest(outputPath, copyToClipboard, region),
            cancellationToken).ConfigureAwait(false);
        if (!pngCapture.Success)
        {
            return ScreenshotCaptureResult.Fail(
                pngCapture.FailureKind!.Value,
                pngCapture.Message,
                pngCapture.Details,
                pngCapture.ScreenReadErrorKind);
        }

        var png = pngCapture.Data!;
        return ScreenshotCaptureResult.Ok(new ScreenshotCaptureData(
            png.OutputPath,
            png.Width,
            png.Height,
            "png",
            png.Provider,
            png.IsRegion,
            png.CopiedToClipboard));
    }

    private async Task<ScreenshotCaptureResult> CaptureToFileAsync(
        string outputPath,
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

        var captureResult = await CaptureFrameAsync(_screenFrameProvider, region, cancellationToken).ConfigureAwait(false);
        if (!captureResult.IsSuccess)
        {
            return captureResult.Failure!;
        }

        using var frame = captureResult.Frame!;
        try
        {
            var fullOutputPath = Path.GetFullPath(outputPath);
            EnsureOutputDirectory(fullOutputPath);
            var fileStream = new FileStream(
                fullOutputPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                4096,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            await using var fileStreamDisposal = fileStream.ConfigureAwait(false);
            await _imageAssetCodec.EncodePngAsync(frame, fileStream, cancellationToken).ConfigureAwait(false);
            await fileStream.FlushAsync(cancellationToken).ConfigureAwait(false);

            return ScreenshotCaptureResult.Ok(new ScreenshotCaptureData(
                fullOutputPath,
                frame.Width,
                frame.Height,
                "png",
                _screenFrameProvider.ProviderName,
                region is not null,
                CopiedToClipboard: false));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return ScreenshotCaptureResult.Fail(
                ScreenshotCaptureFailureKind.FileWriteFailed,
                "Failed to write screenshot file.",
                [ex.Message]);
        }
    }

    public async Task<ScreenshotPngCaptureResult> CapturePngAsync(
        ScreenshotPngCaptureRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        if (request.MaximumEncodedBytes is <= 0 or > ScreenImageAssetPolicy.MaxEncodedBytes)
        {
            return ScreenshotPngCaptureResult.Fail(
                ScreenshotCaptureFailureKind.CaptureFailed,
                "Screenshot PNG capture options are invalid.",
                ["The maximum encoded PNG size is outside the supported range."],
                ScreenReadErrorKind.InvalidArguments);
        }

        if (_screenFrameProvider is null || !_screenFrameProvider.IsSupported)
        {
            return ScreenshotPngCaptureResult.Fail(
                ScreenshotCaptureFailureKind.ProviderUnsupported,
                "Screenshot capture is not supported in this runtime.",
                ["No supported IScreenFrameProvider is available for the current platform/session."]);
        }

        if (request.CopyToClipboard && (_imageClipboardService is null || !_imageClipboardService.IsSupported))
        {
            return ScreenshotPngCaptureResult.Fail(
                ScreenshotCaptureFailureKind.ClipboardUnsupported,
                "Image clipboard is not supported in this runtime.",
                ["No supported IImageClipboardService is available for the current platform/session."]);
        }

        var captureResult = await CaptureFrameAsync(_screenFrameProvider, request.Region, cancellationToken).ConfigureAwait(false);
        if (!captureResult.IsSuccess)
        {
            var failure = captureResult.Failure!;
            return ScreenshotPngCaptureResult.Fail(
                failure.FailureKind!.Value,
                failure.Message,
                failure.Details,
                failure.ScreenReadErrorKind);
        }

        using var frame = captureResult.Frame!;
        try
        {
            var pngBytes = await EncodeToPngBytesAsync(frame, request.MaximumEncodedBytes, cancellationToken).ConfigureAwait(false);
            string? fullOutputPath = null;
            if (!string.IsNullOrWhiteSpace(request.OutputPath))
            {
                var writeResult = await WriteOutputAsync(request.OutputPath, pngBytes, cancellationToken).ConfigureAwait(false);
                if (!writeResult.Success)
                {
                    var failure = writeResult.Failure!;
                    return ScreenshotPngCaptureResult.Fail(
                        failure.FailureKind!.Value,
                        failure.Message,
                        failure.Details,
                        failure.ScreenReadErrorKind);
                }

                fullOutputPath = writeResult.OutputPath;
            }

            if (request.CopyToClipboard)
            {
                var clipboardResult = await CopyToClipboardAsync(_imageClipboardService!, pngBytes, cancellationToken).ConfigureAwait(false);
                if (!clipboardResult.Success)
                {
                    return ScreenshotPngCaptureResult.Fail(
                        clipboardResult.FailureKind!.Value,
                        clipboardResult.Message,
                        clipboardResult.Details,
                        clipboardResult.ScreenReadErrorKind);
                }
            }

            return ScreenshotPngCaptureResult.Ok(new ScreenshotPngCaptureData(
                pngBytes,
                fullOutputPath,
                frame.Width,
                frame.Height,
                _screenFrameProvider.ProviderName,
                request.Region is not null,
                request.CopyToClipboard));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return ScreenshotPngCaptureResult.Fail(
                ScreenshotCaptureFailureKind.CaptureFailed,
                "Screenshot PNG encoding failed.",
                [ex.Message]);
        }
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

    private static async Task<(bool Success, string? OutputPath, ScreenshotCaptureResult? Failure)> WriteOutputAsync(
        string outputPath,
        ReadOnlyMemory<byte> pngBytes,
        CancellationToken cancellationToken)
    {
        try
        {
            var fullOutputPath = Path.GetFullPath(outputPath);
            await WriteBytesToFileAsync(pngBytes, fullOutputPath, cancellationToken).ConfigureAwait(false);

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
        ReadOnlyMemory<byte> pngBytes,
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

    private async Task<byte[]> EncodeToPngBytesAsync(
        ScreenFrame frame,
        int maximumEncodedBytes,
        CancellationToken cancellationToken)
    {
        using var pngStream = new BoundedMemoryStream(maximumEncodedBytes);
        await _imageAssetCodec.EncodePngAsync(frame, pngStream, cancellationToken).ConfigureAwait(false);
        var pngBytes = pngStream.ToArray();
        var validation = await ScreenImageAssetPolicy
            .TryValidateEncodedPngAsync(pngBytes, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        if (!validation.IsValid)
        {
            throw new InvalidDataException(validation.Error ?? "Screenshot PNG validation failed.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        return pngBytes;
    }

    private static async Task WriteBytesToFileAsync(ReadOnlyMemory<byte> pngBytes, string outputPath, CancellationToken cancellationToken)
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

    private sealed class BoundedMemoryStream(int maximumLength) : MemoryStream
    {
        private readonly int _maximumLength = maximumLength;

        public override void Write(byte[] buffer, int offset, int count)
        {
            EnsureWriteFits(count);
            base.Write(buffer, offset, count);
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            EnsureWriteFits(buffer.Length);
            base.Write(buffer);
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            EnsureWriteFits(count);
            return base.WriteAsync(buffer, offset, count, cancellationToken);
        }

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            EnsureWriteFits(buffer.Length);
            return base.WriteAsync(buffer, cancellationToken);
        }

        public override void WriteByte(byte value)
        {
            EnsureWriteFits(1);
            base.WriteByte(value);
        }

        public override void SetLength(long value)
        {
            if (value > _maximumLength)
            {
                throw new InvalidDataException($"PNG output exceeds the maximum encoded size of {_maximumLength.ToString(CultureInfo.InvariantCulture)} bytes.");
            }

            base.SetLength(value);
        }

        private void EnsureWriteFits(int count)
        {
            if (Position > _maximumLength - count)
            {
                throw new InvalidDataException($"PNG output exceeds the maximum encoded size of {_maximumLength.ToString(CultureInfo.InvariantCulture)} bytes.");
            }
        }
    }
}
