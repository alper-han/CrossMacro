namespace CrossMacro.Infrastructure.Tests.Services;


[Collection("EnvironmentVariableSensitive")]
[System.Runtime.Versioning.SupportedOSPlatform("linux")]
public sealed class LinuxShellImageClipboardServiceTests
{
    [Fact]
    public async Task SetPngAsync_UsesWlCopyImagePng_OnWayland()
    {
        var originalWaylandDisplay = Environment.GetEnvironmentVariable("WAYLAND_DISPLAY");
        try
        {
            Environment.SetEnvironmentVariable("WAYLAND_DISPLAY", "wayland-0");
            var runner = Substitute.For<CrossMacro.Platform.Abstractions.IProcessRunner>();
            _ = runner.CheckCommandAsync("wl-copy", Arg.Any<CancellationToken>()).Returns(Task.FromResult(true));
            var service = new LinuxShellImageClipboardService(runner);
            byte[] png = [1, 2, 3];

            await service.SetPngAsync(png);

            await runner.Received(1).WriteClipboardInputAndCloseAsync(
                "wl-copy",
                Arg.Is<string[]>(args => args.Length == 2 && args[0] == "--type" && args[1] == "image/png"),
                Arg.Any<ReadOnlyMemory<byte>>(),
                Arg.Any<CancellationToken>());
        }
        finally
        {
            Environment.SetEnvironmentVariable("WAYLAND_DISPLAY", originalWaylandDisplay);
        }
    }

    [Fact]
    public async Task SetPngAsync_UsesXclipImagePng_OnX11()
    {
        var originalWaylandDisplay = Environment.GetEnvironmentVariable("WAYLAND_DISPLAY");
        try
        {
            Environment.SetEnvironmentVariable("WAYLAND_DISPLAY", value: null);
            var runner = Substitute.For<CrossMacro.Platform.Abstractions.IProcessRunner>();
            _ = runner.CheckCommandAsync("xclip", Arg.Any<CancellationToken>()).Returns(Task.FromResult(true));
            var service = new LinuxShellImageClipboardService(runner);

            byte[] png = [1, 2, 3];

            await service.SetPngAsync(png);

            await runner.Received(1).WriteClipboardInputAndCloseAsync(
                "xclip",
                Arg.Is<string[]>(args => string.Join(' ', args) == "-selection clipboard -t image/png -i"),
                Arg.Any<ReadOnlyMemory<byte>>(),
                Arg.Any<CancellationToken>());
        }
        finally
        {
            Environment.SetEnvironmentVariable("WAYLAND_DISPLAY", originalWaylandDisplay);
        }
    }
}
