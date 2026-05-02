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

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct IpcInputEvent
{
    public byte Type;
    public int Code;
    public int Value;
    public long Timestamp;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct IpcSimulationRequest
{
    public ushort Type;
    public ushort Code;
    public int Value;
    public int DelayAfterMs;
}
