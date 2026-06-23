using System.Runtime.InteropServices;

namespace CrossMacro.Platform.Linux.DisplayServer.Wayland.PortalPipeWire;

[StructLayout(LayoutKind.Sequential)]
internal struct SpaList
{
    public IntPtr Next;
    public IntPtr Previous;
}

[StructLayout(LayoutKind.Sequential)]
internal struct SpaCallbacks
{
    public IntPtr Functions;
    public IntPtr Data;
}

[StructLayout(LayoutKind.Sequential)]
internal struct SpaHook
{
    public SpaList Link;
    public SpaCallbacks Callbacks;
    public IntPtr Removed;
    public IntPtr Private;
}

[StructLayout(LayoutKind.Sequential)]
internal struct PipeWireStreamEvents
{
    public uint Version;
    public IntPtr Destroy;
    public IntPtr StateChanged;
    public IntPtr ControlInfo;
    public IntPtr IoChanged;
    public IntPtr ParamChanged;
    public IntPtr AddBuffer;
    public IntPtr RemoveBuffer;
    public IntPtr Process;
    public IntPtr Drained;
    public IntPtr Command;
    public IntPtr TriggerDone;
}

[StructLayout(LayoutKind.Sequential)]
internal struct PipeWireBuffer
{
    public IntPtr Buffer;
    public IntPtr UserData;
    public ulong Size;
    public ulong Requested;
    public ulong Time;
}

[StructLayout(LayoutKind.Sequential)]
internal struct SpaBuffer
{
    public uint MetaCount;
    public uint DataCount;
    public IntPtr Metas;
    public IntPtr Datas;
}

[StructLayout(LayoutKind.Sequential)]
internal struct SpaData
{
    public uint Type;
    public uint Flags;
    public long Fd;
    public uint MapOffset;
    public uint MaxSize;
    public IntPtr Data;
    public IntPtr Chunk;
}

[StructLayout(LayoutKind.Sequential)]
internal struct SpaChunk
{
    public uint Offset;
    public uint Size;
    public int Stride;
    public int Flags;
}
