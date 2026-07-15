
namespace CrossMacro.Infrastructure.Services;

/// <summary>No-op IScreenPixelReader for runtime scripts that do not use screen-reading steps.</summary>
internal sealed class NullScreenPixelReader : IScreenPixelReader, IScreenImageSearchReader
{
    public static readonly NullScreenPixelReader Instance = new();

    private NullScreenPixelReader()
    {
    }

    public string ProviderName => "NullScreenPixelReader";

    public bool IsSupported => false;

    public Task<ScreenReadResult<ScreenPixelColor>> GetPixelAsync(ScreenPoint point, ScreenReadOptions options)
    {
        Core.Logging.Log.Warning("[NullScreenPixelReader] Screen reading is not available. GetPixelAsync called.");
        return Task.FromResult(ScreenReadResult<ScreenPixelColor>.Failure(ScreenReadErrorKind.Unsupported, "Screen reading is not available."));
    }

    public Task<ScreenReadResult<ScreenPixelColor>> WaitForPixelAsync(ScreenPoint point, ScreenPixelColor expected, ScreenReadOptions options)
    {
        Core.Logging.Log.Warning("[NullScreenPixelReader] Screen reading is not available. WaitForPixelAsync called.");
        return Task.FromResult(ScreenReadResult<ScreenPixelColor>.Failure(ScreenReadErrorKind.Unsupported, "Screen reading is not available."));
    }

    public Task<ScreenReadResult<ScreenPixelSearchMatch>> SearchPixelAsync(ScreenRect region, ScreenPixelColor expected, int tolerance, ScreenReadOptions options)
    {
        Core.Logging.Log.Warning("[NullScreenPixelReader] Screen reading is not available. SearchPixelAsync called.");
        return Task.FromResult(ScreenReadResult<ScreenPixelSearchMatch>.Failure(ScreenReadErrorKind.Unsupported, "Screen reading is not available."));
    }

    public Task<ScreenReadResult<ScreenImageMatch>> SearchImageAsync(
        ScreenRect? region,
        ScreenFrame template,
        ScreenImageMatchOptions matchOptions,
        ScreenReadOptions options)
    {
        Core.Logging.Log.Warning("[NullScreenPixelReader] Screen reading is not available. SearchImageAsync called.");
        return Task.FromResult(ScreenReadResult<ScreenImageMatch>.Failure(ScreenReadErrorKind.Unsupported, "Screen reading is not available."));
    }

    public void Dispose()
    {
    }
}
