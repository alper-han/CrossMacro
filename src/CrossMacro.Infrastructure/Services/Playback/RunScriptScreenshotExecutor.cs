
namespace CrossMacro.Infrastructure.Services.Playback;

internal sealed class RunScriptScreenshotExecutor(IScreenshotCaptureService? screenshotCaptureService)
{
    private readonly IScreenshotCaptureService? _screenshotCaptureService = screenshotCaptureService;

    public async Task ExecuteStepAsync(string step, int stepNumber, IDictionary<string, string> variables, CancellationToken cancellationToken)
    {
        if (_screenshotCaptureService is null)
        {
            throw new InvalidOperationException($"Step {stepNumber.ToString(CultureInfo.InvariantCulture)}: Screenshot script steps require an IScreenshotCaptureService runtime service.");
        }

        try
        {
            var resolvedStep = RunScriptRuntimeText.ResolveVariables(step, variables, $"Step {stepNumber.ToString(CultureInfo.InvariantCulture)}: ");
            if (!RunScriptPlatformSyntax.TryParseScreenshotStep(resolvedStep, out var parsed, out var error))
            {
                throw new InvalidOperationException(error ?? "Invalid screenshot syntax.");
            }

            var region = parsed.UseRegion
                ? ParseRegion(parsed)
                : (ScreenRect?)null;

            var result = await _screenshotCaptureService
                .CaptureAsync(parsed.OutputPath, parsed.CopyToClipboard, region, cancellationToken)
                .ConfigureAwait(false);

            if (!result.Success)
            {
                throw new InvalidOperationException(FormatFailure(result));
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (InvalidOperationException ex) when (!ex.Message.StartsWith($"Step {stepNumber.ToString(CultureInfo.InvariantCulture)}:", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Step {stepNumber.ToString(CultureInfo.InvariantCulture)}: {ex.Message}", ex);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            throw new InvalidOperationException($"Step {stepNumber.ToString(CultureInfo.InvariantCulture)}: Screenshot capture failed.", ex);
        }
    }

    private static ScreenRect ParseRegion(ScreenshotStep parsed)
    {
        var x = ParseInteger(parsed.RegionX, "region x");
        var y = ParseInteger(parsed.RegionY, "region y");
        var width = ParsePositiveInteger(parsed.RegionWidth, "region width");
        var height = ParsePositiveInteger(parsed.RegionHeight, "region height");
        try
        {
            return new ScreenRect(x, y, width, height);
        }
        catch (OverflowException ex)
        {
            throw new InvalidOperationException("Screenshot region endpoint exceeds the supported screen coordinate range.", ex);
        }
    }

    private static int ParsePositiveInteger(string value, string description)
    {
        var parsed = ParseInteger(value, description);
        if (parsed <= 0)
        {
            throw new InvalidOperationException($"Screenshot {description} must be >= 1.");
        }

        return parsed;
    }

    private static int ParseInteger(string value, string description)
    {
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            throw new InvalidOperationException($"Invalid screenshot {description} '{value}'. Expected integer.");
        }

        return parsed;
    }

    private static string FormatFailure(ScreenshotCaptureResult result)
    {
        var details = result.Details.Count is 0 ? string.Empty : $" {string.Join(' ', result.Details.Where(detail => !string.IsNullOrWhiteSpace(detail)))}";
        return string.IsNullOrWhiteSpace(result.Message)
            ? $"Screenshot capture failed.{details}"
            : $"{result.Message}{details}";
    }
}
