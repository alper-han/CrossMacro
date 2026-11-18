namespace CrossMacro.Platform.Linux.Tests.DisplayServer.Wayland;

/// <summary>Command formatting and window matching are covered by <see cref="HyprlandWindowManagerSocketTests"/>.</summary>
public sealed class HyprlandWindowManagerTests
{
    [Fact]
    public async Task WhenHyprlandIsUnavailable_EveryOperationFailsWithoutThrowing()
    {
        using var client = new HyprlandIpcClient(default);
        var manager = new HyprlandWindowManager(client);
        var token = CancellationToken.None;

        Assert.False(client.IsAvailable);
        Assert.Null(await manager.GetActiveWindowAsync(token));
        Assert.Empty(await manager.GetWindowsAsync(token));
        Assert.Null(await manager.GetActiveWorkspaceAsync(token));

        Assert.All(
            [
                await manager.FocusWindowByAddressAsync("0x1234", token),
                await manager.FocusWindowByTitleAsync("Firefox", token),
                await manager.FocusWindowByClassAsync("firefox", token),
                await manager.CloseWindowByAddressAsync("0x1234", token),
                await manager.CloseWindowByTitleAsync("notepad", token),
                await manager.MoveActiveWindowAsync(100, 200, token),
                await manager.ResizeActiveWindowAsync(800, 600, token),
                await manager.FullscreenActiveWindowAsync(token),
                await manager.FloatActiveWindowAsync(token),
                await manager.CenterActiveWindowAsync(token),
                await manager.SwitchWorkspaceAsync("2", token),
            ],
            Assert.False);
    }
}
