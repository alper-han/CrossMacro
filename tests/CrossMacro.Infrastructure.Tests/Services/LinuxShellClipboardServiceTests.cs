using System;
using System.Threading.Tasks;
using CrossMacro.Infrastructure.Services;
using NSubstitute;
using Xunit;

namespace CrossMacro.Infrastructure.Tests.Services;

public class LinuxShellClipboardServiceTests
{
    private readonly IProcessRunner _processRunner;
    private readonly LinuxShellClipboardService _service;

    public LinuxShellClipboardServiceTests()
    {
        _processRunner = Substitute.For<IProcessRunner>();
        _service = new LinuxShellClipboardService(_processRunner);
    }

    [Fact]
    public async Task InitializeAsync_DetectsWayland_WhenWlCopyExists()
    {
        // Arrange
        Environment.SetEnvironmentVariable("WAYLAND_DISPLAY", "wayland-0");
        _processRunner.CheckCommandAsync("wl-copy").Returns(Task.FromResult(true));

        // Act
        await _service.InitializeAsync();

        // Assert
        Assert.True(_service.IsSupported);
        await _processRunner.Received(1).CheckCommandAsync("wl-copy");
    }

    [Fact]
    public async Task SetTextAsync_UsesWlCopy_OnWayland()
    {
        // Arrange
        Environment.SetEnvironmentVariable("WAYLAND_DISPLAY", "wayland-0");
        _processRunner.CheckCommandAsync("wl-copy").Returns(Task.FromResult(true));
        await _service.InitializeAsync();

        // Act
        await _service.SetTextAsync("test");

        // Assert
        await _processRunner.Received(1).RunCommandAsync("wl-copy", Arg.Any<string>(), "test");
    }
    
    [Fact]
    public async Task SetTextAsync_UsesXclip_OnX11()
    {
        // Arrange
        Environment.SetEnvironmentVariable("WAYLAND_DISPLAY", null);
        _processRunner.CheckCommandAsync("xclip").Returns(Task.FromResult(true));
        // Force re-init if possible or just use new instance
        var service = new LinuxShellClipboardService(_processRunner);
        await service.InitializeAsync();

        // Act
        await service.SetTextAsync("test");

        // Assert
        await _processRunner.Received(1).RunCommandAsync("xclip", Arg.Any<string>(), "test");
    }
}
