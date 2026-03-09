using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using CrossMacro.Platform.Linux;
using CrossMacro.Platform.Linux.Services;
using CrossMacro.TestInfrastructure;
using Xunit;

namespace CrossMacro.Platform.Linux.Tests.Services;

[Collection("EnvironmentVariableSensitive")]
public sealed class LinuxDisplaySessionServiceTests
{
    [LinuxFact]
    public void IsSessionSupported_WhenNotFlatpak_ShouldReturnTrue()
    {
        using var env = new TemporaryEnvironment()
            .Set("FLATPAK_ID", null)
            .Set("XDG_SESSION_TYPE", "wayland")
            .Set("CROSSMACRO_USE_DAEMON", "0");

        var service = CreateService(
            fileExists: _ => false,
            canOpenForWrite: _ => false,
            canOpenForRead: _ => false,
            daemonHandshakeProbe: _ => false,
            getInputEventCandidates: () => []);

        var supported = service.IsSessionSupported(out var reason);

        Assert.True(supported);
        Assert.Equal(string.Empty, reason);
    }

    [LinuxFact]
    public void IsSessionSupported_WhenFlatpakAndUnsupportedSession_ShouldReturnFalseWithReason()
    {
        using var env = new TemporaryEnvironment()
            .Set("FLATPAK_ID", "io.github.alper_han.crossmacro")
            .Set("XDG_SESSION_TYPE", "tty")
            .Set("CROSSMACRO_USE_DAEMON", "0");

        var service = CreateService(
            fileExists: _ => false,
            canOpenForWrite: _ => false,
            canOpenForRead: _ => false,
            daemonHandshakeProbe: _ => false,
            getInputEventCandidates: () => []);

        var supported = service.IsSessionSupported(out var reason);

        Assert.False(supported);
        Assert.Contains("Unsupported Flatpak session", reason, StringComparison.Ordinal);
    }

    [LinuxFact]
    public void IsSessionSupported_WhenFlatpakWaylandWithDaemonSocket_ShouldReturnTrue()
    {
        using var env = new TemporaryEnvironment()
            .Set("FLATPAK_ID", "io.github.alper_han.crossmacro")
            .Set("XDG_SESSION_TYPE", "wayland")
            .Set("CROSSMACRO_USE_DAEMON", "1");

        var service = CreateService(
            fileExists: _ => true,
            canOpenForWrite: _ => false,
            canOpenForRead: _ => false,
            daemonHandshakeProbe: _ => true,
            getInputEventCandidates: () => []);

        var supported = service.IsSessionSupported(out _);

        Assert.True(supported);
    }

    [LinuxFact]
    public void IsSessionSupported_WhenFlatpakWaylandDaemonModeWithoutSocket_ShouldReturnFalse()
    {
        using var env = new TemporaryEnvironment()
            .Set("FLATPAK_ID", "io.github.alper_han.crossmacro")
            .Set("XDG_SESSION_TYPE", "wayland")
            .Set("CROSSMACRO_USE_DAEMON", "1");

        var service = CreateService(
            fileExists: _ => false,
            canOpenForWrite: _ => false,
            canOpenForRead: _ => false,
            daemonHandshakeProbe: _ => false,
            getInputEventCandidates: () => []);

        var supported = service.IsSessionSupported(out var reason);

        Assert.False(supported);
        Assert.Contains("handshake failed", reason, StringComparison.OrdinalIgnoreCase);
    }

    [LinuxFact]
    public void IsSessionSupported_WhenFlatpakWaylandDirectModeWithoutUInput_ShouldReturnFalse()
    {
        using var env = new TemporaryEnvironment()
            .Set("FLATPAK_ID", "io.github.alper_han.crossmacro")
            .Set("XDG_SESSION_TYPE", "wayland")
            .Set("CROSSMACRO_USE_DAEMON", "0");

        var service = CreateService(
            fileExists: _ => false,
            canOpenForWrite: _ => false,
            canOpenForRead: _ => true,
            daemonHandshakeProbe: _ => false,
            getInputEventCandidates: () => ["/dev/input/event0"]);

        var supported = service.IsSessionSupported(out var reason);

        Assert.False(supported);
        Assert.Contains("/dev/uinput", reason, StringComparison.Ordinal);
    }

    [LinuxFact]
    public void IsSessionSupported_WhenFlatpakWaylandDirectModeWithUInput_ShouldReturnTrue()
    {
        using var env = new TemporaryEnvironment()
            .Set("FLATPAK_ID", "io.github.alper_han.crossmacro")
            .Set("XDG_SESSION_TYPE", "wayland")
            .Set("CROSSMACRO_USE_DAEMON", "0");

        var service = CreateService(
            fileExists: _ => false,
            canOpenForWrite: path => path == LinuxConstants.UInputDevicePath,
            canOpenForRead: path => path == "/dev/input/event0",
            daemonHandshakeProbe: _ => false,
            getInputEventCandidates: () => ["/dev/input/event0"]);

        var supported = service.IsSessionSupported(out _);

        Assert.True(supported);
    }

