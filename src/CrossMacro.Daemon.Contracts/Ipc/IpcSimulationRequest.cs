using System.Runtime.InteropServices;

namespace CrossMacro.Daemon.Contracts.Ipc;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct IpcSimulationRequest
{
    public ushort Type;
    public ushort Code;
    public int Value;
    public int DelayAfterMs;
}
