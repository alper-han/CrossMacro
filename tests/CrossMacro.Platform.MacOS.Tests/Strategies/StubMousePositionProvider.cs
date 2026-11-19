
namespace CrossMacro.Platform.MacOS.Tests.Strategies;

internal sealed class StubMousePositionProvider((int X, int Y)? position) : IMousePositionProvider
{
    public int AbsolutePositionQueries { get; private set; }

    public string ProviderName => "test";

    public bool IsSupported => true;

    public Task<(int X, int Y)?> GetAbsolutePositionAsync()
    {
        AbsolutePositionQueries++;
        return Task.FromResult(position);
    }

    public Task<(int Width, int Height)?> GetScreenResolutionAsync() => Task.FromResult<(int Width, int Height)?>(null);

    public void Dispose() { /* Test provider has no resources. */ }
}
