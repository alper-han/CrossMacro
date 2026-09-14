namespace CrossMacro.Platform.Windows.Services.WindowManagement;

internal interface IWindowsWindowBackend : IWindowQueryService, IWorkspaceManagementService
{
    public bool IsSupported { get; }
    public Task<WindowInfo?> FindWindowAsync(Func<WindowInfo, bool> predicate, CancellationToken cancellationToken);
    public Task<bool> FocusWindowByAddressAsync(string address, CancellationToken cancellationToken = default);
    public Task<bool> CloseWindowByAddressAsync(string address, CancellationToken cancellationToken = default);
    public Task<bool> MoveActiveWindowAsync(int x, int y, CancellationToken cancellationToken = default);
    public Task<bool> ResizeActiveWindowAsync(int width, int height, CancellationToken cancellationToken = default);
    public Task<bool> FullscreenActiveWindowAsync(CancellationToken cancellationToken = default);
    public Task<bool> MaximizeActiveWindowAsync(CancellationToken cancellationToken = default);
    public Task<bool> FloatActiveWindowAsync(CancellationToken cancellationToken = default);
    public Task<bool> CenterActiveWindowAsync(CancellationToken cancellationToken = default);
}
