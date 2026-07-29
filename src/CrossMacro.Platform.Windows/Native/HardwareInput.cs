
namespace CrossMacro.Platform.Windows.Native;

[StructLayout(LayoutKind.Sequential)]
internal struct HardwareInput
{
    public uint uMsg;
    public ushort wParamL;
    public ushort wParamH;
}
