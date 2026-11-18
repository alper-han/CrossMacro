using System.Runtime.InteropServices;

namespace CrossMacro.Daemon.Contracts.Ipc;

[StructLayout(LayoutKind.Auto)]
public readonly record struct IpcInputEvent
{
    public byte Type { get; init; }

    public int Code { get; init; }

    public int Value { get; init; }

    public long Timestamp { get; init; }
}
