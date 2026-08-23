namespace CrossMacro.Platform.MacOS.Native;

internal static partial class MacOSPasteboard
{
    private const string AppKitLib = "/System/Library/Frameworks/AppKit.framework/AppKit";
    private const string FoundationLib = "/System/Library/Frameworks/Foundation.framework/Foundation";
    private const string ObjCLib = "/usr/lib/libobjc.A.dylib";
    private static readonly Lazy<bool> Available = new(IsAvailableCore);
    private static readonly Lazy<IntPtr> PasteboardClass = new(() => objc_getClass("NSPasteboard"));
    private static readonly Lazy<IntPtr> NSStringClass = new(() => objc_getClass("NSString"));
    private static readonly Lazy<IntPtr> NSDataClass = new(() => objc_getClass("NSData"));
    private static readonly Lazy<IntPtr> AutoreleasePoolClass = new(() => objc_getClass("NSAutoreleasePool"));
    private static readonly Lazy<IntPtr> GeneralPasteboardSelector = new(() => sel_registerName("generalPasteboard"));
    private static readonly Lazy<IntPtr> ClearContentsSelector = new(() => sel_registerName("clearContents"));
    private static readonly Lazy<IntPtr> SetStringSelector = new(() => sel_registerName("setString:forType:"));
    private static readonly Lazy<IntPtr> StringForTypeSelector = new(() => sel_registerName("stringForType:"));
    private static readonly Lazy<IntPtr> SetDataSelector = new(() => sel_registerName("setData:forType:"));
    private static readonly Lazy<IntPtr> DataForTypeSelector = new(() => sel_registerName("dataForType:"));
    private static readonly Lazy<IntPtr> InitWithBytesLengthEncodingSelector = new(() => sel_registerName("initWithBytes:length:encoding:"));
    private static readonly Lazy<IntPtr> DataUsingEncodingSelector = new(() => sel_registerName("dataUsingEncoding:"));
    private static readonly Lazy<IntPtr> DataWithBytesLengthSelector = new(() => sel_registerName("dataWithBytes:length:"));
    private static readonly Lazy<IntPtr> BytesSelector = new(() => sel_registerName("bytes"));
    private static readonly Lazy<IntPtr> LengthSelector = new(() => sel_registerName("length"));
    private static readonly Lazy<IntPtr> AllocSelector = new(() => sel_registerName("alloc"));
    private static readonly Lazy<IntPtr> InitSelector = new(() => sel_registerName("init"));
    private static readonly Lazy<IntPtr> DrainSelector = new(() => sel_registerName("drain"));
    private static readonly Lazy<IntPtr> ReleaseSelector = new(() => sel_registerName("release"));
    private const nuint NsUtf8StringEncoding = 4;

    internal static bool IsAvailable => Available.Value;

