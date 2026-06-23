using System;
using System.Runtime.InteropServices;

namespace CrossMacro.Platform.Linux.Native.X11
{
    /// <summary>
    /// P/Invoke declarations for Xlib (X11) functions
    /// </summary>
    public static class X11Native
    {
        private const string LibX11 = "libX11.so.6";
        public const int ZPixmap = 2;
        public static readonly UIntPtr AllPlanes = new(ulong.MaxValue);
        
        static X11Native()
        {
            // Register a custom resolver for X11 libraries to handle naming variations on different distros (e.g. NixOS)
            NativeLibrary.SetDllImportResolver(System.Reflection.Assembly.GetExecutingAssembly(), DllImportResolver);
            
            // Enable thread safety
            XInitThreads();
        }

        private static IntPtr DllImportResolver(string libraryName, System.Reflection.Assembly assembly, DllImportSearchPath? searchPath)
        {
            // Only handle our specific libraries
            if (libraryName != LibXtst && libraryName != LibX11 && libraryName != LibXi)
            {
                return IntPtr.Zero;
            }

            // Try default load first
            if (NativeLibrary.TryLoad(libraryName, assembly, searchPath, out IntPtr handle))
            {
                return handle;
            }

            // Fallback for libXtst.so.6 -> libXtst.so
            if (libraryName == LibXtst)
            {
                if (NativeLibrary.TryLoad("libXtst.so", assembly, searchPath, out handle)) return handle;
                if (NativeLibrary.TryLoad("libXtst.so.6.1.0", assembly, searchPath, out handle)) return handle;
            }
            
            // Fallback for libX11.so.6 -> libX11.so
            if (libraryName == LibX11)
            {
                 if (NativeLibrary.TryLoad("libX11.so", assembly, searchPath, out handle)) return handle;
            }

            return IntPtr.Zero;
        }

