using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Core.Services;
using CrossMacro.Infrastructure.Services.ScreenCapture;
using CrossMacro.Platform.Abstractions;

namespace CrossMacro.Cli.Services;

public sealed class ScreenshotCliService : IScreenshotCliService
{
    private readonly IScreenFrameProvider? _screenFrameProvider;
    private readonly IImageClipboardService? _imageClipboardService;

    public ScreenshotCliService(IScreenFrameProvider? screenFrameProvider, IImageClipboardService? imageClipboardService)
    {
        _screenFrameProvider = screenFrameProvider;
        _imageClipboardService = imageClipboardService;
    }

    public async Task<CliCommandExecutionResult> ExecuteAsync(ScreenshotCliOptions options, CancellationToken cancellationToken)
    {
        if (!TryValidateOptions(options, out var validationError, out var region))
        {
            return validationError;
        }

        if (!TryGetScreenFrameProvider(out var provider, out var unsupported))
        {
            return unsupported;
        }

        return options.Action switch
        {
            ScreenshotCliAction.Capture => await CaptureAsync(provider, _imageClipboardService, options, region, cancellationToken).ConfigureAwait(false),
            _ => CliCommandExecutionResult.Fail(CliExitCode.InvalidArguments, "Unknown screenshot action.")
        };
    }

    private static async Task<CliCommandExecutionResult> CaptureAsync(
        IScreenFrameProvider provider,
        IImageClipboardService? imageClipboardService,
        ScreenshotCliOptions options,
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
            return CliCommandExecutionResult.Fail(CliExitCode.RuntimeError, "Screenshot capture failed.", [ex.Message]);
        }

        if (!captureResult.IsSuccess)
        {
            var code = captureResult.ErrorKind is ScreenReadErrorKind.Unsupported
                    or ScreenReadErrorKind.PermissionDenied
                    or ScreenReadErrorKind.BackendUnavailable
                ? CliExitCode.EnvironmentError
                : CliExitCode.RuntimeError;
            return CliCommandExecutionResult.Fail(code, "Screenshot capture failed.",
                [captureResult.ErrorMessage ?? captureResult.ErrorKind?.ToString() ?? "Unknown capture error."]);
        }

        using var frame = captureResult.Value!;

        if (options.Clipboard && (imageClipboardService is null || !imageClipboardService.IsSupported))
        {
            return CliCommandExecutionResult.Fail(
                CliExitCode.EnvironmentError,
                "Image clipboard is not supported in this runtime.",
                ["No supported IImageClipboardService is available for the current platform/session."]);
        }

        string? outputPath = null;
        byte[]? pngBytes = null;
        if (options.Clipboard)
        {
            using var pngStream = new MemoryStream();
            ScreenFramePngEncoder.Encode(frame, pngStream);
            pngBytes = pngStream.ToArray();
        }

        try
        {
            if (!string.IsNullOrWhiteSpace(options.OutputPath) && !options.Clipboard)
            {
                outputPath = Path.GetFullPath(options.OutputPath);
                WriteFrameToFile(frame, outputPath);
            }
            else if (!string.IsNullOrWhiteSpace(options.OutputPath))
            {
                outputPath = Path.GetFullPath(options.OutputPath);
                WriteBytesToFile(pngBytes!, outputPath);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return CliCommandExecutionResult.Fail(CliExitCode.FileError, "Failed to write screenshot file.", [ex.Message]);
        }

        if (options.Clipboard)
        {
            try
            {
                await imageClipboardService!.SetPngAsync(pngBytes!, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (ImageClipboardUnavailableException ex)
            {
                return CliCommandExecutionResult.Fail(CliExitCode.EnvironmentError, "Image clipboard is not supported in this runtime.", [ex.Message]);
            }
            catch (Exception ex)
            {
                return CliCommandExecutionResult.Fail(CliExitCode.RuntimeError, "Failed to copy screenshot to clipboard.", [ex.Message]);
            }
        }

        var data = new ScreenshotData(
            outputPath,
            frame.Width,
            frame.Height,
            "png",
            provider.ProviderName,
            region is not null,
            options.Clipboard);

        return CliCommandExecutionResult.Ok(BuildSuccessMessage(outputPath, options.Clipboard, frame), data);
    }

    private static void WriteFrameToFile(ScreenFrame frame, string outputPath)
    {
        EnsureOutputDirectory(outputPath);
        using var fileStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None);
        ScreenFramePngEncoder.Encode(frame, fileStream);
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

    private static string BuildSuccessMessage(string? outputPath, bool copiedToClipboard, ScreenFrame frame)
    {
        if (outputPath is not null && copiedToClipboard)
        {
            return $"Screenshot saved to {outputPath} and copied to clipboard ({frame.Width}x{frame.Height}).";
        }

        if (outputPath is not null)
        {
            return $"Screenshot saved to {outputPath} ({frame.Width}x{frame.Height}).";
        }

        return $"Screenshot copied to clipboard ({frame.Width}x{frame.Height}).";
    }

    private static bool TryValidateOptions(
        ScreenshotCliOptions options,
        [NotNullWhen(false)] out CliCommandExecutionResult? result,
        out ScreenRect? region)
    {
        region = null;

        if (string.IsNullOrWhiteSpace(options.OutputPath) && !options.Clipboard)
        {
            result = CliCommandExecutionResult.Fail(
                CliExitCode.InvalidArguments,
                "screenshot requires --output <path> or --clipboard.");
            return false;
        }

        var hasAnyRegionValue = options.RegionX.HasValue || options.RegionY.HasValue ||
                                options.RegionWidth.HasValue || options.RegionHeight.HasValue;
        if (hasAnyRegionValue &&
            (options.RegionX is null || options.RegionY is null ||
             options.RegionWidth is null || options.RegionHeight is null))
        {
            result = CliCommandExecutionResult.Fail(
                CliExitCode.InvalidArguments,
                "--region requires <x> <y> <width> <height>.");
            return false;
        }

        if (options.RegionX is int x && options.RegionY is int y &&
            options.RegionWidth is int width && options.RegionHeight is int height)
        {
            if (width <= 0 || height <= 0)
            {
                result = CliCommandExecutionResult.Fail(
                    CliExitCode.InvalidArguments,
                    "--region width and height must be positive.");
                return false;
            }

            region = new ScreenRect(x, y, width, height);
        }

        result = null;
        return true;
    }

    private bool TryGetScreenFrameProvider(
        [NotNullWhen(true)] out IScreenFrameProvider? provider,
        [NotNullWhen(false)] out CliCommandExecutionResult? result)
    {
        if (_screenFrameProvider is null || !_screenFrameProvider.IsSupported)
        {
            provider = null;
            result = CliCommandExecutionResult.Fail(
                CliExitCode.EnvironmentError,
                "Screenshot capture is not supported in this runtime.",
                ["No supported IScreenFrameProvider is available for the current platform/session."]);
            return false;
        }

        provider = _screenFrameProvider;
        result = null;
        return true;
    }
}
