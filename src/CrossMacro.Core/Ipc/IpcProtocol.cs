using System;
using System.Runtime.InteropServices;

namespace CrossMacro.Core.Ipc;

public static class IpcProtocol
{
    public const int ProtocolVersion = 1;
    
    /// <summary>
    /// Primary socket path managed by systemd RuntimeDirectory.
    /// Falls back to /tmp for systems without proper systemd setup.
    /// </summary>
    public const string DefaultSocketPath = "/run/crossmacro/crossmacro.sock";
    
    /// <summary>
    /// Fallback socket path for portable/AppImage deployments.
    /// </summary>
    public const string FallbackSocketPath = "/tmp/crossmacro.sock";
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
