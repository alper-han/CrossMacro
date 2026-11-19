namespace CrossMacro.Platform.Abstractions.Diagnostics;

/// <summary>Probes the Linux daemon and direct input access now, for diagnostics (doctor, MCP); never answers from a cache.</summary>
public interface ILinuxInputDiagnostics
{
    public ValueTask<LinuxInputDiagnosticSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default);
}
