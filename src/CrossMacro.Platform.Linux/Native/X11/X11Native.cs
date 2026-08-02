
namespace CrossMacro.Platform.Linux.Native.X11;

/// <summary>
/// P/Invoke declarations for Xlib (X11) functions
/// </summary>
internal static partial class X11Native
{
    private const string LibX11 = "libX11.so.6";
    public const int SelectionClear = 29;
    public const int SelectionRequest = 30;
    public const int SelectionNotify = 31;
    public const int PropertyNotify = 28;
    public const int PropertyNewValue = 0;
    public const int PropertyDelete = 1;
    public const int PropModeReplace = 0;
    public const nuint PropertyChangeMask = 1u << 22;
    public const nuint CurrentTime = 0;
    public const int ZPixmap = 2;
    public static readonly UIntPtr AllPlanes = new(ulong.MaxValue);

    static X11Native()
    {
        // Register a custom resolver for X11 libraries to handle naming variations on different distros (e.g. NixOS)
        NativeLibrary.SetDllImportResolver(System.Reflection.Assembly.GetExecutingAssembly(), DllImportResolver);

        // Enable thread safety
        _ = XInitThreads();
    }

    private static IntPtr DllImportResolver(string libraryName, System.Reflection.Assembly assembly, DllImportSearchPath? searchPath)
    {
        // Only handle our specific libraries
        if (!string.Equals(libraryName, LibXtst, StringComparison.Ordinal) && !string.Equals(libraryName, LibX11, StringComparison.Ordinal) && !string.Equals(libraryName, LibXi, StringComparison.Ordinal))
        {
            return IntPtr.Zero;
        }

        // Try default load first
        if (NativeLibrary.TryLoad(libraryName, assembly, searchPath, out IntPtr handle))
        {
            return handle;
        }

        // Fallback for libXtst.so.6 -> libXtst.so
        if (string.Equals(libraryName, LibXtst, StringComparison.Ordinal))
        {
            if (NativeLibrary.TryLoad("libXtst.so", assembly, searchPath, out handle))
            {
                return handle;
            }

            if (NativeLibrary.TryLoad("libXtst.so.6.1.0", assembly, searchPath, out handle))
            {
                return handle;
            }
        }

        // Fallback for libX11.so.6 -> libX11.so
        if (string.Equals(libraryName, LibX11, StringComparison.Ordinal) && NativeLibrary.TryLoad("libX11.so", assembly, searchPath, out handle))
        {
            return handle;
        }

        return IntPtr.Zero;
    }

