using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using CrossMacro.Core.Services;
using CrossMacro.Platform.Linux.DisplayServer;
using CrossMacro.Platform.Linux.DisplayServer.Wayland;
using CrossMacro.Platform.Linux.Helpers;
using CrossMacro.Platform.Linux.Native.Xkb;
using Serilog;
using Tmds.DBus;

namespace CrossMacro.Platform.Linux.Services;

/// <summary>
/// Linux keyboard layout detection and key mapping service.
/// Detection Priority: IBus > Hyprland IPC > System Fallbacks
/// </summary>
public class LinuxKeyboardLayoutService : IKeyboardLayoutService, IDisposable
{
    private IntPtr _xkbContext;
    private IntPtr _xkbKeymap;
    private IntPtr _xkbState;
    private readonly Lock _lock = new();
    private readonly IBusLayoutSource _ibusSource = new();
    private readonly bool _isHyprland;
    private readonly bool _isKde;

    public LinuxKeyboardLayoutService()
    {
        _isHyprland = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("HYPRLAND_INSTANCE_SIGNATURE"));
        var desktop = Environment.GetEnvironmentVariable("XDG_CURRENT_DESKTOP")?.ToUpperInvariant() ?? "";
        _isKde = desktop.Contains("KDE") || desktop.Contains("PLASMA");
        
        if (_isHyprland)
            Log.Information("[KeyboardLayoutService] Environment: Hyprland");
        else if (_isKde)
            Log.Information("[KeyboardLayoutService] Environment: KDE Plasma");
        else
            Log.Information("[KeyboardLayoutService] Environment: Generic (IBus primary)");

