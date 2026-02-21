using System;
using System.Collections.Generic;
using CrossMacro.Platform.Linux.Services;
using CrossMacro.TestInfrastructure;
using Xunit;

namespace CrossMacro.Platform.Linux.Tests.Services;

public sealed class LinuxDisplaySessionServiceTests
{
    [LinuxFact]
    public void IsSessionSupported_WhenNotFlatpak_ShouldReturnTrue()
    {
        using var env = new TemporaryEnvironment()
            .Set("FLATPAK_ID", null)
            .Set("XDG_SESSION_TYPE", "wayland")
            .Set("CROSSMACRO_USE_DAEMON", "0");

        var service = new LinuxDisplaySessionService(_ => false, _ => false);

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

        var service = new LinuxDisplaySessionService(_ => false, _ => false);

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

        var service = new LinuxDisplaySessionService(_ => true, _ => false);

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

        var service = new LinuxDisplaySessionService(_ => false, _ => false);

        var supported = service.IsSessionSupported(out var reason);

        Assert.False(supported);
        Assert.Contains("daemon socket is not accessible", reason, StringComparison.OrdinalIgnoreCase);
    }

    [LinuxFact]
    public void IsSessionSupported_WhenFlatpakWaylandDirectModeWithoutUInput_ShouldReturnFalse()
    {
        using var env = new TemporaryEnvironment()
            .Set("FLATPAK_ID", "io.github.alper_han.crossmacro")
            .Set("XDG_SESSION_TYPE", "wayland")
            .Set("CROSSMACRO_USE_DAEMON", "0");

        var service = new LinuxDisplaySessionService(_ => false, _ => false);

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

        var service = new LinuxDisplaySessionService(_ => false, _ => true);

        var supported = service.IsSessionSupported(out _);

        Assert.True(supported);
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