    internal static bool TrySetText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return WithPasteboard(pasteboard =>
        {
            var value = CreateOwnedString(text);
            var type = CreateOwnedString("public.utf8-plain-text");
            try
            {
                if (value == IntPtr.Zero || type == IntPtr.Zero)
                {
                    return false;
                }

                _ = objc_msgSend_nint(pasteboard, ClearContentsSelector.Value);
                return objc_msgSend_bool_IntPtr_IntPtr(pasteboard, SetStringSelector.Value, value, type);
            }
            finally
            {
                ReleaseObject(value);
                ReleaseObject(type);
            }
        });
    }

    internal static string? GetText()
    {
        return WithPasteboard(pasteboard =>
        {
            var type = CreateOwnedString("public.utf8-plain-text");
            try
            {
                if (type == IntPtr.Zero)
                {
                    return null;
                }

                var value = objc_msgSend_IntPtr_IntPtr(pasteboard, StringForTypeSelector.Value, type);
                if (value == IntPtr.Zero)
                {
                    return null;
                }

                var data = objc_msgSend_IntPtr_nuint(value, DataUsingEncodingSelector.Value, NsUtf8StringEncoding);
                return CopyData(data, maximumBytes: int.MaxValue) is { } utf8
                    ? Encoding.UTF8.GetString(utf8)
                    : null;
            }
            finally
            {
                ReleaseObject(type);
            }
        });
    }

    internal static bool TrySetPng(byte[] pngBytes)
    {
        ArgumentNullException.ThrowIfNull(pngBytes);
        if (pngBytes.Length is 0)
        {
            return false;
        }

        return WithPasteboard(pasteboard =>
        {
            var type = CreateOwnedString("public.png");
            var handle = GCHandle.Alloc(pngBytes, GCHandleType.Pinned);
            try
            {
                var data = objc_msgSend_dataWithBytesLength(
                    NSDataClass.Value,
                    DataWithBytesLengthSelector.Value,
                    handle.AddrOfPinnedObject(),
                    (nuint)pngBytes.Length);
                if (type == IntPtr.Zero || data == IntPtr.Zero)
                {
                    return false;
                }

                _ = objc_msgSend_nint(pasteboard, ClearContentsSelector.Value);
                return objc_msgSend_bool_IntPtr_IntPtr(pasteboard, SetDataSelector.Value, data, type);
            }
            finally
            {
                handle.Free();
                ReleaseObject(type);
            }
        });
    }

    internal static byte[]? GetPng(int maximumBytes)
    {
        return WithPasteboard(pasteboard =>
        {
            var type = CreateOwnedString("public.png");
            try
            {
                if (type == IntPtr.Zero)
                {
                    return null;
                }

                var data = objc_msgSend_IntPtr_IntPtr(pasteboard, DataForTypeSelector.Value, type);
                return CopyData(data, maximumBytes);
            }
            finally
            {
                ReleaseObject(type);
            }
        });
    }

    private static byte[]? CopyData(IntPtr data, int maximumBytes)
    {
        if (data == IntPtr.Zero)
        {
            return null;
        }

        var length = objc_msgSend_nuint(data, LengthSelector.Value);
        if (length > (nuint)maximumBytes)
        {
            throw new InvalidDataException("Clipboard data exceeds the maximum allowed size.");
        }

        if (length is 0)
        {
            return [];
        }

        var bytes = objc_msgSend_IntPtr(data, BytesSelector.Value);
        if (bytes == IntPtr.Zero)
        {
            return null;
        }

        var output = new byte[checked((int)length)];
        Marshal.Copy(bytes, output, 0, output.Length);
        return output;
    }

    private static T? WithPasteboard<T>(Func<IntPtr, T?> operation)
        where T : class
    {
        if (!IsAvailable)
        {
            return null;
        }

        var pool = CreateAutoreleasePool();
        try
        {
            var pasteboard = objc_msgSend_IntPtr(PasteboardClass.Value, GeneralPasteboardSelector.Value);
            return pasteboard == IntPtr.Zero ? null : operation(pasteboard);
        }
        finally
        {
            DrainAutoreleasePool(pool);
        }
    }

    private static bool WithPasteboard(Func<IntPtr, bool> operation)
    {
        if (!IsAvailable)
        {
            return false;
        }

        var pool = CreateAutoreleasePool();
        try
        {
            var pasteboard = objc_msgSend_IntPtr(PasteboardClass.Value, GeneralPasteboardSelector.Value);
            if (pasteboard == IntPtr.Zero)
            {
                return false;
            }

            return operation(pasteboard);
        }
        finally
        {
            DrainAutoreleasePool(pool);
        }
    }

    private static IntPtr CreateOwnedString(string value)
    {
        if (NSStringClass.Value == IntPtr.Zero)
        {
            return IntPtr.Zero;
        }

        var utf8 = Encoding.UTF8.GetBytes(value);
        var bytesToPin = utf8.Length is 0 ? new byte[1] : utf8;
        var handle = default(GCHandle);
        try
        {
            handle = GCHandle.Alloc(bytesToPin, GCHandleType.Pinned);
            var bytes = handle.AddrOfPinnedObject();

            var allocated = objc_msgSend_IntPtr(NSStringClass.Value, AllocSelector.Value);
            return allocated == IntPtr.Zero
                ? IntPtr.Zero
                : objc_msgSend_initWithBytesLengthEncoding(
                    allocated,
                    InitWithBytesLengthEncodingSelector.Value,
                    bytes,
                    (nuint)utf8.Length,
                    NsUtf8StringEncoding);
        }
        finally
        {
            if (handle.IsAllocated)
            {
                handle.Free();
            }
        }
    }

    private static void ReleaseObject(IntPtr value)
    {
        if (value != IntPtr.Zero)
        {
            objc_msgSend_void(value, ReleaseSelector.Value);
        }
    }

    private static bool IsAvailableCore()
    {
        if (!OperatingSystem.IsMacOS())
        {
            return false;
        }

        return NativeLibrary.TryLoad(AppKitLib, out _)
            && NativeLibrary.TryLoad(FoundationLib, out _)
            && PasteboardClass.Value != IntPtr.Zero
            && NSStringClass.Value != IntPtr.Zero
            && NSDataClass.Value != IntPtr.Zero
            && AutoreleasePoolClass.Value != IntPtr.Zero;
    }

    private static IntPtr CreateAutoreleasePool()
    {
        var allocated = objc_msgSend_IntPtr(AutoreleasePoolClass.Value, AllocSelector.Value);
        return allocated == IntPtr.Zero ? IntPtr.Zero : objc_msgSend_IntPtr(allocated, InitSelector.Value);
    }

    private static void DrainAutoreleasePool(IntPtr pool)
    {
        if (pool != IntPtr.Zero)
        {
            objc_msgSend_void(pool, DrainSelector.Value);
        }
    }

    [LibraryImport(ObjCLib, EntryPoint = "objc_getClass", StringMarshalling = StringMarshalling.Utf8)]
    private static partial IntPtr objc_getClass(string name);

    [LibraryImport(ObjCLib, EntryPoint = "sel_registerName", StringMarshalling = StringMarshalling.Utf8)]
    private static partial IntPtr sel_registerName(string name);

    [LibraryImport(ObjCLib, EntryPoint = "objc_msgSend")]
    private static partial IntPtr objc_msgSend_IntPtr(IntPtr receiver, IntPtr selector);

    [LibraryImport(ObjCLib, EntryPoint = "objc_msgSend")]
    private static partial IntPtr objc_msgSend_IntPtr_IntPtr(IntPtr receiver, IntPtr selector, IntPtr argument);

    [LibraryImport(ObjCLib, EntryPoint = "objc_msgSend")]
    private static partial nint objc_msgSend_nint(IntPtr receiver, IntPtr selector);

    [LibraryImport(ObjCLib, EntryPoint = "objc_msgSend")]
    [return: MarshalAs(UnmanagedType.I1)]
    private static partial bool objc_msgSend_bool_IntPtr_IntPtr(IntPtr receiver, IntPtr selector, IntPtr firstArgument, IntPtr secondArgument);

    [LibraryImport(ObjCLib, EntryPoint = "objc_msgSend")]
    private static partial void objc_msgSend_void(IntPtr receiver, IntPtr selector);

    [LibraryImport(ObjCLib, EntryPoint = "objc_msgSend")]
    private static partial nuint objc_msgSend_nuint(IntPtr receiver, IntPtr selector);

    [LibraryImport(ObjCLib, EntryPoint = "objc_msgSend")]
    private static partial IntPtr objc_msgSend_dataWithBytesLength(IntPtr receiver, IntPtr selector, IntPtr bytes, nuint length);

    [LibraryImport(ObjCLib, EntryPoint = "objc_msgSend")]
    private static partial IntPtr objc_msgSend_initWithBytesLengthEncoding(
        IntPtr receiver,
        IntPtr selector,
        IntPtr bytes,
        nuint length,
        nuint encoding);

    [LibraryImport(ObjCLib, EntryPoint = "objc_msgSend")]
    private static partial IntPtr objc_msgSend_IntPtr_nuint(IntPtr receiver, IntPtr selector, nuint value);
}
