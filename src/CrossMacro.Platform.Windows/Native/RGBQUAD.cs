
namespace CrossMacro.Platform.Windows.Native;

[StructLayout(LayoutKind.Sequential)]
internal struct RGBQUAD
{
    public byte rgbBlue;
    public byte rgbGreen;
    public byte rgbRed;
    public byte rgbReserved;
}
