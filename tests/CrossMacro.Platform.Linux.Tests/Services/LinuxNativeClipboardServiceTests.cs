using CrossMacro.Platform.Linux.Clipboard;

namespace CrossMacro.Platform.Linux.Tests.Services;

public sealed class LinuxNativeClipboardServiceTests
{
    [Fact]
    public async Task GetPngAsync_AlwaysReportsExplicitImageReadUnavailabilityWithoutInitializingBackend()
    {
        using var service = new LinuxNativeClipboardService(new LinuxEnvironmentSnapshot(
            FlatpakId: null,
            AppImage: null,
            SessionType: "wayland",
            WaylandDisplay: "wayland-test",
            Display: null,
            CurrentDesktop: null,
            GdmSession: null,
            HyprlandInstanceSignature: null,
            RuntimeDir: null,
            WayfireSocket: null,
            SwaySocket: null,
            WindowButtons: null));

        var exception = await Assert.ThrowsAsync<ImageClipboardUnavailableException>(
            () => service.GetPngAsync(1024, CancellationToken.None));

        Assert.Contains("not supported", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(((IImageClipboardReader)service).IsSupported);
    }

    [Fact]
    public async Task GetPngAsync_WhenAlreadyCanceled_PropagatesCancellation()
    {
        using var service = new LinuxNativeClipboardService(new LinuxEnvironmentSnapshot(
            FlatpakId: null,
            AppImage: null,
            SessionType: "wayland",
            WaylandDisplay: "wayland-test",
            Display: null,
            CurrentDesktop: null,
            GdmSession: null,
            HyprlandInstanceSignature: null,
            RuntimeDir: null,
            WayfireSocket: null,
            SwaySocket: null,
            WindowButtons: null));
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.GetPngAsync(1024, cancellation.Token));
    }
}
