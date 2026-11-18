
namespace CrossMacro.Cli.Services.ScreenReading;

public sealed class ScreenshotCliService(IScreenshotCaptureService screenshotCaptureService) : IScreenshotCliService
{
    private readonly IScreenshotCaptureService _screenshotCaptureService = screenshotCaptureService ?? throw new ArgumentNullException(nameof(screenshotCaptureService));

    public async Task<CliCommandExecutionResult> ExecuteAsync(ScreenshotCliOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (!TryValidateOptions(options, out var validationError, out var region))
        {
            return validationError;
        }

        return await CaptureAsync(_screenshotCaptureService, options, region, cancellationToken).ConfigureAwait(false);
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
            return CliCommandExecutionResult.Fail(ScreenFailureExitCodes.ForScreenshot(result.FailureKind, result.ScreenReadErrorKind), result.Message, result.Details);
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
            return $"Screenshot saved to {capture.OutputPath} and copied to clipboard ({capture.Width.ToString(CultureInfo.InvariantCulture)}x{capture.Height.ToString(CultureInfo.InvariantCulture)}).";
        }

        if (capture.OutputPath is not null)
        {
            return $"Screenshot saved to {capture.OutputPath} ({capture.Width.ToString(CultureInfo.InvariantCulture)}x{capture.Height.ToString(CultureInfo.InvariantCulture)}).";
        }

        return $"Screenshot copied to clipboard ({capture.Width.ToString(CultureInfo.InvariantCulture)}x{capture.Height.ToString(CultureInfo.InvariantCulture)}).";
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

        if (!ScreenRegionRules.TryCreate(options.RegionX, options.RegionY, options.RegionWidth, options.RegionHeight, "--region", out region, out var regionError))
        {
            result = CliCommandExecutionResult.Fail(CliExitCode.InvalidArguments, regionError);
            return false;
        }

        result = null;
        return true;
    }

}
