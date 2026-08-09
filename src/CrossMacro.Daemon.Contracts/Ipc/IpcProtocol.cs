
namespace CrossMacro.Daemon.Contracts.Ipc;

public static class IpcProtocol
{
    // Version 4 includes the decoded event count in simulation acknowledgements.
    public const int ProtocolVersion = 4;

    public const int MaxSimulationBatchEvents = 4096;

    public const long MaxSimulationBatchDelayMicroseconds = 1_000_000;

    public const long MaxSimulationBatchTotalDelayMicroseconds = 5_000_000;

    /// <summary>
    /// Canonical daemon socket path managed by systemd RuntimeDirectory.
    /// </summary>
    public const string DefaultSocketPath = "/run/crossmacro/crossmacro.sock";

}
