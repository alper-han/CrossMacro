
namespace CrossMacro.Platform.Linux.Tests.Services;

public sealed class LinuxDaemonHandshakeProbeTests
{
    private const string SocketPath = "/run/crossmacro/crossmacro.sock";

    [Fact]
    public void Probe_ReturnsTheTransportResultForTheRequestedBudget()
    {
        var timeout = TimeSpan.FromSeconds(3);
        string? probedPath = null;
        TimeSpan? probedTimeout = null;
        var probe = new LinuxDaemonHandshakeProbe((path, budget, _) =>
        {
            probedPath = path;
            probedTimeout = budget;
            return Task.FromResult(LinuxDaemonHandshakeProbeResult.Success(path, budget));
        });

        var result = probe.Probe(SocketPath, timeout);

        Assert.True(result.Succeeded);
        Assert.Equal(SocketPath, probedPath);
        Assert.Equal(timeout, probedTimeout);
    }

    [Fact]
    public void Probe_WithoutBudget_UsesTwoSecondDefault()
    {
        TimeSpan? probedTimeout = null;
        var probe = new LinuxDaemonHandshakeProbe((path, budget, _) =>
        {
            probedTimeout = budget;
            return Task.FromResult(LinuxDaemonHandshakeProbeResult.Failed(path, budget, LinuxDaemonHandshakeStatus.Timeout));
        });

        Assert.False(probe.Probe(SocketPath));
        Assert.Equal(TimeSpan.FromSeconds(2), probedTimeout);
    }
}
