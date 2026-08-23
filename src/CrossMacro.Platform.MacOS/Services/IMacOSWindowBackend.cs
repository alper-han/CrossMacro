namespace CrossMacro.Platform.MacOS.Services;

internal interface IMacOSWindowBackend : IDisposable
{
    public bool IsAvailable { get; }

    public WindowInfo? GetActiveWindow();

    public IReadOnlyList<WindowInfo> GetWindows();

    public bool Focus(string address);

    public bool Close(string address);

    public bool SetPosition(string address, int x, int y);

    public bool SetSize(string address, int width, int height);

    public bool Zoom(string address);

    public bool ToggleFullscreen(string address);

    public ScreenRect? GetContainingDisplayBounds(string address);
}
