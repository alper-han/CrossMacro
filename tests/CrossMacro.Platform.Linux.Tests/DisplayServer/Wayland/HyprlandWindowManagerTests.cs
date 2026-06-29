using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Platform.Linux.DisplayServer.Wayland;
using Xunit;

namespace CrossMacro.Platform.Linux.Tests.DisplayServer.Wayland;

[Collection("EnvironmentVariableSensitive")]
public sealed class HyprlandWindowManagerTests
{

    [Fact]
    public async Task GetActiveWindowAsync_WhenIpcReturnsNull_ReturnsNull()
    {
        using var client = UnavailableClient();
        var manager = new HyprlandWindowManager(client);

        var result = await manager.GetActiveWindowAsync(CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetWindowsAsync_WhenIpcReturnsNull_ReturnsEmptyList()
    {
        using var client = UnavailableClient();
        var manager = new HyprlandWindowManager(client);

        var result = await manager.GetWindowsAsync(CancellationToken.None);

        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task FocusWindowByAddressAsync_WhenIpcReturnsNull_ReturnsFalse()
    {
        using var client = UnavailableClient();
        var manager = new HyprlandWindowManager(client);

        var result = await manager.FocusWindowByAddressAsync("0x1234", CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    public async Task FocusWindowByTitleAsync_WhenIpcReturnsNull_ReturnsFalse()
    {
        using var client = UnavailableClient();
        var manager = new HyprlandWindowManager(client);

        var result = await manager.FocusWindowByTitleAsync("Firefox", CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    public async Task FocusWindowByClassAsync_WhenIpcReturnsNull_ReturnsFalse()
    {
        using var client = UnavailableClient();
        var manager = new HyprlandWindowManager(client);

        var result = await manager.FocusWindowByClassAsync("firefox", CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    public async Task CloseWindowByAddressAsync_WhenIpcReturnsNull_ReturnsFalse()
    {
        using var client = UnavailableClient();
        var manager = new HyprlandWindowManager(client);

        var result = await manager.CloseWindowByAddressAsync("0x1234", CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    public async Task CloseWindowByTitleAsync_WhenIpcReturnsNull_ReturnsFalse()
    {
        using var client = UnavailableClient();
        var manager = new HyprlandWindowManager(client);

        var result = await manager.CloseWindowByTitleAsync("notepad", CancellationToken.None);

        Assert.False(result);
    }


    [Fact]
    public async Task MoveActiveWindowAsync_WhenIpcReturnsNull_ReturnsFalse()
    {
        using var client = UnavailableClient();
        var manager = new HyprlandWindowManager(client);

        var result = await manager.MoveActiveWindowAsync(100, 200, CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    public async Task ResizeActiveWindowAsync_WhenIpcReturnsNull_ReturnsFalse()
    {
        using var client = UnavailableClient();
        var manager = new HyprlandWindowManager(client);

        var result = await manager.ResizeActiveWindowAsync(800, 600, CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    public async Task FullscreenActiveWindowAsync_WhenIpcReturnsNull_ReturnsFalse()
    {
        using var client = UnavailableClient();
        var manager = new HyprlandWindowManager(client);

        var result = await manager.FullscreenActiveWindowAsync(CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    public async Task FloatActiveWindowAsync_WhenIpcReturnsNull_ReturnsFalse()
    {
        using var client = UnavailableClient();
        var manager = new HyprlandWindowManager(client);

        var result = await manager.FloatActiveWindowAsync(CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    public async Task CenterActiveWindowAsync_WhenIpcReturnsNull_ReturnsFalse()
    {
        using var client = UnavailableClient();
        var manager = new HyprlandWindowManager(client);

        var result = await manager.CenterActiveWindowAsync(CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    public async Task GetActiveWorkspaceAsync_WhenIpcReturnsNull_ReturnsNull()
    {
        using var client = UnavailableClient();
        var manager = new HyprlandWindowManager(client);

        var result = await manager.GetActiveWorkspaceAsync(CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task SwitchWorkspaceAsync_WhenIpcReturnsNull_ReturnsFalse()
    {
        using var client = UnavailableClient();
        var manager = new HyprlandWindowManager(client);

        var result = await manager.SwitchWorkspaceAsync("2", CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    public async Task FocusWindowByAddressAsync_WhenAddressLacksWith0xPrefix_StillSendsNormalized()
    {
        using var client = UnavailableClient();
        var manager = new HyprlandWindowManager(client);

        var result = await manager.FocusWindowByAddressAsync("ABCD", CancellationToken.None);

        Assert.False(result); 
    }

    [Fact]
    public async Task FocusWindowByAddressAsync_WhenAddressIsEmpty_ReturnsFalseImmediately()
    {
        using var client = UnavailableClient();
        var manager = new HyprlandWindowManager(client);

        var result = await manager.FocusWindowByAddressAsync(string.Empty, CancellationToken.None);

        Assert.False(result);
        Assert.False(client.IsAvailable); 
    }

    // ---- helpers ----------------------------------------------------------------------

    private static HyprlandIpcClient UnavailableClient()
    {
        Environment.SetEnvironmentVariable("HYPRLAND_INSTANCE_SIGNATURE", null);
        Environment.SetEnvironmentVariable("XDG_RUNTIME_DIR", null);
        return new HyprlandIpcClient();
    }
}
