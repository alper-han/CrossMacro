using CrossMacro.Infrastructure.Services.ScreenReading;
using CrossMacro.Platform.Abstractions.ScreenReading;

namespace CrossMacro.TestInfrastructure;

/// <summary>Screen reader that reports every operation as unsupported.</summary>
internal sealed class NullScreenPixelReader : IScreenImageSearchReader
{
    public static readonly NullScreenPixelReader Instance = new();

    private NullScreenPixelReader()
    {
    }

    public string ProviderName => "NullScreenPixelReader";

    public bool IsSupported => false;

    public Task<ScreenReadResult<ScreenPixelColor>> GetPixelAsync(ScreenPoint point, ScreenReadOptions options)
    {
        options.CancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Unsupported<ScreenPixelColor>());
    }

    public Task<ScreenReadResult<ScreenPixelColor>> WaitForPixelAsync(ScreenPoint point, ScreenPixelColor expected, ScreenReadOptions options)
    {
        options.CancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Unsupported<ScreenPixelColor>());
    }

    public Task<ScreenReadResult<ScreenPixelSearchMatch>> SearchPixelAsync(ScreenRect region, ScreenPixelColor expected, int tolerance, ScreenReadOptions options)
    {
        options.CancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Unsupported<ScreenPixelSearchMatch>());
    }

    public Task<ScreenReadResult<ScreenImageMatch>> SearchImageAsync(
        ScreenRect? region,
        ScreenFrame imageTemplate,
        ScreenImageMatchOptions options,
        ScreenReadOptions readOptions)
    {
        readOptions.CancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Unsupported<ScreenImageMatch>());
    }

    public void Dispose()
    {
    }

    private static ScreenReadResult<T> Unsupported<T>() =>
        ScreenReadResultFactory.Failure<T>(ScreenReadErrorKind.Unsupported, "Screen reading is not available.");
}
