
namespace CrossMacro.Platform.Linux.Native.Xkb;

internal static partial class XkbNative
{
    private const string LibXkbCommon = "libxkbcommon.so.0";

    // xkb_context_flags
    public const int XKB_CONTEXT_NO_FLAGS = 0;

    // xkb_keymap_compile_flags
    public const int XKB_KEYMAP_COMPILE_NO_FLAGS = 0;
    public const uint XKB_MOD_INVALID = 0xffffffff;

    [StructLayout(LayoutKind.Sequential)]
    public struct xkb_rule_names
    {
        public string? rules;
        public string? model;
        public string? layout;
        public string? variant;
        public string? options;
    }

    [LibraryImport(LibXkbCommon)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial IntPtr xkb_context_new(int flags);

    [LibraryImport(LibXkbCommon)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void xkb_context_unref(IntPtr context);

    [LibraryImport(LibXkbCommon, EntryPoint = "xkb_keymap_new_from_names")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial IntPtr xkb_keymap_new_from_names_native(
        IntPtr context,
        IntPtr names,
        int flags);

    public static IntPtr xkb_keymap_new_from_names(IntPtr context, ref xkb_rule_names names, int flags)
    {
        var namesPointer = Marshal.AllocHGlobal(Marshal.SizeOf<xkb_rule_names>());
        Marshal.StructureToPtr(names, namesPointer, fDeleteOld: false);
        try
        {
            return xkb_keymap_new_from_names_native(context, namesPointer, flags);
        }
        finally
        {
            Marshal.DestroyStructure<xkb_rule_names>(namesPointer);
            Marshal.FreeHGlobal(namesPointer);
        }
    }

    [LibraryImport(LibXkbCommon, StringMarshalling = StringMarshalling.Utf8)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial IntPtr xkb_keymap_new_from_string(
        IntPtr context,
        string str,
        int format,
        int flags);

    [LibraryImport(LibXkbCommon)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void xkb_keymap_unref(IntPtr keymap);

    [LibraryImport(LibXkbCommon)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial IntPtr xkb_state_new(IntPtr keymap);

    [LibraryImport(LibXkbCommon, StringMarshalling = StringMarshalling.Utf8)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial uint xkb_keymap_mod_get_index(
        IntPtr keymap,
        string name);

    [LibraryImport(LibXkbCommon)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial void xkb_state_unref(IntPtr state);

    [LibraryImport(LibXkbCommon)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial int xkb_state_key_get_utf8(
        IntPtr state,
        uint keycode,
        IntPtr buffer,
        uint size);

    [LibraryImport(LibXkbCommon)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial uint xkb_state_key_get_one_sym(
        IntPtr state,
        uint keycode);

    [LibraryImport(LibXkbCommon)]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    public static partial int xkb_state_update_mask(
        IntPtr state,
        uint depressed_mods,
        uint latched_mods,
        uint locked_mods,
        uint depressed_layout,
        uint latched_layout,
        uint locked_layout);

    // Helper to get string from utf8 buffer
    public static string GetUtf8String(IntPtr state, uint keycode)
    {
        // 64 bytes should be way more than enough for any single key
        var buffer = new byte[64];
        var handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
        int len;
        try
        {
            len = xkb_state_key_get_utf8(state, keycode, handle.AddrOfPinnedObject(), 64);
        }
        finally
        {
            handle.Free();
        }

        if (len <= 0)
        {
            return string.Empty;
        }

        return System.Text.Encoding.UTF8.GetString(buffer, 0, len);
    }
}
