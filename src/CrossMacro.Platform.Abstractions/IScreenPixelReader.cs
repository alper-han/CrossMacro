using System;
using System.Threading.Tasks;

namespace CrossMacro.Platform.Abstractions;

public interface IScreenPixelReader : IDisposable
{
    string ProviderName { get; }

    bool IsSupported { get; }

    Task<ScreenReadResult<ScreenPixelColor>> GetPixelAsync(ScreenPoint point, ScreenReadOptions options);

    Task<ScreenReadResult<ScreenPixelColor>> WaitForPixelAsync(ScreenPoint point, ScreenPixelColor expected, ScreenReadOptions options);

    Task<ScreenReadResult<ScreenPixelSearchMatch>> SearchPixelAsync(
        ScreenRect region,
        ScreenPixelColor expected,
        int tolerance,
        ScreenReadOptions options);
}
