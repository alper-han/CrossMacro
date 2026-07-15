using System.Runtime.InteropServices;

namespace CrossMacro.Daemon.Contracts.Ipc;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct IpcInputEvent
{
    public byte Type;
    public int Code;
    public int Value;
    public long Timestamp;
}
