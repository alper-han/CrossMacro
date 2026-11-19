using System.Text;

namespace CrossMacro.Platform.MacOS.Native;

internal static partial class MacOSPasteboard
{
    private const nuint NsUtf8StringEncoding = 4;
    private const string Utf8TextType = "public.utf8-plain-text";
    private const string PngType = "public.png";

    private static readonly Lazy<bool> Available = new(IsAvailableCore);
    private static readonly Lazy<IntPtr> PasteboardClass = new(() => ObjCRuntime.GetClass("NSPasteboard"));
    private static readonly Lazy<IntPtr> NSStringClass = new(() => ObjCRuntime.GetClass("NSString"));
    private static readonly Lazy<IntPtr> NSDataClass = new(() => ObjCRuntime.GetClass("NSData"));
    private static readonly Lazy<IntPtr> GeneralPasteboardSelector = new(() => ObjCRuntime.RegisterSelector("generalPasteboard"));
    private static readonly Lazy<IntPtr> ClearContentsSelector = new(() => ObjCRuntime.RegisterSelector("clearContents"));
    private static readonly Lazy<IntPtr> SetStringSelector = new(() => ObjCRuntime.RegisterSelector("setString:forType:"));
    private static readonly Lazy<IntPtr> StringForTypeSelector = new(() => ObjCRuntime.RegisterSelector("stringForType:"));
    private static readonly Lazy<IntPtr> SetDataSelector = new(() => ObjCRuntime.RegisterSelector("setData:forType:"));
    private static readonly Lazy<IntPtr> DataForTypeSelector = new(() => ObjCRuntime.RegisterSelector("dataForType:"));
    private static readonly Lazy<IntPtr> InitWithBytesLengthEncodingSelector = new(() => ObjCRuntime.RegisterSelector("initWithBytes:length:encoding:"));
    private static readonly Lazy<IntPtr> DataUsingEncodingSelector = new(() => ObjCRuntime.RegisterSelector("dataUsingEncoding:"));
    private static readonly Lazy<IntPtr> DataWithBytesLengthSelector = new(() => ObjCRuntime.RegisterSelector("dataWithBytes:length:"));
    private static readonly Lazy<IntPtr> BytesSelector = new(() => ObjCRuntime.RegisterSelector("bytes"));
    private static readonly Lazy<IntPtr> LengthSelector = new(() => ObjCRuntime.RegisterSelector("length"));

    internal static bool IsAvailable => Available.Value;

