namespace CrossMacro.Infrastructure.Tests.Services;

using System;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Infrastructure.Services;
using NSubstitute;

[Collection("EnvironmentVariableSensitive")]
public sealed class LinuxShellImageClipboardServiceTests
{
    [Fact]
    public async Task SetPngAsync_UsesWlCopyImagePng_OnWayland()
    {
        var originalWaylandDisplay = Environment.GetEnvironmentVariable("WAYLAND_DISPLAY");
        try
        {
            Environment.SetEnvironmentVariable("WAYLAND_DISPLAY", "wayland-0");
            var runner = Substitute.For<IProcessRunner>();
            runner.CheckCommandAsync("wl-copy", Arg.Any<CancellationToken>()).Returns(Task.FromResult(true));
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
            Environment.SetEnvironmentVariable("WAYLAND_DISPLAY", null);
            var runner = Substitute.For<IProcessRunner>();
            runner.CheckCommandAsync("xclip", Arg.Any<CancellationToken>()).Returns(Task.FromResult(true));
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
