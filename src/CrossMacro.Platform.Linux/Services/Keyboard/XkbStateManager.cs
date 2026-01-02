using CrossMacro.Platform.Linux.Native.Xkb;
using Serilog;

namespace CrossMacro.Platform.Linux.Services.Keyboard;

/// <summary>
/// Manages XKB context, keymap, and state for character-to-keycode translation.
/// Handles modifier state tracking and maintains a character input cache.
/// </summary>
public class XkbStateManager : IXkbStateManager
{
    private IntPtr _xkbContext;
    private IntPtr _xkbKeymap;
    private IntPtr _xkbState;
    private readonly Lock _lock = new();
    
    private uint _modIndexShift;
    private uint _modIndexLock;
    private uint _modIndexAlt;
    private uint _modIndexAltGr;
    private uint _modIndexCtrl;
    
    private Dictionary<char, (int KeyCode, bool Shift, bool AltGr)>? _charToInputCache;

    public bool IsInitialized => _xkbState != IntPtr.Zero;

    public void Initialize(string? layout)
    {
        lock (_lock)
        {
            try
            {
                Log.Information("[XkbStateManager] Initializing with layout: {Layout}", layout ?? "default");

                _xkbContext = XkbNative.xkb_context_new(XkbNative.XKB_CONTEXT_NO_FLAGS);
                if (_xkbContext == IntPtr.Zero)
                {
                    Log.Error("[XkbStateManager] Failed to create xkb context");
                    return;
                }

                var rules = new XkbNative.xkb_rule_names { layout = layout };
                _xkbKeymap = XkbNative.xkb_keymap_new_from_names(_xkbContext, ref rules, XkbNative.XKB_KEYMAP_COMPILE_NO_FLAGS);
                if (_xkbKeymap == IntPtr.Zero)
                {
                    Log.Error("[XkbStateManager] Failed to create xkb keymap");
                    return;
                }

                _xkbState = XkbNative.xkb_state_new(_xkbKeymap);
                UpdateModifierIndices();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[XkbStateManager] Error initializing XKB");
            }
        }
    }

    public string? GetUtf8String(uint keycode)
    {
        if (_xkbState == IntPtr.Zero) return null;
        return XkbNative.GetUtf8String(_xkbState, keycode);
    }

    public char? GetCharFromKeyCode(int keyCode, bool shift, bool altGr, bool capsLock)
    {
        // Modifiers don't produce characters
        if (IsModifier(keyCode)) return null;
        if (keyCode == 57) return ' '; // Space

        if (_xkbState != IntPtr.Zero)
        {
            lock (_lock)
            {
                XkbNative.xkb_state_update_mask(_xkbState, 0, 0, 0, 0, 0, 0);
                
                uint depressedMods = 0;
                if (shift && _modIndexShift != XkbNative.XKB_MOD_INVALID) 
                    depressedMods |= (1u << (int)_modIndexShift);
                if (altGr && _modIndexAltGr != XkbNative.XKB_MOD_INVALID) 
                    depressedMods |= (1u << (int)_modIndexAltGr);

                uint lockedMods = 0;
                if (capsLock && _modIndexLock != XkbNative.XKB_MOD_INVALID) 
                    lockedMods |= (1u << (int)_modIndexLock);

                XkbNative.xkb_state_update_mask(_xkbState, depressedMods, 0, lockedMods, 0, 0, 0);
                var utf8 = XkbNative.GetUtf8String(_xkbState, (uint)(keyCode + 8));
                XkbNative.xkb_state_update_mask(_xkbState, 0, 0, 0, 0, 0, 0);

                if (!string.IsNullOrEmpty(utf8) && utf8.Length == 1) return utf8[0];
            }
        }
        return null;
    }

    public (int KeyCode, bool Shift, bool AltGr)? GetInputForChar(char c)
    {
        lock (_lock)
        {
            if (_charToInputCache == null) BuildCharInputCache();
            return _charToInputCache!.TryGetValue(c, out var input) ? input : null;
        }
    }

    private void UpdateModifierIndices()
    {
        if (_xkbKeymap == IntPtr.Zero) return;
        _charToInputCache = null;

        _modIndexShift = GetModIndex("Shift");
        _modIndexLock = GetModIndex("Lock", "Caps_Lock");
        _modIndexCtrl = GetModIndex("Control", "Ctrl");
        _modIndexAlt = GetModIndex("Mod1", "Alt", "LAlt");
        _modIndexAltGr = GetModIndex("ISO_Level3_Shift", "Mod5", "AltGr", "RAlt");
    }

    private uint GetModIndex(params string[] names)
    {
        foreach (var name in names)
        {
            var idx = XkbNative.xkb_keymap_mod_get_index(_xkbKeymap, name);
            if (idx != XkbNative.XKB_MOD_INVALID) return idx;
        }
        return XkbNative.XKB_MOD_INVALID;
    }

    private void BuildCharInputCache()
    {
        _charToInputCache = [];
        for (int code = 1; code < 255; code++)
        {
            if (IsModifier(code)) continue;
            TryAddCharToCache(code, false, false);
            TryAddCharToCache(code, true, false);
            TryAddCharToCache(code, false, true);
            TryAddCharToCache(code, true, true);
        }
    }

    private void TryAddCharToCache(int code, bool shift, bool altGr)
    {
        var c = GetCharFromKeyCode(code, shift, altGr, false);
        if (c.HasValue && !_charToInputCache!.ContainsKey(c.Value))
        {
            _charToInputCache[c.Value] = (code, shift, altGr);
        }
    }

    private static bool IsModifier(int keyCode) => keyCode is 29 or 97 or 42 or 54 or 56 or 100 or 125 or 126;

    public void Dispose()
    {
        if (_xkbState != IntPtr.Zero) XkbNative.xkb_state_unref(_xkbState);
        if (_xkbKeymap != IntPtr.Zero) XkbNative.xkb_keymap_unref(_xkbKeymap);
        if (_xkbContext != IntPtr.Zero) XkbNative.xkb_context_unref(_xkbContext);
        
        _xkbState = IntPtr.Zero;
        _xkbKeymap = IntPtr.Zero;
        _xkbContext = IntPtr.Zero;
    }
}
