using System;
using System.Runtime.InteropServices;

namespace CrossMacro.Platform.Linux.Native.Xkb;

public static unsafe class XkbNative
{
    private const string LibXkbCommon = "libxkbcommon.so.0";

    public const int XKB_CONTEXT_NO_FLAGS = 0;

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

    [DllImport(LibXkbCommon, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr xkb_context_new(int flags);

    [DllImport(LibXkbCommon, CallingConvention = CallingConvention.Cdecl)]
    public static extern void xkb_context_unref(IntPtr context);

    [DllImport(LibXkbCommon, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr xkb_keymap_new_from_names(
        IntPtr context,
        ref xkb_rule_names names,
        int flags);
        
    [DllImport(LibXkbCommon, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr xkb_keymap_new_from_string(
        IntPtr context,
        string str,
        int format,
        int flags);

    [DllImport(LibXkbCommon, CallingConvention = CallingConvention.Cdecl)]
    public static extern void xkb_keymap_unref(IntPtr keymap);

    [DllImport(LibXkbCommon, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr xkb_state_new(IntPtr keymap);

    [DllImport(LibXkbCommon, CallingConvention = CallingConvention.Cdecl)]
    public static extern uint xkb_keymap_mod_get_index(
        IntPtr keymap, 
        [MarshalAs(UnmanagedType.LPStr)] string name);

    [DllImport(LibXkbCommon, CallingConvention = CallingConvention.Cdecl)]
    public static extern void xkb_state_unref(IntPtr state);

    [DllImport(LibXkbCommon, CallingConvention = CallingConvention.Cdecl)]
    public static extern int xkb_state_key_get_utf8(
        IntPtr state,
        uint keycode,
        byte* buffer,
        uint size);
        
    [DllImport(LibXkbCommon, CallingConvention = CallingConvention.Cdecl)]
    public static extern uint xkb_state_key_get_one_sym(
        IntPtr state,
        uint keycode);

    [DllImport(LibXkbCommon, CallingConvention = CallingConvention.Cdecl)]
    public static extern int xkb_state_update_mask(
        IntPtr state,
        uint depressed_mods,
        uint latched_mods,
        uint locked_mods,
        uint depressed_layout,
        uint latched_layout,
        uint locked_layout);

    public static string GetUtf8String(IntPtr state, uint keycode)
    {
        byte* buffer = stackalloc byte[64];
        int len = xkb_state_key_get_utf8(state, keycode, buffer, 64);
        
        if (len <= 0) return string.Empty;
        
        return System.Text.Encoding.UTF8.GetString(buffer, len);
    }
}
