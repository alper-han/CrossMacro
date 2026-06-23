using System;
using System.Threading.Tasks;

namespace CrossMacro.Platform.Abstractions;

public interface IScreenFrameProvider : IDisposable
{
    string ProviderName { get; }

    bool IsSupported { get; }

    Task<ScreenReadResult<ScreenFrame>> CaptureFrameAsync(ScreenRect? region, ScreenReadOptions options);
}
