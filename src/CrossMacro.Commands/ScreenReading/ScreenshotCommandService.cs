using System.Globalization;
using CrossMacro.Platform.Abstractions.ScreenCapture;

namespace CrossMacro.Commands.ScreenReading;

public sealed class ScreenshotCommandService(IScreenshotCaptureService screenshotCaptureService)
{
    private readonly IScreenshotCaptureService _screenshotCaptureService = screenshotCaptureService ?? throw new ArgumentNullException(nameof(screenshotCaptureService));

    public async Task<OperationResult> ExecuteAsync(ScreenshotOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (!TryValidateOptions(options, out var validationError, out var region))
        {
            return validationError;
        }

        return await CaptureAsync(_screenshotCaptureService, options, region, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<OperationResult> CaptureAsync(
        IScreenshotCaptureService screenshotCaptureService,
        ScreenshotOptions options,
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
            return OperationResult.Fail(ScreenFailureOutcomes.ForScreenshot(result.FailureKind, result.ScreenReadErrorKind), result.Message, result.Details);
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

        return OperationResult.Ok(BuildSuccessMessage(capture), data);
    }

    private static string BuildSuccessMessage(ScreenshotCaptureData capture)
    {
        if (capture.OutputPath is not null && capture.CopiedToClipboard)
        {
            return $"Screenshot saved to {capture.OutputPath} and copied to clipboard ({capture.Width.ToString(CultureInfo.InvariantCulture)}x{capture.Height.ToString(CultureInfo.InvariantCulture)}).";
        }

        if (capture.OutputPath is not null)
        {
            return $"Screenshot saved to {capture.OutputPath} ({capture.Width.ToString(CultureInfo.InvariantCulture)}x{capture.Height.ToString(CultureInfo.InvariantCulture)}).";
        }

        return $"Screenshot copied to clipboard ({capture.Width.ToString(CultureInfo.InvariantCulture)}x{capture.Height.ToString(CultureInfo.InvariantCulture)}).";
    }

    private static bool TryValidateOptions(
        ScreenshotOptions options,
        [NotNullWhen(false)] out OperationResult? result,
        out ScreenRect? region)
    {
        region = null;

        if (string.IsNullOrWhiteSpace(options.OutputPath) && !options.Clipboard)
        {
            result = OperationResult.Fail(
                ExecutionOutcomeCode.InvalidArguments,
                "screenshot requires --output <path> or --clipboard.");
            return false;
        }

        if (!ScreenRegionRules.TryCreate(options.RegionX, options.RegionY, options.RegionWidth, options.RegionHeight, "--region", out region, out var regionError))
        {
            result = OperationResult.Fail(ExecutionOutcomeCode.InvalidArguments, regionError);
            return false;
        }

        result = null;
        return true;
    }

}
