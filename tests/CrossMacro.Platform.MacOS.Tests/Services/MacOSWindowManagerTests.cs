namespace CrossMacro.Platform.MacOS.Tests.Services;

public sealed class MacOSWindowManagerTests
{
    [Fact]
    public async Task WindowOperations_DelegateToAccessibilityBackend()
    {
        var active = CreateWindow("active", "CrossMacro", "CrossMacro", 100, 200, 800, 600);
        var backend = new FakeWindowBackend
        {
            ActiveWindow = active,
            Windows = [active],
            ContainingDisplayBounds = new ScreenRect(-100, 50, 2000, 1000),
        };
        using var manager = new MacOSWindowManager(backend, isMacOS: () => true);

        Assert.True(await manager.FocusWindowByAddressAsync(active.Address, CancellationToken.None));
        Assert.True(await manager.FocusWindowByTitleAsync("cross", CancellationToken.None));
        Assert.True(await manager.FocusWindowByClassAsync("macro", CancellationToken.None));
        Assert.True(await manager.CloseWindowByTitleAsync("Cross", CancellationToken.None));
        Assert.True(await manager.MoveActiveWindowAsync(10, 20, CancellationToken.None));
        Assert.True(await manager.ResizeActiveWindowAsync(640, 480, CancellationToken.None));
        Assert.True(await manager.CenterActiveWindowAsync(CancellationToken.None));
        Assert.True(await manager.MaximizeActiveWindowAsync(CancellationToken.None));
        Assert.True(await manager.FullscreenActiveWindowAsync(CancellationToken.None));

        Assert.Equal([active.Address, active.Address, active.Address], backend.FocusedAddresses);
        Assert.Equal([active.Address], backend.ClosedAddresses);
        Assert.Equal([(10, 20), (500, 250)], backend.Positions);
        Assert.Equal([(640, 480)], backend.Sizes);
        Assert.Equal([active.Address], backend.ZoomedAddresses);
        Assert.Equal([active.Address], backend.FullscreenAddresses);
    }

    [Fact]
    public async Task InvalidAndUnsupportedOperations_ReturnFalseWithoutCallingBackend()
    {
        var backend = new FakeWindowBackend();
        using var manager = new MacOSWindowManager(backend, isMacOS: () => true);

        Assert.False(await manager.FocusWindowByAddressAsync(string.Empty, CancellationToken.None));
        Assert.False(await manager.FocusWindowByTitleAsync(" ", CancellationToken.None));
        Assert.False(await manager.FocusWindowByClassAsync(" ", CancellationToken.None));
        Assert.False(await manager.CloseWindowByTitleAsync(" ", CancellationToken.None));
        Assert.False(await manager.ResizeActiveWindowAsync(0, 480, CancellationToken.None));
        Assert.False(await manager.CenterActiveWindowAsync(CancellationToken.None));
        Assert.False(await manager.FloatActiveWindowAsync(CancellationToken.None));
        Assert.Null(await manager.GetActiveWorkspaceAsync(CancellationToken.None));
        Assert.False(await manager.SwitchWorkspaceAsync("1", CancellationToken.None));
        Assert.False(await manager.MoveActiveWindowToWorkspaceAsync("1", CancellationToken.None));
        Assert.False(await manager.MoveWindowToWorkspaceByAddressAsync("active", "1", CancellationToken.None));
        Assert.Empty(backend.FocusedAddresses);
        Assert.Empty(backend.ClosedAddresses);
    }

    [Fact]
    public async Task Operations_WhenUnavailable_ReturnNeutralResults()
    {
        var backend = new FakeWindowBackend { IsAvailable = false };
        using var manager = new MacOSWindowManager(backend, isMacOS: () => true);

        Assert.False(manager.IsSupported);
        Assert.Null(await manager.GetActiveWindowAsync(CancellationToken.None));
        Assert.Empty(await manager.GetWindowsAsync(CancellationToken.None));
        Assert.False(await manager.FocusWindowByAddressAsync("active", CancellationToken.None));
    }

    private static WindowInfo CreateWindow(string address, string title, string @class, int x, int y, int width, int height) => new()
    {
        Address = address,
        Title = title,
        Class = @class,
        Pid = 123,
        X = x,
        Y = y,
        Width = width,
        Height = height,
    };

    private sealed class FakeWindowBackend : IMacOSWindowBackend
    {
        public bool IsAvailable { get; set; } = true;
        public WindowInfo? ActiveWindow { get; set; }
        public IReadOnlyList<WindowInfo> Windows { get; set; } = [];
        public List<string> FocusedAddresses { get; } = [];
        public List<string> ClosedAddresses { get; } = [];
        public List<(int X, int Y)> Positions { get; } = [];
        public List<(int Width, int Height)> Sizes { get; } = [];
        public List<string> ZoomedAddresses { get; } = [];
        public List<string> FullscreenAddresses { get; } = [];
        public ScreenRect? ContainingDisplayBounds { get; set; }

        public WindowInfo? GetActiveWindow() => ActiveWindow;
        public IReadOnlyList<WindowInfo> GetWindows() => Windows;
        public bool Focus(string address)
        {
            FocusedAddresses.Add(address);
            return true;
        }

        public bool Close(string address)
        {
            ClosedAddresses.Add(address);
            return true;
        }

        public bool SetPosition(string address, int x, int y)
        {
            Positions.Add((x, y));
            return true;
        }

        public bool SetSize(string address, int width, int height)
        {
            Sizes.Add((width, height));
            return true;
        }

        public bool Zoom(string address)
        {
            ZoomedAddresses.Add(address);
            return true;
        }

        public bool ToggleFullscreen(string address)
        {
            FullscreenAddresses.Add(address);
            return true;
        }

        public ScreenRect? GetContainingDisplayBounds(string address) => ContainingDisplayBounds;

        public void Dispose() { }
    }
}
