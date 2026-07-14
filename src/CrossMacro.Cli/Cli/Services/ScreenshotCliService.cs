using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Core.Services;
using CrossMacro.Platform.Abstractions;

namespace CrossMacro.Cli.Services;

public sealed partial class ScreenshotCliService : IScreenshotCliService
{
    private readonly IScreenshotCaptureService _screenshotCaptureService;

    public ScreenshotCliService(IScreenshotCaptureService screenshotCaptureService)
    {
        _screenshotCaptureService = screenshotCaptureService ?? throw new ArgumentNullException(nameof(screenshotCaptureService));
    }

    public async Task<CliCommandExecutionResult> ExecuteAsync(ScreenshotCliOptions options, CancellationToken cancellationToken)
    {
        if (!TryValidateOptions(options, out var validationError, out var region))
        {
            return validationError;
        }

        return options.Action switch
        {
            ScreenshotCliAction.Capture => await CaptureAsync(_screenshotCaptureService, options, region, cancellationToken).ConfigureAwait(false),
            _ => CliCommandExecutionResult.Fail(CliExitCode.InvalidArguments, "Unknown screenshot action.")
        };
    }

    private static async Task<CliCommandExecutionResult> CaptureAsync(
        IScreenshotCaptureService screenshotCaptureService,
        ScreenshotCliOptions options,
        ScreenRect? region,
        CancellationToken cancellationToken)
    {
        var result = await screenshotCaptureService.CaptureAsync(
            options.OutputPath,
            options.Clipboard,
            region,
            cancellationToken).ConfigureAwait(false);

        if (!result.Success)
        {
            return CliCommandExecutionResult.Fail(GetExitCode(result), result.Message, result.Details);
        }

        var capture = result.Data!;
        var data = new ScreenshotData(
            capture.OutputPath,
            capture.Width,
            capture.Height,
            capture.Format,
            capture.Provider,
            capture.IsRegion,
            capture.CopiedToClipboard);

        return CliCommandExecutionResult.Ok(BuildSuccessMessage(capture), data);
    }

    private static string BuildSuccessMessage(ScreenshotCaptureData capture)
    {
        if (capture.OutputPath is not null && capture.CopiedToClipboard)
        {
            return $"Screenshot saved to {capture.OutputPath} and copied to clipboard ({capture.Width}x{capture.Height}).";
        }

        if (capture.OutputPath is not null)
        {
            return $"Screenshot saved to {capture.OutputPath} ({capture.Width}x{capture.Height}).";
        }

        return $"Screenshot copied to clipboard ({capture.Width}x{capture.Height}).";
    }

    private static CliExitCode GetExitCode(ScreenshotCaptureResult result)
    {
        return result.FailureKind switch
        {
            ScreenshotCaptureFailureKind.ProviderUnsupported or ScreenshotCaptureFailureKind.ClipboardUnsupported => CliExitCode.EnvironmentError,
            ScreenshotCaptureFailureKind.FileWriteFailed => CliExitCode.FileError,
            ScreenshotCaptureFailureKind.CaptureFailed when result.ScreenReadErrorKind is ScreenReadErrorKind.Unsupported
                    or ScreenReadErrorKind.PermissionDenied
                    or ScreenReadErrorKind.BackendUnavailable => CliExitCode.EnvironmentError,
            _ => CliExitCode.RuntimeError
        };
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

            try
            {
                region = new ScreenRect(x, y, width, height);
            }
            catch (OverflowException)
            {
                result = CliCommandExecutionResult.Fail(
                    CliExitCode.InvalidArguments,
                    "--region endpoint exceeds the supported screen coordinate range.");
                return false;
            }
        }

        result = null;
        return true;
    }

}
