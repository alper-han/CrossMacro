
namespace CrossMacro.Platform.Windows.Native;

[StructLayout(LayoutKind.Sequential)]
internal struct RgbQuad
{
    public byte rgbBlue;
    public byte rgbGreen;
    public byte rgbRed;
    public byte rgbReserved;
}
