using System;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Infrastructure.Services;
using CrossMacro.Platform.Linux.Clipboard;
using LinuxShellClipboardService = CrossMacro.Platform.Linux.Clipboard.LinuxShellClipboardService;
using NSubstitute;
using Xunit;

namespace CrossMacro.Infrastructure.Tests.Services;

[Collection("EnvironmentVariableSensitive")]
public class LinuxShellClipboardServiceTests
{
    private readonly CrossMacro.Infrastructure.Services.IProcessRunner _processRunner;
    private readonly LinuxShellClipboardService _service;

    public LinuxShellClipboardServiceTests()
    {
        _processRunner = Substitute.For<CrossMacro.Infrastructure.Services.IProcessRunner>();
        _service = new LinuxShellClipboardService(_processRunner);
    }

    [Fact]
    public async Task InitializeAsync_DetectsWayland_WhenWlCopyExists()
    {
        // Arrange
        var originalWaylandDisplay = Environment.GetEnvironmentVariable("WAYLAND_DISPLAY");
        try
        {
            Environment.SetEnvironmentVariable("WAYLAND_DISPLAY", "wayland-0");
            _processRunner.CheckCommandAsync("wl-copy", Arg.Any<CancellationToken>()).Returns(Task.FromResult(true));
            _processRunner.CheckCommandAsync("wl-paste", Arg.Any<CancellationToken>()).Returns(Task.FromResult(true));

            // Act
            await _service.InitializeAsync();

            // Assert
            Assert.True(_service.IsSupported);
            await _processRunner.Received(1).CheckCommandAsync("wl-copy", Arg.Any<CancellationToken>());
            await _processRunner.Received(1).CheckCommandAsync("wl-paste", Arg.Any<CancellationToken>());
        }
        finally
        {
            Environment.SetEnvironmentVariable("WAYLAND_DISPLAY", originalWaylandDisplay);
        }
    }

    [Fact]
    public async Task SetTextAsync_UsesWlCopy_OnWayland()
    {
        // Arrange
        var originalWaylandDisplay = Environment.GetEnvironmentVariable("WAYLAND_DISPLAY");
        try
        {
            Environment.SetEnvironmentVariable("WAYLAND_DISPLAY", "wayland-0");
            _processRunner.CheckCommandAsync("wl-copy", Arg.Any<CancellationToken>()).Returns(Task.FromResult(true));
            _processRunner.CheckCommandAsync("wl-paste", Arg.Any<CancellationToken>()).Returns(Task.FromResult(true));
            await _service.InitializeAsync();

            // Act
            await _service.SetTextAsync("test");

            // Assert
            await _processRunner.Received(1).WriteClipboardInputAndCloseAsync("wl-copy", Arg.Any<string>(), "test", Arg.Any<CancellationToken>());
        }
        finally
        {
            Environment.SetEnvironmentVariable("WAYLAND_DISPLAY", originalWaylandDisplay);
        }
    }