    [LinuxFact]
    public void IsSessionSupported_WhenFlatpakWaylandDirectModeWithUInputButNoEventRead_ShouldReturnFalse()
    {
        using var env = new TemporaryEnvironment()
            .Set("FLATPAK_ID", "io.github.alper_han.crossmacro")
            .Set("XDG_SESSION_TYPE", "wayland")
            .Set("CROSSMACRO_USE_DAEMON", "0");

        var service = CreateService(
            fileExists: _ => false,
            canOpenForWrite: path => path == LinuxConstants.UInputDevicePath,
            canOpenForRead: _ => false,
            daemonHandshakeProbe: _ => false,
            getInputEventCandidates: () => ["/dev/input/event0"]);

        var supported = service.IsSessionSupported(out var reason);

        Assert.False(supported);
        Assert.Contains("/dev/input/event*", reason, StringComparison.Ordinal);
    }

    [LinuxFact]
    public void IsSessionSupported_WhenFlatpakWaylandDaemonSocketExistsButHandshakeFails_ShouldReturnFalse()
    {
        using var env = new TemporaryEnvironment()
            .Set("FLATPAK_ID", "io.github.alper_han.crossmacro")
            .Set("XDG_SESSION_TYPE", "wayland")
            .Set("CROSSMACRO_USE_DAEMON", "1");

        var service = CreateService(
            fileExists: _ => true,
            canOpenForWrite: _ => false,
            canOpenForRead: _ => false,
            daemonHandshakeProbe: _ => false,
            getInputEventCandidates: () => []);

        var supported = service.IsSessionSupported(out var reason);

        Assert.False(supported);
        Assert.Contains("handshake failed", reason, StringComparison.OrdinalIgnoreCase);
    }

    [LinuxFact]
    public void IsSessionSupported_WhenFlatpakWaylandDaemonHandshakeFailsButDirectReady_ShouldReturnTrue()
    {
        using var env = new TemporaryEnvironment()
            .Set("FLATPAK_ID", "io.github.alper_han.crossmacro")
            .Set("XDG_SESSION_TYPE", "wayland")
            .Set("CROSSMACRO_USE_DAEMON", "1");

        var service = CreateService(
            fileExists: _ => true,
            canOpenForWrite: path => path == LinuxConstants.UInputDevicePath,
            canOpenForRead: path => path == "/dev/input/event0",
            daemonHandshakeProbe: _ => false,
            getInputEventCandidates: () => ["/dev/input/event0"]);

        var supported = service.IsSessionSupported(out var reason);

        Assert.True(supported);
        Assert.Equal(string.Empty, reason);
    }

    [LinuxFact]
    public void IsSessionSupported_WhenDaemonHandshakeProbeReturnsAfterDelay_ShouldWaitForProbeResult()
    {
        using var env = new TemporaryEnvironment()
            .Set("FLATPAK_ID", "io.github.alper_han.crossmacro")
            .Set("XDG_SESSION_TYPE", "wayland")
            .Set("CROSSMACRO_USE_DAEMON", "1");

        var service = CreateService(
            fileExists: _ => true,
            canOpenForWrite: _ => false,
            canOpenForRead: _ => false,
            daemonHandshakeProbe: _ =>
            {
                Thread.Sleep(250);
                return false;
            },
            getInputEventCandidates: () => []);

        var sw = Stopwatch.StartNew();
        var supported = service.IsSessionSupported(out var reason);
        sw.Stop();

        Assert.False(supported);
        Assert.Contains("handshake failed", reason, StringComparison.OrdinalIgnoreCase);
        Assert.True(sw.Elapsed >= TimeSpan.FromMilliseconds(200), $"Expected to wait for delayed probe result, elapsed: {sw.Elapsed}");
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(2), $"Expected probe to finish before timeout budget, elapsed: {sw.Elapsed}");
    }

    private static LinuxDisplaySessionService CreateService(
        Func<string, bool> fileExists,
        Func<string, bool> canOpenForWrite,
        Func<string, bool> canOpenForRead,
        Func<string, bool> daemonHandshakeProbe,
        Func<string[]> getInputEventCandidates)
    {
        return new LinuxDisplaySessionService(
            fileExists,
            canOpenForWrite,
            canOpenForRead,
            daemonHandshakeProbe,
            getInputEventCandidates);
    }

    private sealed class TemporaryEnvironment : IDisposable
    {
        private readonly Dictionary<string, string?> _originalValues = new(StringComparer.Ordinal);

        public TemporaryEnvironment Set(string key, string? value)
        {
            if (!_originalValues.ContainsKey(key))
            {
                _originalValues[key] = Environment.GetEnvironmentVariable(key);
            }

            Environment.SetEnvironmentVariable(key, value);
            return this;
        }

        public void Dispose()
        {
            foreach (var pair in _originalValues)
            {
                Environment.SetEnvironmentVariable(pair.Key, pair.Value);
            }
        }
    }
}
