
namespace CrossMacro.Platform.Windows.Tests.Native;

public sealed class WindowsNativeStructLayoutTests
{
    [Fact]
    public void NativeStructs_HaveExpectedX64Layouts()
    {
        Assert.Equal(8, IntPtr.Size);

        AssertLayout<InputStruct>(40, (nameof(InputStruct.type), 0), (nameof(InputStruct.U), 8));
        AssertLayout<InputUnion>(32, (nameof(InputUnion.mi), 0), (nameof(InputUnion.ki), 0), (nameof(InputUnion.hi), 0));
        AssertLayout<MouseInput>(32,
            (nameof(MouseInput.dx), 0),
            (nameof(MouseInput.dy), 4),
            (nameof(MouseInput.mouseData), 8),
            (nameof(MouseInput.dwFlags), 12),
            (nameof(MouseInput.time), 16),
            (nameof(MouseInput.dwExtraInfo), 24));
        AssertLayout<KeybdInput>(24,
            (nameof(KeybdInput.wVk), 0),
            (nameof(KeybdInput.wScan), 2),
            (nameof(KeybdInput.dwFlags), 4),
            (nameof(KeybdInput.time), 8),
            (nameof(KeybdInput.dwExtraInfo), 16));
        AssertLayout<HardwareInput>(8,
            (nameof(HardwareInput.uMsg), 0),
            (nameof(HardwareInput.wParamL), 4),
            (nameof(HardwareInput.wParamH), 6));
        AssertLayout<PointStruct>(8, (nameof(PointStruct.x), 0), (nameof(PointStruct.y), 4));
        AssertLayout<MsllHookStruct>(32,
            (nameof(MsllHookStruct.pt), 0),
            (nameof(MsllHookStruct.mouseData), 8),
            (nameof(MsllHookStruct.flags), 12),
            (nameof(MsllHookStruct.time), 16),
            (nameof(MsllHookStruct.dwExtraInfo), 24));
        AssertLayout<KbdllHookStruct>(24,
            (nameof(KbdllHookStruct.vkCode), 0),
            (nameof(KbdllHookStruct.scanCode), 4),
            (nameof(KbdllHookStruct.flags), 8),
            (nameof(KbdllHookStruct.time), 12),
            (nameof(KbdllHookStruct.dwExtraInfo), 16));
        AssertLayout<Msg>(48,
            (nameof(Msg.hwnd), 0),
            (nameof(Msg.message), 8),
            (nameof(Msg.wParam), 16),
            (nameof(Msg.lParam), 24),
            (nameof(Msg.time), 32),
            (nameof(Msg.pt), 36),
            (nameof(Msg.lPrivate), 44));
        AssertLayout<BitmapInfo>(44, (nameof(BitmapInfo.bmiHeader), 0), (nameof(BitmapInfo.bmiColors), 40));
        AssertLayout<BitmapInfoHeader>(40,
            (nameof(BitmapInfoHeader.biSize), 0),
            (nameof(BitmapInfoHeader.biWidth), 4),
            (nameof(BitmapInfoHeader.biHeight), 8),
            (nameof(BitmapInfoHeader.biPlanes), 12),
            (nameof(BitmapInfoHeader.biBitCount), 14),
            (nameof(BitmapInfoHeader.biCompression), 16),
            (nameof(BitmapInfoHeader.biSizeImage), 20),
            (nameof(BitmapInfoHeader.biXPelsPerMeter), 24),
            (nameof(BitmapInfoHeader.biYPelsPerMeter), 28),
            (nameof(BitmapInfoHeader.biClrUsed), 32),
            (nameof(BitmapInfoHeader.biClrImportant), 36));
        AssertLayout<RgbQuad>(4,
            (nameof(RgbQuad.rgbBlue), 0),
            (nameof(RgbQuad.rgbGreen), 1),
            (nameof(RgbQuad.rgbRed), 2),
            (nameof(RgbQuad.rgbReserved), 3));
        AssertLayout<WndClassEx>(80,
            (nameof(WndClassEx.cbSize), 0),
            (nameof(WndClassEx.style), 4),
            (nameof(WndClassEx.lpfnWndProc), 8),
            (nameof(WndClassEx.cbClsExtra), 16),
            (nameof(WndClassEx.cbWndExtra), 20),
            (nameof(WndClassEx.hInstance), 24),
            (nameof(WndClassEx.hIcon), 32),
            (nameof(WndClassEx.hCursor), 40),
            (nameof(WndClassEx.hbrBackground), 48),
            (nameof(WndClassEx.lpszMenuName), 56),
            (nameof(WndClassEx.lpszClassName), 64),
            (nameof(WndClassEx.hIconSm), 72));
        AssertLayout<RectStruct>(16,
            (nameof(RectStruct.left), 0),
            (nameof(RectStruct.top), 4),
            (nameof(RectStruct.right), 8),
            (nameof(RectStruct.bottom), 12));
    }

    private static void AssertLayout<T>(int size, params (string Field, int Offset)[] fields)
        where T : struct
    {
        Assert.Equal(size, Marshal.SizeOf<T>());
        foreach (var (field, offset) in fields)
        {
            Assert.Equal(offset, Marshal.OffsetOf<T>(field).ToInt32());
        }
    }
}