        if (OperatingSystem.IsLinux())
            InitializeXkb();
    }

    private void InitializeXkb()
    {
        lock (_lock)
        {
            try
            {
                var layout = DetectKeyboardLayout();
                Log.Information("[KeyboardLayoutService] Initializing XKB with layout: {Layout}", layout ?? "default");

                _xkbContext = XkbNative.xkb_context_new(XkbNative.XKB_CONTEXT_NO_FLAGS);
                if (_xkbContext == IntPtr.Zero)
                {
                    Log.Error("Failed to create xkb context");
                    return;
                }

                var rules = new XkbNative.xkb_rule_names { layout = layout };
                _xkbKeymap = XkbNative.xkb_keymap_new_from_names(_xkbContext, ref rules, XkbNative.XKB_KEYMAP_COMPILE_NO_FLAGS);
                if (_xkbKeymap == IntPtr.Zero)
                {
                    Log.Error("Failed to create xkb keymap");
                    return;
                }

                _xkbState = XkbNative.xkb_state_new(_xkbKeymap);
                UpdateModifierIndices();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error initializing XKB");
            }
        }
    }

    /// <summary>
    /// Detects keyboard layout. Priority: IBus > KDE DBus > Hyprland IPC > System Fallbacks
    /// </summary>
    private string? DetectKeyboardLayout()
    {
        try
        {
            // 1. Hyprland IPC (IBus unreliable on Hyprland)
            if (_isHyprland)
            {
                var hyprLayout = DetectHyprlandLayout();
                if (!string.IsNullOrWhiteSpace(hyprLayout))
                    return hyprLayout;
            }

            // 2. KDE DBus (IBus often not used on KDE)
            if (_isKde)
            {
                var kdeLayout = DetectKdeLayout();
                if (!string.IsNullOrWhiteSpace(kdeLayout))
                    return kdeLayout;
            }

            // 3. IBus (Works on GNOME , etc.)
            var ibusLayout = _ibusSource.DetectLayout();
            if (!string.IsNullOrWhiteSpace(ibusLayout))
                return ibusLayout;

            // 4. X11/XWayland fallback
            var x11Layout = DetectX11Layout();
            if (!string.IsNullOrWhiteSpace(x11Layout))
                return x11Layout;

            // 5. System default
            return DetectLocalectlLayout();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[KeyboardLayoutService] Error detecting layout");
            return "us";
        }
    }

    private string? DetectKdeLayout()
    {
        try
        {
            using var connection = new Connection(Address.Session);
            connection.ConnectAsync().GetAwaiter().GetResult();

            var keyboard = connection.CreateProxy<IKdeKeyboard>("org.kde.keyboard", "/Layouts");
            
            // Get current layout index
            var index = keyboard.getLayoutAsync().GetAwaiter().GetResult();
            
            // Get layouts list: (shortName, variant, displayName)[]
            var layouts = keyboard.getLayoutsListAsync().GetAwaiter().GetResult();
            
            if (layouts != null && index < layouts.Length)
            {
                return layouts[index].shortName;
            }
        }
        catch (Exception ex)
        {
            Log.Debug("[KeyboardLayoutService] KDE DBus failed: {Message}", ex.Message);
        }
        return null;
    }

    private string? DetectHyprlandLayout()
    {
        try
        {
            using var ipcClient = new HyprlandIpcClient();
            if (!ipcClient.IsAvailable) return null;

            var json = ipcClient.SendCommandAsync("j/devices").GetAwaiter().GetResult();
            if (string.IsNullOrWhiteSpace(json)) return null;

            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("keyboards", out var keyboards))
            {
                foreach (var kb in keyboards.EnumerateArray())
                {
                    if (kb.TryGetProperty("active_layout_index", out _) &&
                        kb.TryGetProperty("layout", out var layout) &&
                        !string.IsNullOrWhiteSpace(layout.GetString()))
                    {
                        return layout.GetString();
                    }
                }

                foreach (var kb in keyboards.EnumerateArray())
                {
                    if (kb.TryGetProperty("layout", out var layout) && 
                        !string.IsNullOrWhiteSpace(layout.GetString()))
                    {
                        return layout.GetString();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "[KeyboardLayoutService] Hyprland IPC failed");
        }
        return null;
    }

    private string? DetectX11Layout()
    {
        var output = ProcessHelper.ExecuteCommand("setxkbmap", "-query");
        if (string.IsNullOrWhiteSpace(output)) return null;

        foreach (var line in output.Split('\n'))
        {
            if (line.StartsWith("layout:", StringComparison.OrdinalIgnoreCase))
            {
                var parts = line.Split(':', StringSplitOptions.TrimEntries);
                if (parts.Length > 1) return parts[1].Split(',')[0].Trim();
            }
        }
        return null;
    }

    private string? DetectLocalectlLayout()
    {
        var output = ProcessHelper.ExecuteCommand("localectl", "status");
        if (string.IsNullOrWhiteSpace(output)) return null;

        foreach (var line in output.Split('\n'))
        {
            if (line.Trim().StartsWith("X11 Layout:", StringComparison.OrdinalIgnoreCase))
            {
                var parts = line.Split(':', StringSplitOptions.TrimEntries);
                if (parts.Length > 1) return parts[1].Split(',')[0].Trim();
            }
        }
        return null;
    }

    public string GetKeyName(int keyCode)
    {
        // 1. Modifier keys - always return consistent names
        var modifierName = keyCode switch
        {
            29 => "Ctrl", 97 => "Ctrl", 42 => "Shift", 54 => "Shift",
            56 => "Alt", 100 => "AltGr", 125 => "Super", 126 => "Super",
            _ => null
        };
        if (modifierName != null) return modifierName;

        // 2. Special/Navigation keys
        var special = keyCode switch
        {
            57 => "Space", 28 => "Enter", 15 => "Tab", 14 => "Backspace", 1 => "Escape",
            111 => "Delete", 110 => "Insert", 102 => "Home", 107 => "End",
            104 => "PageUp", 109 => "PageDown",
            103 => "Up", 108 => "Down", 105 => "Left", 106 => "Right",
            58 => "CapsLock", 69 => "NumLock", 70 => "ScrollLock",
            99 => "PrintScreen", 119 => "Pause", 127 => "Menu",
            _ => null
        };
        if (special != null) return special;

        // 3. Function keys (F1-F10: 59-68, F11: 87, F12: 88, F13-F24: 183-194)
        if (keyCode >= 59 && keyCode <= 68) return "F" + (keyCode - 58);
        if (keyCode == 87) return "F11";
        if (keyCode == 88) return "F12";
        if (keyCode >= 183 && keyCode <= 194) return "F" + (keyCode - 170); // 183->13, 194->24

        // 4. Numpad
        var numpad = keyCode switch
        {
            71 => "Numpad7", 72 => "Numpad8", 73 => "Numpad9", 74 => "Numpad-",
            75 => "Numpad4", 76 => "Numpad5", 77 => "Numpad6", 78 => "Numpad+",
            79 => "Numpad1", 80 => "Numpad2", 81 => "Numpad3",
            82 => "Numpad0", 83 => "Numpad.", 96 => "NumpadEnter",
            98 => "Numpad/", 55 => "Numpad*", 117 => "Numpad=",
            _ => null
        };
        if (numpad != null) return numpad;

        // 5. Try XKB for character keys
        if (_xkbState != IntPtr.Zero)
        {
            var utf8 = XkbNative.GetUtf8String(_xkbState, (uint)(keyCode + 8));
            if (!string.IsNullOrEmpty(utf8)) return utf8.Length == 1 ? utf8.ToUpper() : utf8;
        }

        // 6. Digits fallback
        if (keyCode == 11) return "0";
        if (keyCode >= 2 && keyCode <= 10) return (keyCode - 1).ToString();

        // 7. Letters fallback (QWERTY)
        if (keyCode >= 16 && keyCode <= 25) return "QWERTYUIOP"[keyCode - 16].ToString();
        if (keyCode >= 30 && keyCode <= 38) return "ASDFGHJKL"[keyCode - 30].ToString();
        if (keyCode >= 44 && keyCode <= 50) return "ZXCVBNM"[keyCode - 44].ToString();

        return $"Key{keyCode}";
    }

    public int GetKeyCode(string keyName)
    {
        // Special keys
        var special = keyName switch
        {
            "Space" => 57, "Enter" or "Return" => 28, "Backspace" => 14, "Tab" => 15, "Escape" or "Esc" => 1,
            "Ctrl" or "LCtrl" => 29, "RCtrl" => 97, "Shift" or "LShift" => 42, "RShift" => 54,
            "Alt" or "LAlt" => 56, "AltGr" or "RAlt" => 100, "Super" or "LSuper" or "Meta" => 125, "RSuper" => 126,
            "CapsLock" => 58, "NumLock" => 69, "ScrollLock" => 70,
            "PrintScreen" or "PrtSc" => 99, "Pause" => 119, "Menu" => 127,
            "Delete" or "Del" => 111, "Insert" or "Ins" => 110,
            "Home" => 102, "End" => 107, "PageUp" or "PgUp" => 104, "PageDown" or "PgDn" => 109,
            "Up" => 103, "Down" => 108, "Left" => 105, "Right" => 106,
            // Numpad
            "Numpad7" => 71, "Numpad8" => 72, "Numpad9" => 73, "Numpad-" => 74,
            "Numpad4" => 75, "Numpad5" => 76, "Numpad6" => 77, "Numpad+" => 78,
            "Numpad1" => 79, "Numpad2" => 80, "Numpad3" => 81,
            "Numpad0" => 82, "Numpad." => 83, "NumpadEnter" => 96, "Numpad/" => 98, "Numpad*" => 55,
            _ => -1
        };
        if (special != -1) return special;

        // Function keys
        if (keyName.StartsWith("F", StringComparison.OrdinalIgnoreCase) && int.TryParse(keyName[1..], out var fNum))
        {
            if (fNum >= 1 && fNum <= 10) return 59 + fNum - 1;
            if (fNum == 11) return 87;
            if (fNum == 12) return 88;
            if (fNum >= 13 && fNum <= 24) return 183 + fNum - 13;
        }

        // Reverse lookup via GetKeyName
        for (int i = 0; i < 256; i++)
        {
            if (string.Equals(GetKeyName(i), keyName, StringComparison.OrdinalIgnoreCase)) return i;
        }
        return -1;
    }

    private uint _modIndexShift;
    private uint _modIndexLock;
    private uint _modIndexAlt;
    private uint _modIndexAltGr;
    private uint _modIndexCtrl;

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

    public char? GetCharFromKeyCode(int keyCode, bool leftShift, bool rightShift, bool rightAlt, bool leftAlt, bool leftCtrl, bool capsLock)
    {
        bool shift = leftShift || rightShift;
        bool altGr = rightAlt;

        if (IsModifier(keyCode)) return null;
        if (keyCode == 57) return ' ';

        if (_xkbState != IntPtr.Zero)
        {
            lock (_lock)
            {
                XkbNative.xkb_state_update_mask(_xkbState, 0, 0, 0, 0, 0, 0);
                uint depressedMods = 0;
                if (shift && _modIndexShift != XkbNative.XKB_MOD_INVALID) depressedMods |= (1u << (int)_modIndexShift);
                if (altGr && _modIndexAltGr != XkbNative.XKB_MOD_INVALID) depressedMods |= (1u << (int)_modIndexAltGr);

                uint lockedMods = 0;
                if (capsLock && _modIndexLock != XkbNative.XKB_MOD_INVALID) lockedMods |= (1u << (int)_modIndexLock);

                XkbNative.xkb_state_update_mask(_xkbState, depressedMods, 0, lockedMods, 0, 0, 0);
                var utf8 = XkbNative.GetUtf8String(_xkbState, (uint)(keyCode + 8));
                XkbNative.xkb_state_update_mask(_xkbState, 0, 0, 0, 0, 0, 0);

                if (!string.IsNullOrEmpty(utf8) && utf8.Length == 1) return utf8[0];
            }
        }
        return null;
    }

    private Dictionary<char, (int KeyCode, bool Shift, bool AltGr)>? _charToInputCache;

    public (int KeyCode, bool Shift, bool AltGr)? GetInputForChar(char c)
    {
        lock (_lock)
        {
            if (_charToInputCache == null) BuildCharInputCache();
            return _charToInputCache!.TryGetValue(c, out var input) ? input : null;
        }
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
        var c = GetCharFromKeyCode(code, shift, false, altGr, false, false, false);
        if (c.HasValue && !_charToInputCache!.ContainsKey(c.Value))
        {
            _charToInputCache[c.Value] = (code, shift, altGr);
        }
    }

    private bool IsModifier(int keyCode) => keyCode is 29 or 97 or 42 or 54 or 56 or 100 or 125 or 126;

    public void Dispose()
    {
        if (_xkbState != IntPtr.Zero) XkbNative.xkb_state_unref(_xkbState);
        if (_xkbKeymap != IntPtr.Zero) XkbNative.xkb_keymap_unref(_xkbKeymap);
        if (_xkbContext != IntPtr.Zero) XkbNative.xkb_context_unref(_xkbContext);
    }
}
