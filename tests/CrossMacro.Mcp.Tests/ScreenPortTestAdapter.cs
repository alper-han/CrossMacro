namespace CrossMacro.Mcp.Tests;

internal sealed class ScreenPortTestAdapter(IScreenCliService service) : IScreenPixelReader, IScreenImageAutomation
{
    public string ProviderName { get; private set; } = "test";
    public bool IsSupported => true;

    public async Task<ScreenReadResult<ScreenPixelColor>> GetPixelAsync(ScreenPoint point, ScreenReadOptions options)
    {
        var result = await service.ExecuteAsync(new ScreenCliOptions(ScreenCliAction.Pixel, point.X, point.Y), options.CancellationToken);
        if (result.Data is ScreenPixelData pixel)
        {
            ProviderName = pixel.ProviderName;
            return ScreenReadResultFactory.Success(ParseColor(pixel.Color));
        }
        return Failure<ScreenPixelColor>(result);
    }

    public async Task<ScreenReadResult<ScreenPixelColor>> WaitForPixelAsync(ScreenPoint point, ScreenPixelColor expected, ScreenReadOptions options)
    {
        var result = await service.ExecuteAsync(new ScreenCliOptions(ScreenCliAction.WaitColor, point.X, point.Y, expected, TimeoutMs: (int?)options.Timeout?.TotalMilliseconds), options.CancellationToken);
        if (result.Data is ScreenWaitColorData wait)
        {
            ProviderName = wait.ProviderName;
            return ScreenReadResultFactory.Success(ParseColor(wait.ActualColor));
        }
        return Failure<ScreenPixelColor>(result);
    }

    public async Task<ScreenReadResult<ScreenPixelSearchMatch>> SearchPixelAsync(ScreenRect region, ScreenPixelColor expected, int tolerance, ScreenReadOptions options)
    {
        var result = await service.ExecuteAsync(new ScreenCliOptions(ScreenCliAction.SearchColor, region.X, region.Y, expected, X2: region.X + region.Width, Y2: region.Y + region.Height,
            Tolerance: tolerance, TimeoutMs: (int?)options.Timeout?.TotalMilliseconds), options.CancellationToken);
        if (result.Data is ScreenSearchColorData search)
        {
            ProviderName = search.ProviderName;
            return ScreenReadResultFactory.Success(new ScreenPixelSearchMatch(new ScreenPoint(search.X!.Value, search.Y!.Value), ParseColor(search.Color!)));
        }
        return Failure<ScreenPixelSearchMatch>(result);
    }

    public async Task<ScreenImageAutomationResult> SearchAsync(ScreenImageAutomationRequest request, CancellationToken cancellationToken)
    {
        var result = await service.ExecuteAsync(new ScreenCliOptions(ScreenCliAction.SearchImage, ImagePath: request.ImagePath,
            RegionX: request.Region?.X, RegionY: request.Region?.Y, RegionWidth: request.Region?.Width, RegionHeight: request.Region?.Height,
            Similarity: request.Similarity, MatchMode: request.MatchMode), cancellationToken);
        if (result.Data is ScreenSearchImageData image)
        {
            ProviderName = image.ProviderName;
            var noMatchMessage = result.Warnings.Count > 0 ? result.Warnings[0] : result.Message;
            return image.Found
                ? ScreenImageAutomationResult.FoundAt(new ScreenPoint(image.X!.Value, image.Y!.Value), image.Score!.Value)
                : ScreenImageAutomationResult.NotFound(noMatchMessage);
        }
        return ScreenImageAutomationResult.Failure(ErrorKind(result), result.Message);
    }

    public Task<ScreenImageAutomationResult> WaitAsync(ScreenImageAutomationRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<ScreenImageAutomationResult> ClickAsync(ScreenImageAutomationRequest request, int buttonCode, CancellationToken cancellationToken) => throw new NotSupportedException();
    public void Dispose() { }

    private static ScreenReadResult<TValue> Failure<TValue>(CliCommandExecutionResult result) =>
        ScreenReadResultFactory.Failure<TValue>(ErrorKind(result), result.Errors.Count > 0 ? result.Errors[0] : result.Message);

    private static ScreenReadErrorKind ErrorKind(CliCommandExecutionResult result) => (CliExitCode)result.ExitCode switch
    {
        CliExitCode.InvalidArguments => ScreenReadErrorKind.InvalidArguments,
        CliExitCode.EnvironmentError => ScreenReadErrorKind.BackendUnavailable,
        CliExitCode.Cancelled => ScreenReadErrorKind.Canceled,
        CliExitCode.Success or CliExitCode.FileError or CliExitCode.ValidationError or CliExitCode.RuntimeError => ScreenReadErrorKind.CaptureFailed,
        _ => ScreenReadErrorKind.CaptureFailed,
    };

    private static ScreenPixelColor ParseColor(string value) =>
        ScreenPixelColor.TryParse(value, out var color) ? color : throw new InvalidOperationException("Invalid fixture color");
}
