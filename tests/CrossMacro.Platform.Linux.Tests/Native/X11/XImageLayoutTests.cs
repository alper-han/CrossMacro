using System.Runtime.InteropServices;
using CrossMacro.Platform.Linux.Native.X11;

namespace CrossMacro.Platform.Linux.Tests.Native.X11;

public sealed class XImageLayoutTests
{
    [Fact]
    public void XImage_HasExpectedSequentialLayout()
    {
        Assert.Equal(136, Marshal.SizeOf<XImage>());
        Assert.Equal(0, Marshal.OffsetOf<XImage>(nameof(XImage.Width)).ToInt32());
        Assert.Equal(4, Marshal.OffsetOf<XImage>(nameof(XImage.Height)).ToInt32());
        Assert.Equal(8, Marshal.OffsetOf<XImage>(nameof(XImage.XOffset)).ToInt32());
        Assert.Equal(12, Marshal.OffsetOf<XImage>(nameof(XImage.Format)).ToInt32());
        Assert.Equal(16, Marshal.OffsetOf<XImage>(nameof(XImage.Data)).ToInt32());
        Assert.Equal(24, Marshal.OffsetOf<XImage>(nameof(XImage.ByteOrder)).ToInt32());
        Assert.Equal(28, Marshal.OffsetOf<XImage>(nameof(XImage.BitmapUnit)).ToInt32());
        Assert.Equal(32, Marshal.OffsetOf<XImage>(nameof(XImage.BitmapBitOrder)).ToInt32());
        Assert.Equal(36, Marshal.OffsetOf<XImage>(nameof(XImage.BitmapPad)).ToInt32());
        Assert.Equal(40, Marshal.OffsetOf<XImage>(nameof(XImage.Depth)).ToInt32());
        Assert.Equal(44, Marshal.OffsetOf<XImage>(nameof(XImage.BytesPerLine)).ToInt32());
        Assert.Equal(48, Marshal.OffsetOf<XImage>(nameof(XImage.BitsPerPixel)).ToInt32());
        Assert.Equal(56, Marshal.OffsetOf<XImage>(nameof(XImage.RedMask)).ToInt32());
        Assert.Equal(64, Marshal.OffsetOf<XImage>(nameof(XImage.GreenMask)).ToInt32());
        Assert.Equal(72, Marshal.OffsetOf<XImage>(nameof(XImage.BlueMask)).ToInt32());
        Assert.Equal(80, Marshal.OffsetOf<XImage>(nameof(XImage.ObData)).ToInt32());
        Assert.Equal(88, Marshal.OffsetOf<XImage>(nameof(XImage.CreateImage)).ToInt32());
        Assert.Equal(96, Marshal.OffsetOf<XImage>(nameof(XImage.DestroyImage)).ToInt32());
        Assert.Equal(104, Marshal.OffsetOf<XImage>(nameof(XImage.GetPixel)).ToInt32());
        Assert.Equal(112, Marshal.OffsetOf<XImage>(nameof(XImage.PutPixel)).ToInt32());
        Assert.Equal(120, Marshal.OffsetOf<XImage>(nameof(XImage.SubImage)).ToInt32());
        Assert.Equal(128, Marshal.OffsetOf<XImage>(nameof(XImage.AddPixel)).ToInt32());
    }

    [Fact]
    public void XImage_RoundTripsThroughUnmanagedMemory()
    {
        var image = new XImage
        {
            Width = 1920,
            Height = 1080,
            XOffset = -7,
            Format = 2,
            Data = new IntPtr(0x1000),
            ByteOrder = 1,
            BitmapUnit = 32,
            BitmapBitOrder = 0,
            BitmapPad = 32,
            Depth = 24,
            BytesPerLine = 7680,
            BitsPerPixel = 32,
            RedMask = new UIntPtr(0x00FF0000UL),
            GreenMask = new UIntPtr(0x0000FF00UL),
            BlueMask = new UIntPtr(0x000000FFUL),
            ObData = new IntPtr(0x2000),
            CreateImage = new IntPtr(0x3000),
            DestroyImage = new IntPtr(0x4000),
            GetPixel = new IntPtr(0x5000),
            PutPixel = new IntPtr(0x6000),
            SubImage = new IntPtr(0x7000),
            AddPixel = new IntPtr(0x8000),
        };

        var roundTrip = RoundTrip(image);

        Assert.Equal(image.Width, roundTrip.Width);
        Assert.Equal(image.Height, roundTrip.Height);
        Assert.Equal(image.XOffset, roundTrip.XOffset);
        Assert.Equal(image.Format, roundTrip.Format);
        Assert.Equal(image.Data, roundTrip.Data);
        Assert.Equal(image.ByteOrder, roundTrip.ByteOrder);
        Assert.Equal(image.BitmapUnit, roundTrip.BitmapUnit);
        Assert.Equal(image.BitmapBitOrder, roundTrip.BitmapBitOrder);
        Assert.Equal(image.BitmapPad, roundTrip.BitmapPad);
        Assert.Equal(image.Depth, roundTrip.Depth);
        Assert.Equal(image.BytesPerLine, roundTrip.BytesPerLine);
        Assert.Equal(image.BitsPerPixel, roundTrip.BitsPerPixel);
        Assert.Equal(image.RedMask, roundTrip.RedMask);
        Assert.Equal(image.GreenMask, roundTrip.GreenMask);
        Assert.Equal(image.BlueMask, roundTrip.BlueMask);
        Assert.Equal(image.ObData, roundTrip.ObData);
        Assert.Equal(image.CreateImage, roundTrip.CreateImage);
        Assert.Equal(image.DestroyImage, roundTrip.DestroyImage);
        Assert.Equal(image.GetPixel, roundTrip.GetPixel);
        Assert.Equal(image.PutPixel, roundTrip.PutPixel);
        Assert.Equal(image.SubImage, roundTrip.SubImage);
        Assert.Equal(image.AddPixel, roundTrip.AddPixel);
    }

    private static XImage RoundTrip(XImage image)
    {
        var pointer = Marshal.AllocHGlobal(Marshal.SizeOf<XImage>());
        try
        {
            Marshal.StructureToPtr(image, pointer, fDeleteOld: false);
            return Marshal.PtrToStructure<XImage>(pointer);
        }
        finally
        {
            Marshal.FreeHGlobal(pointer);
        }
    }
}
