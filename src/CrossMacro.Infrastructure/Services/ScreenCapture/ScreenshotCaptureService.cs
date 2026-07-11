using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Core.Services;
using CrossMacro.Platform.Abstractions;

namespace CrossMacro.Infrastructure.Services.ScreenCapture;

public sealed class ScreenshotCaptureService : IScreenshotCaptureService
{
    private readonly IScreenFrameProvider? _screenFrameProvider;
    private readonly IImageClipboardService? _imageClipboardService;
    private readonly IImageAssetCodec _imageAssetCodec;

    public ScreenshotCaptureService(IScreenFrameProvider? screenFrameProvider, IImageClipboardService? imageClipboardService, IImageAssetCodec? imageAssetCodec = null)
    {
        _screenFrameProvider = screenFrameProvider;
        _imageClipboardService = imageClipboardService;
        _imageAssetCodec = imageAssetCodec ?? new ImageAssetCodec();
    }

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
            pngBytes = EncodeToPngBytes(frame);
        }

        if (!string.IsNullOrWhiteSpace(outputPath))
        {
            var writeResult = WriteOutput(outputPath, frame, pngBytes);
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
        var readOptions = new ScreenReadOptions(null, null, cancellationToken);

        ScreenReadResult<ScreenFrame> captureResult;
        try
        {
            captureResult = await provider.CaptureFrameAsync(region, readOptions).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
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

    private (bool Success, string? OutputPath, ScreenshotCaptureResult? Failure) WriteOutput(string outputPath, ScreenFrame frame, byte[]? pngBytes)
    {
        try
        {
            var fullOutputPath = Path.GetFullPath(outputPath);
            if (pngBytes is null)
            {
                WriteFrameToFile(frame, fullOutputPath);
            }
            else
            {
                WriteBytesToFile(pngBytes, fullOutputPath);
            }

            return (true, fullOutputPath, null);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
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
            return ScreenshotCaptureResult.Ok(new ScreenshotCaptureData(null, 0, 0, "png", string.Empty, false, true));
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
        catch (Exception ex)
        {
            return ScreenshotCaptureResult.Fail(
                ScreenshotCaptureFailureKind.ClipboardWriteFailed,
                "Failed to copy screenshot to clipboard.",
                [ex.Message]);
        }
    }

    private byte[] EncodeToPngBytes(ScreenFrame frame)
    {
        using var pngStream = new MemoryStream();
        _imageAssetCodec.EncodePng(frame, pngStream);
        return pngStream.ToArray();
    }

    private void WriteFrameToFile(ScreenFrame frame, string outputPath)
    {
        EnsureOutputDirectory(outputPath);
        using var fileStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None);
        _imageAssetCodec.EncodePng(frame, fileStream);
    }

    private static void WriteBytesToFile(byte[] pngBytes, string outputPath)
    {
        EnsureOutputDirectory(outputPath);
        File.WriteAllBytes(outputPath, pngBytes);
    }

    private static void EnsureOutputDirectory(string outputPath)
    {
        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }
}