    /// <summary>
    /// Opens a connection to the X server
    /// </summary>
    /// <param name="display">Display name (null for default DISPLAY env var)</param>
    /// <returns>Display pointer, or IntPtr.Zero on failure</returns>
    [LibraryImport(LibX11, StringMarshalling = StringMarshalling.Utf8)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial IntPtr XOpenDisplay(string? display);

    /// <summary>
    /// Closes a connection to the X server
    /// </summary>
    [LibraryImport(LibX11)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial int XCloseDisplay(IntPtr display);

    /// <summary>
    /// Returns drawable geometry including width, height, and depth.
    /// </summary>
    [LibraryImport(LibX11)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial int XGetGeometry(
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
    [LibraryImport(LibX11)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial IntPtr XDefaultRootWindow(IntPtr display);

    /// <summary>
    /// Returns the default screen number referenced by the XOpenDisplay function
    /// </summary>
    [LibraryImport(LibX11)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial int XDefaultScreen(IntPtr display);

    /// <summary>
    /// Returns the width of the screen in pixels
    /// </summary>
    [LibraryImport(LibX11)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial int XDisplayWidth(IntPtr display, int screen);

    /// <summary>
    /// Returns the height of the screen in pixels
    /// </summary>
    [LibraryImport(LibX11)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial int XDisplayHeight(IntPtr display, int screen);

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
    [LibraryImport(LibX11)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool XQueryPointer(
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
    [LibraryImport(LibX11)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial int XInitThreads();

    /// <summary>
    /// Returns the root window of the specified screen
    /// </summary>
    [LibraryImport(LibX11)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial IntPtr XRootWindow(IntPtr display, int screen_number);

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
    [LibraryImport(LibX11)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void XWarpPointer(
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
    [LibraryImport(LibX11)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial int XFlush(IntPtr display);

    [LibraryImport(LibX11)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial IntPtr XGetImage(
        IntPtr display,
        IntPtr drawable,
        int x,
        int y,
        uint width,
        uint height,
        UIntPtr plane_mask,
        int format);

    [LibraryImport(LibX11)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial UIntPtr XGetPixel(IntPtr ximage, int x, int y);

    [LibraryImport(LibX11)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial int XDestroyImage(IntPtr ximage);

    [LibraryImport(LibX11)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial int XPending(IntPtr display);

    [LibraryImport(LibX11)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial int XNextEvent(IntPtr display, IntPtr event_return);

    [LibraryImport(LibX11, StringMarshalling = StringMarshalling.Utf8)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial nuint XInternAtom(
        IntPtr display,
        string atom_name,
        [MarshalAs(UnmanagedType.Bool)] bool only_if_exists);

    [LibraryImport(LibX11)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial nuint XCreateSimpleWindow(
        IntPtr display,
        nuint parent,
        int x,
        int y,
        uint width,
        uint height,
        uint border_width,
        nuint border,
        nuint background);

    [LibraryImport(LibX11)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial int XDestroyWindow(IntPtr display, nuint window);

    [LibraryImport(LibX11)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial int XSelectInput(IntPtr display, nuint window, nuint event_mask);

    [LibraryImport(LibX11)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial int XSetSelectionOwner(IntPtr display, nuint selection, nuint owner, nuint time);

    [LibraryImport(LibX11)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial nuint XGetSelectionOwner(IntPtr display, nuint selection);

    [LibraryImport(LibX11)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial int XConvertSelection(
        IntPtr display,
        nuint selection,
        nuint target,
        nuint property,
        nuint requestor,
        nuint time);

    [LibraryImport(LibX11)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial int XDeleteProperty(IntPtr display, nuint window, nuint property);

    [LibraryImport(LibX11)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial int XGetWindowProperty(
        IntPtr display,
        nuint window,
        nuint property,
        nint long_offset,
        nint long_length,
        [MarshalAs(UnmanagedType.Bool)] bool delete,
        nuint requested_type,
        out nuint actual_type,
        out int actual_format,
        out nuint nitems,
        out nuint bytes_after,
        out IntPtr prop_return);

    [LibraryImport(LibX11)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial int XChangeProperty(
        IntPtr display,
        nuint window,
        nuint property,
        nuint type,
        int format,
        int mode,
        IntPtr data,
        int nelements);

    [LibraryImport(LibX11)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool XSendEvent(
        IntPtr display,
        nuint window,
        [MarshalAs(UnmanagedType.Bool)] bool propagate,
        nuint event_mask,
        IntPtr event_send);

    [LibraryImport(LibX11)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial int XConnectionNumber(IntPtr display);

    [LibraryImport(LibX11)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial int XFree(IntPtr data);

    [LibraryImport(LibX11)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool XGetEventData(IntPtr display, IntPtr cookie);

    [LibraryImport(LibX11)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void XFreeEventData(IntPtr display, IntPtr cookie);

    // XTest Extension
    private const string LibXtst = "libXtst.so.6";

    [LibraryImport(LibXtst)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool XTestQueryExtension(IntPtr display, out int event_base_return, out int error_base_return, out int major_version_return, out int minor_version_return);

    [LibraryImport(LibXtst)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial int XTestFakeKeyEvent(IntPtr display, uint keycode, [MarshalAs(UnmanagedType.Bool)] bool is_press, ulong delay);

    [LibraryImport(LibXtst)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial int XTestFakeButtonEvent(IntPtr display, uint button, [MarshalAs(UnmanagedType.Bool)] bool is_press, ulong delay);

    [LibraryImport(LibXtst)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial int XTestFakeMotionEvent(IntPtr display, int screen_number, int x, int y, ulong delay);

    [LibraryImport(LibXtst)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial int XTestFakeRelativeMotionEvent(IntPtr display, int x, int y, ulong delay);

    // XInput2 Extension
    private const string LibXi = "libXi.so.6";

    [LibraryImport(LibXi)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial int XIQueryVersion(IntPtr display, ref int major_version_inout, ref int minor_version_inout);

    [LibraryImport(LibXi)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial int XISelectEvents(IntPtr display, IntPtr window, ref XIEventMask masks, int num_masks);
}