    [Fact]
    public async Task SetTextAsync_WhenTextIsEmptyOnWayland_ClearsWlClipboard()
    {
        var originalWaylandDisplay = Environment.GetEnvironmentVariable("WAYLAND_DISPLAY");
        try
        {
            Environment.SetEnvironmentVariable("WAYLAND_DISPLAY", "wayland-0");
            _processRunner.CheckCommandAsync("wl-copy", Arg.Any<CancellationToken>()).Returns(Task.FromResult(true));
            _processRunner.CheckCommandAsync("wl-paste", Arg.Any<CancellationToken>()).Returns(Task.FromResult(true));
            await _service.InitializeAsync();

            await _service.SetTextAsync(string.Empty);

            await _processRunner.Received(1).ExecuteCommandAsync("wl-copy", Arg.Is<string[]>(args => args.Length == 1 && args[0] == "--clear"), Arg.Any<CancellationToken>());
            await _processRunner.DidNotReceive().WriteClipboardInputAndCloseAsync("wl-copy", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        }
        finally
        {
            Environment.SetEnvironmentVariable("WAYLAND_DISPLAY", originalWaylandDisplay);
        }
    }

    [Fact]
    public async Task SetTextAsync_WhenToolCommandFails_Throws()
    {
        var originalWaylandDisplay = Environment.GetEnvironmentVariable("WAYLAND_DISPLAY");
        try
        {
            Environment.SetEnvironmentVariable("WAYLAND_DISPLAY", "wayland-0");
            _processRunner.CheckCommandAsync("wl-copy", Arg.Any<CancellationToken>()).Returns(Task.FromResult(true));
            _processRunner.CheckCommandAsync("wl-paste", Arg.Any<CancellationToken>()).Returns(Task.FromResult(true));
            _processRunner.WriteClipboardInputAndCloseAsync("wl-copy", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns<Task>(_ => throw new InvalidOperationException("wl-copy failed"));
            await _service.InitializeAsync();

            var act = async () => await _service.SetTextAsync("test");

            await Assert.ThrowsAsync<InvalidOperationException>(act);
        }
        finally
        {
            Environment.SetEnvironmentVariable("WAYLAND_DISPLAY", originalWaylandDisplay);
        }
    }

    [Fact]
    public async Task SetTextAsync_WhenNoClipboardToolAvailable_Throws()
    {
        var originalWaylandDisplay = Environment.GetEnvironmentVariable("WAYLAND_DISPLAY");
        try
        {
            Environment.SetEnvironmentVariable("WAYLAND_DISPLAY", null);
            var service = new LinuxShellClipboardService(_processRunner);

            var act = async () => await service.SetTextAsync("test");

            await Assert.ThrowsAsync<InvalidOperationException>(act);
            Assert.False(service.IsSupported);
        }
        finally
        {
            Environment.SetEnvironmentVariable("WAYLAND_DISPLAY", originalWaylandDisplay);
        }
    }
    
    [Fact]
    public async Task SetTextAsync_UsesXclip_OnX11()
    {
        // Arrange
        var originalWaylandDisplay = Environment.GetEnvironmentVariable("WAYLAND_DISPLAY");
        try
        {
            Environment.SetEnvironmentVariable("WAYLAND_DISPLAY", null);
            _processRunner.CheckCommandAsync("xclip", Arg.Any<CancellationToken>()).Returns(Task.FromResult(true));
            // Force re-init if possible or just use new instance
            var service = new LinuxShellClipboardService(_processRunner);
            await service.InitializeAsync();

            // Act
            await service.SetTextAsync("test");

            // Assert
            await _processRunner.Received(1).WriteClipboardInputAndCloseAsync("xclip", Arg.Any<string>(), "test", Arg.Any<CancellationToken>());
        }
        finally
        {
            Environment.SetEnvironmentVariable("WAYLAND_DISPLAY", originalWaylandDisplay);
        }
    }

    [Fact]
    public async Task InitializeAsync_WhenWaylandWlPasteMissing_FallsBackToXclip()
    {
        var originalWaylandDisplay = Environment.GetEnvironmentVariable("WAYLAND_DISPLAY");
        try
        {
            Environment.SetEnvironmentVariable("WAYLAND_DISPLAY", "wayland-0");
            _processRunner.CheckCommandAsync("wl-copy", Arg.Any<CancellationToken>()).Returns(Task.FromResult(true));
            _processRunner.CheckCommandAsync("wl-paste", Arg.Any<CancellationToken>()).Returns(Task.FromResult(false));
            _processRunner.CheckCommandAsync("xclip", Arg.Any<CancellationToken>()).Returns(Task.FromResult(true));
            var service = new LinuxShellClipboardService(_processRunner);

            await service.InitializeAsync();
            await service.SetTextAsync("test");

            await _processRunner.Received(1).WriteClipboardInputAndCloseAsync("xclip", Arg.Any<string>(), "test", Arg.Any<CancellationToken>());
        }
        finally
        {
            Environment.SetEnvironmentVariable("WAYLAND_DISPLAY", originalWaylandDisplay);
        }
    }

    [Fact]
    public async Task GetTextAsync_WhenWaylandClipboardIsEmpty_ReturnsEmptyString()
    {
        var originalWaylandDisplay = Environment.GetEnvironmentVariable("WAYLAND_DISPLAY");
        try
        {
            Environment.SetEnvironmentVariable("WAYLAND_DISPLAY", "wayland-0");
            _processRunner.CheckCommandAsync("wl-copy", Arg.Any<CancellationToken>()).Returns(Task.FromResult(true));
            _processRunner.CheckCommandAsync("wl-paste", Arg.Any<CancellationToken>()).Returns(Task.FromResult(true));
            _processRunner.ReadCommandAsync("wl-paste", Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns<Task<string>>(_ => throw new InvalidOperationException("Command 'wl-paste' exited with code 1: Nothing is copied"));
            await _service.InitializeAsync();

            var result = await _service.GetTextAsync();

            Assert.Equal(string.Empty, result);
        }
        finally
        {
            Environment.SetEnvironmentVariable("WAYLAND_DISPLAY", originalWaylandDisplay);
        }
    }

}
