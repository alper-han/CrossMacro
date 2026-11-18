using System.Diagnostics.CodeAnalysis;

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

        var capture = await CapturePngCoreAsync(
            new ScreenshotPngCaptureRequest(outputPath, copyToClipboard, region),
            cancellationToken).ConfigureAwait(false);
        return capture.TryGetValue(out var png, out var failure)
            ? ScreenshotCaptureResult.Ok(new ScreenshotCaptureData(
                png.OutputPath,
                png.Width,
                png.Height,
                "png",
                png.Provider,
                png.IsRegion,
                png.CopiedToClipboard))
            : failure.ToResult();
    }

    public async Task<ScreenshotPngCaptureResult> CapturePngAsync(
        ScreenshotPngCaptureRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var capture = await CapturePngCoreAsync(request, cancellationToken).ConfigureAwait(false);
        return capture.TryGetValue(out var png, out var failure)
            ? ScreenshotPngCaptureResult.Ok(png)
            : failure.ToPngResult();
    }

    private async Task<ScreenshotCaptureResult> CaptureToFileAsync(
        string outputPath,
        ScreenRect? region,
        CancellationToken cancellationToken)
    {
        if (_screenFrameProvider is not { IsSupported: true } provider)
        {
            return CaptureFailure.ProviderUnsupported().ToResult();
        }

        var capture = await CaptureFrameAsync(provider, region, cancellationToken).ConfigureAwait(false);
        if (!capture.TryGetValue(out var capturedFrame, out var captureFailure))
        {
            return captureFailure.ToResult();
        }

        using var frame = capturedFrame;
        try
        {
            var fullOutputPath = Path.GetFullPath(outputPath);
            await AtomicFile.WriteAsync(
                fullOutputPath,
                stream => _imageAssetCodec.EncodePngAsync(frame, stream, cancellationToken),
                cancellationToken).ConfigureAwait(false);

            return ScreenshotCaptureResult.Ok(new ScreenshotCaptureData(
                fullOutputPath,
                frame.Width,
                frame.Height,
                "png",
                provider.ProviderName,
                region is not null,
                CopiedToClipboard: false));
        }
        catch (Exception ex) when (ex is not (OperationCanceledException or OutOfMemoryException))
        {
            return ScreenshotCaptureResult.Fail(
                ScreenshotCaptureFailureKind.FileWriteFailed,
                "Failed to write screenshot file.",
                [ex.Message]);
        }
    }

    private async Task<Step<ScreenshotPngCaptureData>> CapturePngCoreAsync(
        ScreenshotPngCaptureRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (request.MaximumEncodedBytes is <= 0 or > ScreenImageAssetPolicy.MaxEncodedBytes)
        {
            return Step<ScreenshotPngCaptureData>.Fail(new CaptureFailure(
                ScreenshotCaptureFailureKind.CaptureFailed,
                "Screenshot PNG capture options are invalid.",
                ["The maximum encoded PNG size is outside the supported range."],
                ScreenReadErrorKind.InvalidArguments));
        }

        if (_screenFrameProvider is not { IsSupported: true } provider)
        {
            return Step<ScreenshotPngCaptureData>.Fail(CaptureFailure.ProviderUnsupported());
        }

        if (request.CopyToClipboard && _imageClipboardService is not { IsSupported: true })
        {
            return Step<ScreenshotPngCaptureData>.Fail(new CaptureFailure(
                ScreenshotCaptureFailureKind.ClipboardUnsupported,
                "Image clipboard is not supported in this runtime.",
                ["No supported IImageClipboardService is available for the current platform/session."]));
        }

        var capture = await CaptureFrameAsync(provider, request.Region, cancellationToken).ConfigureAwait(false);
        if (!capture.TryGetValue(out var capturedFrame, out var captureFailure))
        {
            return Step<ScreenshotPngCaptureData>.Fail(captureFailure);
        }

        using var frame = capturedFrame;
        try
        {
            var pngBytes = await EncodeToPngBytesAsync(frame, request.MaximumEncodedBytes, cancellationToken).ConfigureAwait(false);
            string? fullOutputPath = null;
            if (!string.IsNullOrWhiteSpace(request.OutputPath))
            {
                var write = await WriteOutputAsync(request.OutputPath, pngBytes, cancellationToken).ConfigureAwait(false);
                if (!write.TryGetValue(out fullOutputPath, out var writeFailure))
                {
                    return Step<ScreenshotPngCaptureData>.Fail(writeFailure);
                }
            }

            if (request.CopyToClipboard
                && _imageClipboardService is { } clipboard
                && await CopyToClipboardAsync(clipboard, pngBytes, cancellationToken).ConfigureAwait(false) is { } clipboardFailure)
            {
                return Step<ScreenshotPngCaptureData>.Fail(clipboardFailure);
            }

            return Step<ScreenshotPngCaptureData>.Ok(new ScreenshotPngCaptureData(
                pngBytes,
                fullOutputPath,
                frame.Width,
                frame.Height,
                provider.ProviderName,
                request.Region is not null,
                request.CopyToClipboard));
        }
        catch (Exception ex) when (ex is not (OperationCanceledException or OutOfMemoryException))
        {
            return Step<ScreenshotPngCaptureData>.Fail(new CaptureFailure(
                ScreenshotCaptureFailureKind.CaptureFailed,
                "Screenshot PNG encoding failed.",
                [ex.Message]));
        }
    }

    private static async Task<Step<ScreenFrame>> CaptureFrameAsync(
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
        catch (Exception ex) when (ex is not (OperationCanceledException or OutOfMemoryException))
        {
            return Step<ScreenFrame>.Fail(new CaptureFailure(
                ScreenshotCaptureFailureKind.CaptureFailed,
                "Screenshot capture failed.",
                [ex.Message]));
        }

        if (captureResult.Value is { } frame && captureResult.IsSuccess)
        {
            return Step<ScreenFrame>.Ok(frame);
        }

        return Step<ScreenFrame>.Fail(new CaptureFailure(
            ScreenshotCaptureFailureKind.CaptureFailed,
            "Screenshot capture failed.",
            [captureResult.ErrorMessage ?? captureResult.ErrorKind?.ToString() ?? "Unknown capture error."],
            captureResult.ErrorKind));
    }

    private static async Task<Step<string>> WriteOutputAsync(
        string outputPath,
        ReadOnlyMemory<byte> pngBytes,
        CancellationToken cancellationToken)
    {
        try
        {
            var fullOutputPath = Path.GetFullPath(outputPath);
            await AtomicFile.WriteAsync(
                fullOutputPath,
                stream => stream.WriteAsync(pngBytes, cancellationToken).AsTask(),
                cancellationToken).ConfigureAwait(false);
            return Step<string>.Ok(fullOutputPath);
        }
        catch (Exception ex) when (ex is not (OperationCanceledException or OutOfMemoryException))
        {
            return Step<string>.Fail(new CaptureFailure(
                ScreenshotCaptureFailureKind.FileWriteFailed,
                "Failed to write screenshot file.",
                [ex.Message]));
        }
    }

    private static async Task<CaptureFailure?> CopyToClipboardAsync(
        IImageClipboardService imageClipboardService,
        ReadOnlyMemory<byte> pngBytes,
        CancellationToken cancellationToken)
    {
        try
        {
            await imageClipboardService.SetPngAsync(pngBytes, cancellationToken).ConfigureAwait(false);
            return null;
        }
        catch (ImageClipboardUnavailableException ex)
        {
            return new CaptureFailure(
                ScreenshotCaptureFailureKind.ClipboardUnsupported,
                "Image clipboard is not supported in this runtime.",
                [ex.Message]);
        }
        catch (Exception ex) when (ex is not (OperationCanceledException or OutOfMemoryException))
        {
            return new CaptureFailure(
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
        return pngStream.ToArray();
    }

    private sealed record CaptureFailure(
        ScreenshotCaptureFailureKind Kind,
        string Message,
        IReadOnlyList<string> Details,
        ScreenReadErrorKind? ReadErrorKind = null)
    {
        public static CaptureFailure ProviderUnsupported() => new(
            ScreenshotCaptureFailureKind.ProviderUnsupported,
            "Screenshot capture is not supported in this runtime.",
            ["No supported IScreenFrameProvider is available for the current platform/session."]);

        public ScreenshotCaptureResult ToResult() => ScreenshotCaptureResult.Fail(Kind, Message, Details, ReadErrorKind);

        public ScreenshotPngCaptureResult ToPngResult() => ScreenshotPngCaptureResult.Fail(Kind, Message, Details, ReadErrorKind);
    }

    private readonly record struct Step<T>(T? Value, CaptureFailure? Failure)
        where T : class
    {
        public static Step<T> Ok(T value) => new(Value: value, Failure: null);

        public static Step<T> Fail(CaptureFailure failure) => new(Value: null, Failure: failure);

        public bool TryGetValue([NotNullWhen(true)] out T? value, [NotNullWhen(false)] out CaptureFailure? failure)
        {
            value = Value;
            failure = Failure;
            return Failure is null;
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
