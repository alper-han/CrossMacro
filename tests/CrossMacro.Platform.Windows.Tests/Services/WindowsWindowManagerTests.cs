namespace CrossMacro.Platform.Windows.Tests.Services;

public sealed class WindowsWindowManagerTests
{
    [Fact]
    public async Task AllOperations_WhenCancellationIsAlreadyRequested_CancelBeforeNativeOrUnsupportedPath()
    {
        var manager = new WindowsWindowManager();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var operations = new Func<Task>[]
        {
            () => manager.GetActiveWindowAsync(cancellation.Token),
            () => manager.GetWindowsAsync(cancellation.Token),
            () => manager.FocusWindowByAddressAsync("0x1", cancellation.Token),
            () => manager.FocusWindowByTitleAsync("title", cancellation.Token),
            () => manager.FocusWindowByClassAsync("class", cancellation.Token),
            () => manager.CloseWindowByAddressAsync("0x1", cancellation.Token),
            () => manager.CloseWindowByTitleAsync("title", cancellation.Token),
            () => manager.MoveActiveWindowAsync(1, 2, cancellation.Token),
            () => manager.ResizeActiveWindowAsync(3, 4, cancellation.Token),
            () => manager.FullscreenActiveWindowAsync(cancellation.Token),
            () => manager.MaximizeActiveWindowAsync(cancellation.Token),
            () => manager.FloatActiveWindowAsync(cancellation.Token),
            () => manager.CenterActiveWindowAsync(cancellation.Token),
            () => manager.GetActiveWorkspaceAsync(cancellation.Token),
            () => manager.SwitchWorkspaceAsync("workspace", cancellation.Token),
            () => manager.MoveActiveWindowToWorkspaceAsync("workspace", cancellation.Token),
            () => manager.MoveWindowToWorkspaceByAddressAsync("0x1", "workspace", cancellation.Token),
        };

        foreach (var operation in operations)
        {
            _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(operation);
        }
    }
}
