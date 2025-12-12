using System;
using System.Runtime.InteropServices;

namespace CrossMacro.Platform.Linux.Native.X11
{
    public static class X11Native
    {
        private const string LibX11 = "libX11.so.6";
        
        static X11Native()
        {
            XInitThreads();
        }

        [DllImport(LibX11, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr XOpenDisplay(string? display);

        [DllImport(LibX11, CallingConvention = CallingConvention.Cdecl)]
        public static extern int XCloseDisplay(IntPtr display);

        [DllImport(LibX11, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr XDefaultRootWindow(IntPtr display);

        [DllImport(LibX11, CallingConvention = CallingConvention.Cdecl)]
        public static extern int XDefaultScreen(IntPtr display);

        [DllImport(LibX11, CallingConvention = CallingConvention.Cdecl)]
        public static extern int XDisplayWidth(IntPtr display, int screen);

        [DllImport(LibX11, CallingConvention = CallingConvention.Cdecl)]
        public static extern int XDisplayHeight(IntPtr display, int screen);

        [DllImport(LibX11, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool XQueryPointer(
            IntPtr display,
            IntPtr window,
            out IntPtr root_return,
            out IntPtr child_return,
            out int root_x_return,
            out int root_y_return,
            out int win_x_return,
            out int win_y_return,
            out uint mask_return);

        [DllImport(LibX11, CallingConvention = CallingConvention.Cdecl)]
        private static extern int XInitThreads();

        [DllImport(LibX11, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr XRootWindow(IntPtr display, int screen_number);

        [DllImport(LibX11, CallingConvention = CallingConvention.Cdecl)]
        public static extern void XWarpPointer(
            IntPtr display,
            IntPtr src_w,
            IntPtr dest_w,
            int src_x,
            int src_y,
            uint src_width,
            uint src_height,
            int dest_x,
            int dest_y);

        [DllImport(LibX11, CallingConvention = CallingConvention.Cdecl)]
        public static extern int XFlush(IntPtr display);
    }
}
