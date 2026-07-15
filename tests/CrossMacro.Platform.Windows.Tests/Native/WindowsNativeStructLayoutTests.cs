using System;
using System.Runtime.InteropServices;
using CrossMacro.Platform.Windows.Native;

namespace CrossMacro.Platform.Windows.Tests.Native;

public sealed class WindowsNativeStructLayoutTests
{
    [Fact]
    public void NativeStructs_HaveExpectedX64Layouts()
    {
        Assert.Equal(8, IntPtr.Size);

        AssertLayout<INPUT>(40, (nameof(INPUT.type), 0), (nameof(INPUT.U), 8));
        AssertLayout<InputUnion>(32, (nameof(InputUnion.mi), 0), (nameof(InputUnion.ki), 0), (nameof(InputUnion.hi), 0));
        AssertLayout<MOUSEINPUT>(32,
            (nameof(MOUSEINPUT.dx), 0),
            (nameof(MOUSEINPUT.dy), 4),
            (nameof(MOUSEINPUT.mouseData), 8),
            (nameof(MOUSEINPUT.dwFlags), 12),
            (nameof(MOUSEINPUT.time), 16),
            (nameof(MOUSEINPUT.dwExtraInfo), 24));
        AssertLayout<KEYBDINPUT>(24,
            (nameof(KEYBDINPUT.wVk), 0),
            (nameof(KEYBDINPUT.wScan), 2),
            (nameof(KEYBDINPUT.dwFlags), 4),
            (nameof(KEYBDINPUT.time), 8),
            (nameof(KEYBDINPUT.dwExtraInfo), 16));
        AssertLayout<HARDWAREINPUT>(8,
            (nameof(HARDWAREINPUT.uMsg), 0),
            (nameof(HARDWAREINPUT.wParamL), 4),
            (nameof(HARDWAREINPUT.wParamH), 6));
        AssertLayout<POINT>(8, (nameof(POINT.x), 0), (nameof(POINT.y), 4));
        AssertLayout<MSLLHOOKSTRUCT>(32,
            (nameof(MSLLHOOKSTRUCT.pt), 0),
            (nameof(MSLLHOOKSTRUCT.mouseData), 8),
            (nameof(MSLLHOOKSTRUCT.flags), 12),
            (nameof(MSLLHOOKSTRUCT.time), 16),
            (nameof(MSLLHOOKSTRUCT.dwExtraInfo), 24));
        AssertLayout<KBDLLHOOKSTRUCT>(24,
            (nameof(KBDLLHOOKSTRUCT.vkCode), 0),
            (nameof(KBDLLHOOKSTRUCT.scanCode), 4),
            (nameof(KBDLLHOOKSTRUCT.flags), 8),
            (nameof(KBDLLHOOKSTRUCT.time), 12),
            (nameof(KBDLLHOOKSTRUCT.dwExtraInfo), 16));
        AssertLayout<MSG>(48,
            (nameof(MSG.hwnd), 0),
            (nameof(MSG.message), 8),
            (nameof(MSG.wParam), 16),
            (nameof(MSG.lParam), 24),
            (nameof(MSG.time), 32),
            (nameof(MSG.pt), 36),
            (nameof(MSG.lPrivate), 44));
        AssertLayout<BITMAPINFO>(44, (nameof(BITMAPINFO.bmiHeader), 0), (nameof(BITMAPINFO.bmiColors), 40));
        AssertLayout<BITMAPINFOHEADER>(40,
            (nameof(BITMAPINFOHEADER.biSize), 0),
            (nameof(BITMAPINFOHEADER.biWidth), 4),
            (nameof(BITMAPINFOHEADER.biHeight), 8),
            (nameof(BITMAPINFOHEADER.biPlanes), 12),
            (nameof(BITMAPINFOHEADER.biBitCount), 14),
            (nameof(BITMAPINFOHEADER.biCompression), 16),
            (nameof(BITMAPINFOHEADER.biSizeImage), 20),
            (nameof(BITMAPINFOHEADER.biXPelsPerMeter), 24),
            (nameof(BITMAPINFOHEADER.biYPelsPerMeter), 28),
            (nameof(BITMAPINFOHEADER.biClrUsed), 32),
            (nameof(BITMAPINFOHEADER.biClrImportant), 36));
        AssertLayout<RGBQUAD>(4,
            (nameof(RGBQUAD.rgbBlue), 0),
            (nameof(RGBQUAD.rgbGreen), 1),
            (nameof(RGBQUAD.rgbRed), 2),
            (nameof(RGBQUAD.rgbReserved), 3));
        AssertLayout<WNDCLASSEX>(80,
            (nameof(WNDCLASSEX.cbSize), 0),
            (nameof(WNDCLASSEX.style), 4),
            (nameof(WNDCLASSEX.lpfnWndProc), 8),
            (nameof(WNDCLASSEX.cbClsExtra), 16),
            (nameof(WNDCLASSEX.cbWndExtra), 20),
            (nameof(WNDCLASSEX.hInstance), 24),
            (nameof(WNDCLASSEX.hIcon), 32),
            (nameof(WNDCLASSEX.hCursor), 40),
            (nameof(WNDCLASSEX.hbrBackground), 48),
            (nameof(WNDCLASSEX.lpszMenuName), 56),
            (nameof(WNDCLASSEX.lpszClassName), 64),
            (nameof(WNDCLASSEX.hIconSm), 72));
        AssertLayout<RECT>(16,
            (nameof(RECT.left), 0),
            (nameof(RECT.top), 4),
            (nameof(RECT.right), 8),
            (nameof(RECT.bottom), 12));
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
