
namespace CrossMacro.Platform.Linux.Native.X11
{
    [StructLayout(LayoutKind.Sequential)]
    public struct XImage
    {
        public int Width;
        public int Height;
        public int XOffset;
        public int Format;
        public IntPtr Data;
        public int ByteOrder;
        public int BitmapUnit;
        public int BitmapBitOrder;
        public int BitmapPad;
        public int Depth;
        public int BytesPerLine;
        public int BitsPerPixel;
        public UIntPtr RedMask;
        public UIntPtr GreenMask;
        public UIntPtr BlueMask;
        public IntPtr ObData;
        public IntPtr CreateImage;
        public IntPtr DestroyImage;
        public IntPtr GetPixel;
        public IntPtr PutPixel;
        public IntPtr SubImage;
        public IntPtr AddPixel;
    }
}
