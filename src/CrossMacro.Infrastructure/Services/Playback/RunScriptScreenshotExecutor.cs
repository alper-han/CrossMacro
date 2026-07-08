using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Core.Services;
using CrossMacro.Infrastructure.Services.ScreenCapture;
using CrossMacro.Platform.Abstractions;

namespace CrossMacro.Infrastructure.Services.Playback;

internal sealed class RunScriptScreenshotExecutor
{
    private readonly IScreenshotCaptureService? _screenshotCaptureService;

    public RunScriptScreenshotExecutor(IScreenshotCaptureService? screenshotCaptureService)
    {
        _screenshotCaptureService = screenshotCaptureService;
    }

    public async Task ExecuteStepAsync(string step, int stepNumber, IDictionary<string, string> variables, CancellationToken cancellationToken)
    {
        if (_screenshotCaptureService is null)
        {
            throw new InvalidOperationException($"Step {stepNumber}: Screenshot script steps require an IScreenshotCaptureService runtime service.");
        }

        try
        {
            var resolvedStep = RunScriptRuntimeText.ResolveVariables(step, variables, $"Step {stepNumber}: ");
            if (!RunScriptSyntax.TryParseScreenshotStep(resolvedStep, out var parsed, out var error))
            {
                throw new InvalidOperationException(error ?? "Invalid screenshot syntax.");
            }

            var region = parsed.UseRegion
                ? new ScreenRect(
                    ParseAndValidateCoordinate(parsed.RegionX, "region x", 0),
                    ParseAndValidateCoordinate(parsed.RegionY, "region y", 0),
                    ParseAndValidateCoordinate(parsed.RegionWidth, "region width", 1),
                    ParseAndValidateCoordinate(parsed.RegionHeight, "region height", 1))
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
        catch (InvalidOperationException ex) when (!ex.Message.StartsWith($"Step {stepNumber}:", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Step {stepNumber}: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Step {stepNumber}: Screenshot capture failed.", ex);
        }
    }

    private static int ParseAndValidateCoordinate(string value, string description, int minValue)
    {
        var parsed = ParseInteger(value, description);
        if (parsed < minValue)
        {
            throw new InvalidOperationException($"Screenshot {description} must be >= {minValue}.");
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
        var details = result.Details.Count == 0 ? string.Empty : $" {string.Join(" ", result.Details.Where(detail => !string.IsNullOrWhiteSpace(detail)))}";
        return string.IsNullOrWhiteSpace(result.Message)
            ? $"Screenshot capture failed.{details}"
            : $"{result.Message}{details}";
    }
}
