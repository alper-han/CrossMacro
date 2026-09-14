namespace CrossMacro.Platform.Windows.Tests.Services;

public sealed class WindowsWindowBackendTests
{
    private static readonly string[] ExpectedFocusEvents = ["timeout:0", "attach:2", "attach:3", "restore", "focus", "detach:3", "detach:2", "timeout:250"];
    [Theory]
    [InlineData(false, false, "EDITOR", "11")]
    [InlineData(true, false, "TERMINAL", "12")]
    [InlineData(false, true, "EDITOR", "11")]
    public async Task Matching_UsesFirstCaseInsensitiveMatchAndDelegatesAddress(bool matchClass, bool close, string text, string expectedAddress)
    {
        var backend = new FakeBackend();
        var manager = new WindowsWindowManager(backend);
        Task<bool> operation;
        if (close) { operation = manager.CloseWindowByTitleAsync(text, CancellationToken.None); }
        else if (matchClass) { operation = manager.FocusWindowByClassAsync(text, CancellationToken.None); }
        else { operation = manager.FocusWindowByTitleAsync(text, CancellationToken.None); }
        var result = await operation;
        Assert.True(result);
        Assert.Equal(expectedAddress, backend.LastAddress);
        Assert.Equal(close, backend.Closed);
    }

    [Fact]
    public async Task EmptyMatch_DoesNotEnumerateOrAct()
    {
        var backend = new FakeBackend();
        var manager = new WindowsWindowManager(backend);
        Assert.False(await manager.FocusWindowByTitleAsync(" ", CancellationToken.None));
        Assert.Equal(0, backend.Enumerations);
        Assert.Null(backend.LastAddress);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Focus_AlwaysReleasesSuccessfulAttachesAndRestoresTimeout(bool failFocus)
    {
        var native = new FakeFocusNative { FailFocus = failFocus };
        var focus = new WindowsWindowFocus(native);
        if (failFocus) { _ = Assert.Throws<InvalidOperationException>(() => focus.Focus(new IntPtr(2))); }
        else { Assert.True(focus.Focus(new IntPtr(2))); }
        Assert.Equal(ExpectedFocusEvents, native.Events, StringComparer.Ordinal);
    }

    [Fact]
    public void Focus_PartialAttachFailureOnlyDetachesOwnedThreads()
    {
        var native = new FakeFocusNative { FailTargetAttach = true };
        Assert.True(new WindowsWindowFocus(native).Focus(new IntPtr(2)));
        Assert.DoesNotContain("detach:2", native.Events, StringComparer.Ordinal);
        Assert.Contains("detach:3", native.Events, StringComparer.Ordinal);
        Assert.Equal("timeout:250", native.Events[^1]);
    }

    private sealed class FakeFocusNative : IWindowsWindowFocusNative
    {
        public List<string> Events { get; } = [];
        public bool FailFocus { get; init; }
        public bool FailTargetAttach { get; init; }
        public bool IsWindow(IntPtr window) => true;
        public uint GetCurrentThreadId() => 1;
        public uint GetWindowThread(IntPtr window) => (uint)window.ToInt32();
        public IntPtr GetForegroundWindow() => new(3);
        public bool GetForegroundLockTimeout(out uint timeout) { timeout = 250; return true; }
        public bool SetForegroundLockTimeout(uint timeout) { Events.Add($"timeout:{timeout}"); return true; }
        public bool AllowSetForegroundWindow() => true;
        public bool UnlockForegroundWindow() => true;
        public bool AttachThreadInput(uint currentThread, uint targetThread, bool fAttach)
        {
            Events.Add($"{(fAttach ? "attach" : "detach")}:{targetThread}");
            return !fAttach || targetThread is not 2 || !FailTargetAttach;
        }
        public bool IsIconic(IntPtr window) => true;
        public bool RestoreWindow(IntPtr window) { Events.Add("restore"); return true; }
        public bool BringWindowToTop(IntPtr window) => true;
        public bool SetForegroundWindow(IntPtr window)
        {
            Events.Add("focus");
            if (FailFocus) { throw new InvalidOperationException("focus failed"); }
            return true;
        }
        public IntPtr SetActiveWindow(IntPtr window) => window;
        public IntPtr SetFocus(IntPtr window) => window;
    }

    private sealed class FakeBackend : IWindowsWindowBackend
    {
        public bool IsSupported => true;
        public string? LastAddress { get; private set; }
        public bool Closed { get; private set; }
        public int Enumerations { get; private set; }
        public Task<WindowInfo?> FindWindowAsync(Func<WindowInfo, bool> predicate, CancellationToken cancellationToken)
        {
            Enumerations++;
            WindowInfo[] windows = [
                new() { Address = "11", Title = "My Editor", Class = "Code" },
                new() { Address = "12", Title = "Editor second", Class = "Terminal" }];
            foreach (var window in windows) { if (predicate(window)) { return Task.FromResult<WindowInfo?>(window); } }
            return Task.FromResult<WindowInfo?>(null);
        }
        public Task<bool> FocusWindowByAddressAsync(string address, CancellationToken cancellationToken = default)
        { LastAddress = address; return Task.FromResult(true); }
        public Task<bool> CloseWindowByAddressAsync(string address, CancellationToken cancellationToken = default)
        { LastAddress = address; Closed = true; return Task.FromResult(true); }
        public Task<WindowInfo?> GetActiveWindowAsync(CancellationToken cancellationToken = default) => Task.FromResult<WindowInfo?>(null);
        public Task<IReadOnlyList<WindowInfo>> GetWindowsAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<WindowInfo>>([]);
        public Task<string?> GetActiveWorkspaceAsync(CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);
        public Task<bool> MoveActiveWindowAsync(int x, int y, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<bool> ResizeActiveWindowAsync(int width, int height, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<bool> FullscreenActiveWindowAsync(CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<bool> MaximizeActiveWindowAsync(CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<bool> FloatActiveWindowAsync(CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<bool> CenterActiveWindowAsync(CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<bool> SwitchWorkspaceAsync(string workspace, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<bool> MoveActiveWindowToWorkspaceAsync(string workspace, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<bool> MoveWindowToWorkspaceByAddressAsync(string address, string workspace, CancellationToken cancellationToken = default) => Task.FromResult(false);
    }
}