    internal static bool TrySetText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return WithPasteboard(unavailableResult: false, pasteboard =>
        {
            var value = CreateOwnedString(text);
            var type = CreateOwnedString(Utf8TextType);
            try
            {
                if (value == IntPtr.Zero || type == IntPtr.Zero)
                {
                    return false;
                }

                ObjCRuntime.SendVoid(pasteboard, ClearContentsSelector.Value);
                return objc_msgSend_bool_IntPtr_IntPtr(pasteboard, SetStringSelector.Value, value, type);
            }
            finally
            {
                ObjCRuntime.Release(value);
                ObjCRuntime.Release(type);
            }
        });
    }

    internal static string? GetText()
    {
        return WithPasteboard<string?>(unavailableResult: null, pasteboard =>
        {
            var type = CreateOwnedString(Utf8TextType);
            try
            {
                if (type == IntPtr.Zero)
                {
                    return null;
                }

                var value = ObjCRuntime.SendIntPtr(pasteboard, StringForTypeSelector.Value, type);
                if (value == IntPtr.Zero)
                {
                    return null;
                }

                var data = ObjCRuntime.SendIntPtr(value, DataUsingEncodingSelector.Value, NsUtf8StringEncoding);
                return CopyData(data, maximumBytes: int.MaxValue) is { } utf8
                    ? Encoding.UTF8.GetString(utf8)
                    : null;
            }
            finally
            {
                ObjCRuntime.Release(type);
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

        return WithPasteboard(unavailableResult: false, pasteboard =>
        {
            var type = CreateOwnedString(PngType);
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

                ObjCRuntime.SendVoid(pasteboard, ClearContentsSelector.Value);
                return objc_msgSend_bool_IntPtr_IntPtr(pasteboard, SetDataSelector.Value, data, type);
            }
            finally
            {
                handle.Free();
                ObjCRuntime.Release(type);
            }
        });
    }

    internal static byte[]? GetPng(int maximumBytes)
    {
        return WithPasteboard<byte[]?>(unavailableResult: null, pasteboard =>
        {
            var type = CreateOwnedString(PngType);
            try
            {
                if (type == IntPtr.Zero)
                {
                    return null;
                }

                var data = ObjCRuntime.SendIntPtr(pasteboard, DataForTypeSelector.Value, type);
                return CopyData(data, maximumBytes);
            }
            finally
            {
                ObjCRuntime.Release(type);
            }
        });
    }

    private static byte[]? CopyData(IntPtr data, int maximumBytes)
    {
        if (data == IntPtr.Zero)
        {
            return null;
        }

        var length = ObjCRuntime.SendNuint(data, LengthSelector.Value);
        if (length > (nuint)maximumBytes)
        {
            throw new InvalidDataException("Clipboard data exceeds the maximum allowed size.");
        }

        if (length is 0)
        {
            return [];
        }

        var bytes = ObjCRuntime.SendIntPtr(data, BytesSelector.Value);
        if (bytes == IntPtr.Zero)
        {
            return null;
        }

        var output = new byte[checked((int)length)];
        Marshal.Copy(bytes, output, 0, output.Length);
        return output;
    }

    private static T WithPasteboard<T>(T unavailableResult, Func<IntPtr, T> operation)
    {
        if (!IsAvailable)
        {
            return unavailableResult;
        }

        using var pool = ObjCRuntime.PushAutoreleasePool();
        var pasteboard = ObjCRuntime.SendIntPtr(PasteboardClass.Value, GeneralPasteboardSelector.Value);
        return pasteboard == IntPtr.Zero ? unavailableResult : operation(pasteboard);
    }

    private static IntPtr CreateOwnedString(string value)
    {
        if (NSStringClass.Value == IntPtr.Zero)
        {
            return IntPtr.Zero;
        }

        var utf8 = Encoding.UTF8.GetBytes(value);
        var bytesToPin = utf8.Length is 0 ? new byte[1] : utf8;
        var handle = GCHandle.Alloc(bytesToPin, GCHandleType.Pinned);
        try
        {
            var allocated = ObjCRuntime.Alloc(NSStringClass.Value);
            if (allocated == IntPtr.Zero)
            {
                return IntPtr.Zero;
            }

            var initialized = objc_msgSend_initWithBytesLengthEncoding(
                allocated,
                InitWithBytesLengthEncodingSelector.Value,
                handle.AddrOfPinnedObject(),
                (nuint)utf8.Length,
                NsUtf8StringEncoding);
            if (initialized == IntPtr.Zero)
            {
                ObjCRuntime.Release(allocated);
            }

            return initialized;
        }
        finally
        {
            handle.Free();
        }
    }

    private static bool IsAvailableCore() =>
        ObjCRuntime.IsAvailable
        && PasteboardClass.Value != IntPtr.Zero
        && NSStringClass.Value != IntPtr.Zero
        && NSDataClass.Value != IntPtr.Zero;

    [LibraryImport(ObjCRuntime.Library, EntryPoint = "objc_msgSend")]
    [return: MarshalAs(UnmanagedType.I1)]
    private static partial bool objc_msgSend_bool_IntPtr_IntPtr(IntPtr receiver, IntPtr selector, IntPtr firstArgument, IntPtr secondArgument);

    [LibraryImport(ObjCRuntime.Library, EntryPoint = "objc_msgSend")]
    private static partial IntPtr objc_msgSend_dataWithBytesLength(IntPtr receiver, IntPtr selector, IntPtr bytes, nuint length);

    [LibraryImport(ObjCRuntime.Library, EntryPoint = "objc_msgSend")]
    private static partial IntPtr objc_msgSend_initWithBytesLengthEncoding(
        IntPtr receiver,
        IntPtr selector,
        IntPtr bytes,
        nuint length,
        nuint encoding);
}
