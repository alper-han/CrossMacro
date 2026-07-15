using System.Runtime.InteropServices;

namespace CrossMacro.Platform.Linux.DisplayServer.Wayland.PortalPipeWire;

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
