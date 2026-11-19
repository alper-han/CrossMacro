using System.Globalization;

namespace CrossMacro.Infrastructure.Tests.TestDoubles;

/// <summary>Appends one line per screen read to a log shared with the other recording doubles of a test.</summary>
internal sealed class ActivityRecordingScreenPixelReader(List<string> activity) : IScreenImageSearchReader
{
    public string ProviderName => "recording-screen-reader";

    public bool IsSupported => true;

    public ScreenPixelColor PixelColor { get; init; } = new(0x12, 0x34, 0x56);

    public ScreenReadResult<ScreenPixelColor>? WaitResult { get; init; }

    public ScreenReadResult<ScreenPixelSearchMatch>? SearchResult { get; init; }

    public Task<ScreenReadResult<ScreenPixelColor>> GetPixelAsync(ScreenPoint point, ScreenReadOptions options)
    {
        options.CancellationToken.ThrowIfCancellationRequested();
        activity.Add(string.Create(CultureInfo.InvariantCulture, $"screen:pixelcolor:{point.X},{point.Y}"));
        return Task.FromResult(ScreenReadResultFactory.Success<ScreenPixelColor>(PixelColor));
    }

    public Task<ScreenReadResult<ScreenPixelColor>> WaitForPixelAsync(
        ScreenPoint point,
        ScreenPixelColor expected,
        ScreenReadOptions options)
    {
        options.CancellationToken.ThrowIfCancellationRequested();
        activity.Add(string.Create(CultureInfo.InvariantCulture, $"screen:waitcolor:{point.X},{point.Y}"));
        return Task.FromResult(WaitResult ?? ScreenReadResultFactory.Success<ScreenPixelColor>(expected));
    }

    public Task<ScreenReadResult<ScreenPixelSearchMatch>> SearchPixelAsync(
        ScreenRect region,
        ScreenPixelColor expected,
        int tolerance,
        ScreenReadOptions options)
    {
        options.CancellationToken.ThrowIfCancellationRequested();
        activity.Add(string.Create(CultureInfo.InvariantCulture, $"screen:pixelsearch:{region.X},{region.Y}"));
        return Task.FromResult(SearchResult ?? ScreenReadResultFactory.Success<ScreenPixelSearchMatch>(new ScreenPixelSearchMatch(new ScreenPoint(region.X, region.Y), expected)));
    }

    public Task<ScreenReadResult<ScreenImageMatch>> SearchImageAsync(
        ScreenRect? region,
        ScreenFrame imageTemplate,
        ScreenImageMatchOptions options,
        ScreenReadOptions readOptions)
    {
        readOptions.CancellationToken.ThrowIfCancellationRequested();
        activity.Add("screen:imagesearch");
        return Task.FromResult(ScreenReadResultFactory.Failure<ScreenImageMatch>(ScreenReadErrorKind.Unsupported, "Image search is not recorded."));
    }

    public void Dispose()
    {
    }
}
