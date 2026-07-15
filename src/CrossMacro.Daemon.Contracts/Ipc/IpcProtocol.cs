using System;
using System.Runtime.InteropServices;

namespace CrossMacro.Daemon.Contracts.Ipc;

public static class IpcProtocol
{
    public const int ProtocolVersion = 3;

    public const int MaxSimulationBatchEvents = 4096;

    public const int MaxSimulationBatchDelayMs = 1000;

    public const int MaxSimulationBatchTotalDelayMs = 5000;

    /// <summary>
    /// Canonical daemon socket path managed by systemd RuntimeDirectory.
    /// </summary>
    public const string DefaultSocketPath = "/run/crossmacro/crossmacro.sock";

}
