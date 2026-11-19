using System.Runtime.InteropServices;

namespace CrossMacro.Daemon.Contracts.Ipc;

[StructLayout(LayoutKind.Auto)]
public readonly record struct IpcSimulationRequest
{
    public ushort Type { get; init; }

    public ushort Code { get; init; }

    public int Value { get; init; }

    public long DelayAfterMicroseconds { get; init; }
}
