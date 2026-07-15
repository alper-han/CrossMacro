
namespace CrossMacro.UI.ViewModels;

internal sealed class DesignMousePositionProvider : IMousePositionProvider
{
    public string ProviderName => "Design Preview";

    public bool IsSupported => true;

    public Task<(int X, int Y)?> GetAbsolutePositionAsync() => Task.FromResult<(int X, int Y)?>((640, 360));

    public Task<(int Width, int Height)?> GetScreenResolutionAsync() => Task.FromResult<(int Width, int Height)?>((1920, 1080));

    public void Dispose()
    {
    }
}
