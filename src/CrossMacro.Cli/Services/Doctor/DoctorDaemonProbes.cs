namespace CrossMacro.Cli.Services.Doctor;

/// <summary>The structured Linux daemon probes; the daemon checks run only when this is supplied.</summary>
internal sealed record DoctorDaemonProbes(
    Func<string, CancellationToken, ValueTask<LinuxDaemonSocketAccessResult>> SocketAccess,
    Func<string, TimeSpan, LinuxDaemonHandshakeProbeResult> Handshake);
