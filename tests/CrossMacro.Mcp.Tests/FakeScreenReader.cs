namespace CrossMacro.Mcp.Tests;

/// <summary>Implements the screen ports MCP depends on, returning configured results and recording each call.</summary>
internal sealed class FakeScreenReader : IScreenPixelReader, IScreenImageAutomation
{
    public string ProviderName => "test";

    public bool IsSupported { get; init; } = true;

    public ScreenReadResult<ScreenPixelColor> PixelResult { get; init; } =
        ScreenReadResultFactory.Failure<ScreenPixelColor>(ScreenReadErrorKind.CaptureFailed, "Pixel result was not configured.");

    public ScreenReadResult<ScreenPixelSearchMatch> SearchPixelResult { get; init; } =
        ScreenReadResultFactory.Failure<ScreenPixelSearchMatch>(ScreenReadErrorKind.CaptureFailed, "Pixel search result was not configured.");

    public ScreenImageAutomationResult ImageResult { get; init; } =
        ScreenImageAutomationResult.Failure(ScreenReadErrorKind.CaptureFailed, "Image result was not configured.");

    public int CallCount { get; private set; }

    public ScreenPoint? LastPoint { get; private set; }

    public ScreenRect? LastRegion { get; private set; }

    public ScreenPixelColor? LastExpected { get; private set; }

    public int? LastTolerance { get; private set; }

    public ScreenReadOptions? LastOptions { get; private set; }

    public ScreenImageAutomationRequest? LastImageRequest { get; private set; }

    public Task<ScreenReadResult<ScreenPixelColor>> GetPixelAsync(ScreenPoint point, ScreenReadOptions options)
    {
        options.CancellationToken.ThrowIfCancellationRequested();
        Record(options);
        LastPoint = point;
        return Task.FromResult(PixelResult);
    }

    public Task<ScreenReadResult<ScreenPixelColor>> WaitForPixelAsync(ScreenPoint point, ScreenPixelColor expected, ScreenReadOptions options)
    {
        options.CancellationToken.ThrowIfCancellationRequested();
        Record(options);
        LastPoint = point;
        LastExpected = expected;
        return Task.FromResult(PixelResult);
    }

    public Task<ScreenReadResult<ScreenPixelSearchMatch>> SearchPixelAsync(ScreenRect region, ScreenPixelColor expected, int tolerance, ScreenReadOptions options)
    {
        options.CancellationToken.ThrowIfCancellationRequested();
        Record(options);
        LastRegion = region;
        LastExpected = expected;
        LastTolerance = tolerance;
        return Task.FromResult(SearchPixelResult);
    }

    public Task<ScreenImageAutomationResult> SearchAsync(ScreenImageAutomationRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CallCount++;
        LastImageRequest = request;
        return Task.FromResult(ImageResult);
    }

    public Task<ScreenImageAutomationResult> WaitAsync(ScreenImageAutomationRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException("MCP does not expose image waits.");

    public Task<ScreenImageAutomationResult> ClickAsync(ScreenImageAutomationRequest request, int buttonCode, CancellationToken cancellationToken) =>
        throw new NotSupportedException("MCP does not expose image clicks.");

    public void Dispose()
    {
    }

    private void Record(ScreenReadOptions options)
    {
        CallCount++;
        LastOptions = options;
    }
}
