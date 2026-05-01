using System;
using System.Runtime.InteropServices;

namespace CrossMacro.Daemon.Contracts.Ipc;

public static class IpcProtocol
{
    public const int ProtocolVersion = 2;

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
}