        /// <summary>
        /// Opens a connection to the X server
        /// </summary>
        /// <param name="display">Display name (null for default DISPLAY env var)</param>
        /// <returns>Display pointer, or IntPtr.Zero on failure</returns>
        [DllImport(LibX11, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr XOpenDisplay(string? display);

        /// <summary>
        /// Closes a connection to the X server
        /// </summary>
        [DllImport(LibX11, CallingConvention = CallingConvention.Cdecl)]
        public static extern int XCloseDisplay(IntPtr display);

        /// <summary>
        /// Returns drawable geometry including width, height, and depth.
        /// </summary>
        [DllImport(LibX11, CallingConvention = CallingConvention.Cdecl)]
        public static extern int XGetGeometry(
            IntPtr display,
            IntPtr drawable,
            out IntPtr root_return,
            out int x_return,
            out int y_return,
            out uint width_return,
            out uint height_return,
            out uint border_width_return,
            out uint depth_return);

        /// <summary>
        /// Returns the root window for the default screen
        /// </summary>
        [DllImport(LibX11, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr XDefaultRootWindow(IntPtr display);

        /// <summary>
        /// Returns the default screen number referenced by the XOpenDisplay function
        /// </summary>
        [DllImport(LibX11, CallingConvention = CallingConvention.Cdecl)]
        public static extern int XDefaultScreen(IntPtr display);

        /// <summary>
        /// Returns the width of the screen in pixels
        /// </summary>
        [DllImport(LibX11, CallingConvention = CallingConvention.Cdecl)]
        public static extern int XDisplayWidth(IntPtr display, int screen);

        /// <summary>
        /// Returns the height of the screen in pixels
        /// </summary>
        [DllImport(LibX11, CallingConvention = CallingConvention.Cdecl)]
        public static extern int XDisplayHeight(IntPtr display, int screen);

        /// <summary>
        /// Gets the current pointer coordinates and modifier state
        /// </summary>
        /// <param name="display">X display connection</param>
        /// <param name="window">Window to query (usually root window)</param>
        /// <param name="root_return">Root window the pointer is on</param>
        /// <param name="child_return">Child window pointer is in</param>
        /// <param name="root_x_return">X coordinate relative to root window</param>
        /// <param name="root_y_return">Y coordinate relative to root window</param>
        /// <param name="win_x_return">X coordinate relative to queried window</param>
        /// <param name="win_y_return">Y coordinate relative to queried window</param>
        /// <param name="mask_return">Current modifier keys and pointer buttons</param>
        /// <returns>True if pointer is on the same screen as window</returns>
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

        /// <summary>
        /// Initializes Xlib support for concurrent threads
        /// Must be called before any other Xlib calls in multi-threaded applications
        /// </summary>
        /// <returns>Non-zero on success</returns>
        [DllImport(LibX11, CallingConvention = CallingConvention.Cdecl)]
        private static extern int XInitThreads();

        /// <summary>
        /// Returns the root window of the specified screen
        /// </summary>
        [DllImport(LibX11, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr XRootWindow(IntPtr display, int screen_number);

        /// <summary>
        /// Moves the pointer to the specified coordinates
        /// </summary>
        /// <param name="display">X display connection</param>
        /// <param name="src_w">Source window (IntPtr.Zero for none)</param>
        /// <param name="dest_w">Destination window (usually root window)</param>
        /// <param name="src_x">Source X</param>
        /// <param name="src_y">Source Y</param>
        /// <param name="src_width">Source width</param>
        /// <param name="src_height">Source height</param>
        /// <param name="dest_x">Destination X</param>
        /// <param name="dest_y">Destination Y</param>
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


        /// <summary>
        /// Flushes the output buffer (ensures commands are sent to X server immediately)
        /// </summary>
        [DllImport(LibX11, CallingConvention = CallingConvention.Cdecl)]
        public static extern int XFlush(IntPtr display);

        [DllImport(LibX11, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr XGetImage(
            IntPtr display,
            IntPtr drawable,
            int x,
            int y,
            uint width,
            uint height,
            UIntPtr plane_mask,
            int format);

        [DllImport(LibX11, CallingConvention = CallingConvention.Cdecl)]
        public static extern UIntPtr XGetPixel(IntPtr ximage, int x, int y);

        [DllImport(LibX11, CallingConvention = CallingConvention.Cdecl)]
        public static extern int XDestroyImage(IntPtr ximage);

        [DllImport(LibX11, CallingConvention = CallingConvention.Cdecl)]
        public static extern int XPending(IntPtr display);

        [DllImport(LibX11, CallingConvention = CallingConvention.Cdecl)]
        public static extern int XNextEvent(IntPtr display, IntPtr event_return);

        [DllImport(LibX11, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool XGetEventData(IntPtr display, IntPtr cookie);

        [DllImport(LibX11, CallingConvention = CallingConvention.Cdecl)]
        public static extern void XFreeEventData(IntPtr display, IntPtr cookie);

        // XTest Extension
        private const string LibXtst = "libXtst.so.6";

        [DllImport(LibXtst, CallingConvention = CallingConvention.Cdecl)]
        public static extern bool XTestQueryExtension(IntPtr display, out int event_base_return, out int error_base_return, out int major_version_return, out int minor_version_return);

        [DllImport(LibXtst, CallingConvention = CallingConvention.Cdecl)]
        public static extern int XTestFakeKeyEvent(IntPtr display, uint keycode, bool is_press, ulong delay);

        [DllImport(LibXtst, CallingConvention = CallingConvention.Cdecl)]
        public static extern int XTestFakeButtonEvent(IntPtr display, uint button, bool is_press, ulong delay);

        [DllImport(LibXtst, CallingConvention = CallingConvention.Cdecl)]
        public static extern int XTestFakeMotionEvent(IntPtr display, int screen_number, int x, int y, ulong delay);

        [DllImport(LibXtst, CallingConvention = CallingConvention.Cdecl)]
        public static extern int XTestFakeRelativeMotionEvent(IntPtr display, int x, int y, ulong delay);

        // XInput2 Extension
        private const string LibXi = "libXi.so.6";

        [DllImport(LibXi, CallingConvention = CallingConvention.Cdecl)]
        public static extern int XIQueryVersion(IntPtr display, ref int major_version_inout, ref int minor_version_inout);

        [DllImport(LibXi, CallingConvention = CallingConvention.Cdecl)]
        public static extern int XISelectEvents(IntPtr display, IntPtr window, ref XIEventMask masks, int num_masks);
    }

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
