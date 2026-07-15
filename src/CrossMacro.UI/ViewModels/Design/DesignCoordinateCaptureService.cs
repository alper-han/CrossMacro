
namespace CrossMacro.UI.ViewModels;

internal sealed class DesignCoordinateCaptureService : ICoordinateCaptureService
{
    public bool IsCapturing => false;

    public Task<(int X, int Y)?> CaptureMousePositionAsync(CancellationToken ct = default) => Task.FromResult<(int X, int Y)?>((640, 360));

    public Task<int?> CaptureKeyCodeAsync(CancellationToken ct = default) => Task.FromResult<int?>(30);

    public void CancelCapture()
    {
    }
}
