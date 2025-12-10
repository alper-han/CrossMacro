using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using CrossMacro.Core.Services;
using CrossMacro.Native.Xkb;
using Serilog;

namespace CrossMacro.Infrastructure.Services;

public class KeyboardLayoutService : IKeyboardLayoutService, IDisposable
{
    // XKB State
    private IntPtr _xkbContext;
    private IntPtr _xkbKeymap;
    private IntPtr _xkbState;
    private readonly object _lock = new();

    public KeyboardLayoutService()
    {
        InitializeXkb();
    }

    private void InitializeXkb()
    {
        lock (_lock)
        {
            try
            {
                var layout = DetectKeyboardLayout();
                Log.Information("[KeyboardLayoutService] Detected keyboard layout: {Layout}", layout ?? "default");

                _xkbContext = XkbNative.xkb_context_new(XkbNative.XKB_CONTEXT_NO_FLAGS);
                if (_xkbContext == IntPtr.Zero)
                {
                    Log.Error("Failed to create xkb context");
                    return;
                }

                var rules = new XkbNative.xkb_rule_names
                {
                    layout = layout
                };

                _xkbKeymap = XkbNative.xkb_keymap_new_from_names(_xkbContext, ref rules, XkbNative.XKB_KEYMAP_COMPILE_NO_FLAGS);
                if (_xkbKeymap == IntPtr.Zero)
                {
                    Log.Error("Failed to create xkb keymap");
                    return;
                }

                _xkbState = XkbNative.xkb_state_new(_xkbKeymap);
                if (_xkbState == IntPtr.Zero)
                {
                    Log.Error("Failed to create xkb state");
                }
                
                UpdateModifierIndices();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error initializing XKB");
            }
        }
    }

    private string? DetectKeyboardLayout()
    {
        string? layout = null;
        
        // 1. Try hyprctl (Wayland - Hyprland)
        layout = DetectHyprlandLayout();
        if (!string.IsNullOrWhiteSpace(layout)) return layout;

        // Check if running on Wayland
        bool isWayland = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY"));

        if (isWayland)
        {
            // On Wayland, setxkbmap usually queries XWayland which defaults to 'us' unless configured.
            // localectl is more likely to represent the user's system preference.
            
            // 2. Try localectl (Systemd / Generic)
            layout = DetectLocalectlLayout();
            if (!string.IsNullOrWhiteSpace(layout)) return layout;

            // 3. Try setxkbmap (X11/XWayland) as fallback
            layout = DetectX11Layout();
            if (!string.IsNullOrWhiteSpace(layout)) return layout;
        }
        else
        {
            // On X11, setxkbmap is the source of truth for the current session.

            // 2. Try setxkbmap (X11)
            layout = DetectX11Layout();
            if (!string.IsNullOrWhiteSpace(layout)) return layout;

            // 3. Try localectl (Systemd / Generic)
            layout = DetectLocalectlLayout();
            if (!string.IsNullOrWhiteSpace(layout)) return layout;
        }

        // 4. Try Environment Variable
        layout = Environment.GetEnvironmentVariable("XKB_DEFAULT_LAYOUT");
        if (!string.IsNullOrWhiteSpace(layout)) return layout;

        return null; // Fallback to "default" (US usually)
    }

    private string? DetectHyprlandLayout()
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "hyprctl",
                Arguments = "devices -j",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process != null)
            {
                var json = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode == 0)
                {
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("keyboards", out var keyboards))
                    {
                        foreach (var kb in keyboards.EnumerateArray())
                        {
                            if (kb.TryGetProperty("active_layout_index", out var _)) // Prefer active ones
                            {
                                if (kb.TryGetProperty("layout", out var layout) && !string.IsNullOrWhiteSpace(layout.GetString()))
                                {
                                    return layout.GetString();
                                }
                            }
                        }
                        
                        // Fallback to any keyboard if no active index found
                        foreach (var kb in keyboards.EnumerateArray())
                        {
                             if (kb.TryGetProperty("layout", out var layout) && !string.IsNullOrWhiteSpace(layout.GetString()))
                             {
                                 return layout.GetString();
                             }
                        }
                    }
                }
            }
        }
        catch { /* Ignore */ }
        return null;
    }

    private string? DetectX11Layout()
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "setxkbmap",
                Arguments = "-query",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process != null)
            {
                var output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode == 0)
                {
                    // Output format:
                    // rules:      evdev
                    // model:      pc105
                    // layout:     tr
                    foreach (var line in output.Split('\n'))
                    {
                        if (line.StartsWith("layout:", StringComparison.OrdinalIgnoreCase))
                        {
                            var parts = line.Split(':', StringSplitOptions.TrimEntries);
                            if (parts.Length > 1)
                            {
                                // Return first layout if multiple (e.g. "us,ru")
                                return parts[1].Split(',')[0].Trim();
                            }
                        }
                    }
                }
            }
        }
        catch { /* Ignore */ }
        return null;
    }

    private string? DetectLocalectlLayout()
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "localectl",
                Arguments = "status",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process != null)
            {
                var output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode == 0)
                {
                    // Output includes: "X11 Layout: tr"
                    foreach (var line in output.Split('\n'))
                    {
                        if (line.Trim().StartsWith("X11 Layout:", StringComparison.OrdinalIgnoreCase))
                        {
                            var parts = line.Split(':', StringSplitOptions.TrimEntries);
                            if (parts.Length > 1)
                            {
                                return parts[1].Split(',')[0].Trim();
                            }
                        }
                    }
                }
            }
        }
        catch { /* Ignore */ }
        return null;
    }

    public string GetKeyName(int keyCode)
    {
         // Special overrides for non-printable keys
        var special = keyCode switch
        {
            57 => "Space",
            28 => "Enter",
            15 => "Tab",
            14 => "Backspace",
            1 => "Escape",
            111 => "Delete",
            110 => "Insert",
            102 => "Home",
            107 => "End",
            104 => "PageUp",
            109 => "PageDown",
            103 => "Up",
            108 => "Down",
            105 => "Left",
            106 => "Right",
            _ => null
        };

        if (special != null) return special;

        // Try XKB
        if (_xkbState != IntPtr.Zero)
        {
            // Evdev keycode + 8 for XKB
            var utf8 = XkbNative.GetUtf8String(_xkbState, (uint)(keyCode + 8));
            if (!string.IsNullOrEmpty(utf8))
            {
                // Return uppercase for single letters
                return utf8.Length == 1 ? utf8.ToUpper() : utf8;
            }
        }

        // Use hardcoded fallback only if XKB failed or returns nothing
        // Reverse mapping
        if (keyCode >= 59 && keyCode <= 82) return "F" + (keyCode - 59 + 1); // F1-F24
        
        // Digits
        if (keyCode == 11) return "0";
        if (keyCode >= 2 && keyCode <= 10) return (keyCode - 1).ToString();

        // Letters
        if (keyCode >= 16 && keyCode <= 25) return "QWERTYUIOP"[keyCode - 16].ToString();
        if (keyCode >= 30 && keyCode <= 38) return "ASDFGHJKL"[keyCode - 30].ToString();
        if (keyCode >= 44 && keyCode <= 50) return "ZXCVBNM"[keyCode - 44].ToString();

        return keyCode switch
        {
            51 => ",",
            52 => ".",
            12 => "-",
            13 => "=",
            39 => ";",
            40 => "'",
            26 => "[",
            27 => "]",
            43 => "\\",
            53 => "/",
            41 => "`",
            
            _ => $"Key{keyCode}"
        };
    }

    public int GetKeyCode(string keyName)
    {
        // Modifier keys
        if (keyName.Equals("Ctrl", StringComparison.OrdinalIgnoreCase))
            return 29; // KEY_LEFTCTRL
        if (keyName.Equals("Shift", StringComparison.OrdinalIgnoreCase))
            return 42; // KEY_LEFTSHIFT
        if (keyName.Equals("Alt", StringComparison.OrdinalIgnoreCase))
            return 56; // KEY_LEFTALT
        if (keyName.Equals("AltGr", StringComparison.OrdinalIgnoreCase))
            return 100; // KEY_RIGHTALT
        if (keyName.Equals("Super", StringComparison.OrdinalIgnoreCase) || keyName.Equals("Meta", StringComparison.OrdinalIgnoreCase))
            return 125; // KEY_LEFTMETA

        // Function keys
        if (keyName.StartsWith("F", StringComparison.OrdinalIgnoreCase) && int.TryParse(keyName[1..], out var fNum))
        {
            if (fNum >= 1 && fNum <= 24)
                return 59 + fNum - 1; // F1 = 59, F2 = 60, etc.
        }

        // Special keys overrides
        var special = keyName switch
        {
            "Space" => 57,
            "Enter" => 28,
            "Tab" => 15,
            "Backspace" => 14,
            "Escape" or "Esc" => 1,
            "Delete" or "Del" => 111,
            "Insert" or "Ins" => 110,
            "Home" => 102,
            "End" => 107,
            "PageUp" or "PgUp" => 104,
            "PageDown" or "PgDn" => 109,
            "Up" => 103,
            "Down" => 108,
            "Left" => 105,
            "Right" => 106,
            _ => -1
        };
        if (special != -1) return special;

        // Try to reverse match using GetKeyName (which now uses XKB)
        // This handles "Ö", "Ş", etc.
        for (int i = 0; i < 256; i++)
        {
            var name = GetKeyName(i);
            if (string.Equals(name, keyName, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        // Fallback for hardcoded Latin layout if XKB fails
        // Letter keys (QWERTY layout mapping)
        if (keyName.Length == 1 && char.IsLetter(keyName[0]))
        {
            return char.ToUpper(keyName[0]) switch
            {
                'Q' => 16, 'W' => 17, 'E' => 18, 'R' => 19, 'T' => 20, 'Y' => 21, 'U' => 22, 'I' => 23, 'O' => 24, 'P' => 25,
                'A' => 30, 'S' => 31, 'D' => 32, 'F' => 33, 'G' => 34, 'H' => 35, 'J' => 36, 'K' => 37, 'L' => 38,
                'Z' => 44, 'X' => 45, 'C' => 46, 'V' => 47, 'B' => 48, 'N' => 49, 'M' => 50,
                _ => -1
            };
        }

        // Number keys (top row)
        if (keyName.Length == 1 && char.IsDigit(keyName[0]))
        {
            var digit = keyName[0] - '0';
            return digit == 0 ? 11 : 2 + digit - 1; // 1 = 2, 2 = 3, ..., 0 = 11
        }

        // Special keys characters
        return keyName switch
        {
            "," => 51,
            "." => 52,
            "-" => 12,
            "=" => 13,
            ";" => 39,
            "'" => 40,
            "[" => 26,
            "]" => 27,
            "\\" => 43,
            "/" => 53,
            "`" => 41,
            
            _ => -1
        };
    }

    // Modifier Indices
    private uint _modIndexShift;
    private uint _modIndexLock;
    private uint _modIndexAlt;
    private uint _modIndexAltGr;
    private uint _modIndexCtrl;

    public char? GetCharFromKeyCode(int keyCode, bool shift, bool altGr, bool capsLock)
    {
        // Don't produce chars for modifiers
        if (IsModifier(keyCode)) return null;

        // Space special case
        if (keyCode == 57) return ' ';

        // Try XKB first
        if (_xkbState != IntPtr.Zero)
        {
             lock (_lock) 
             {
                 // Reset state first
                 XkbNative.xkb_state_update_mask(_xkbState, 0, 0, 0, 0, 0, 0);

                 uint depressedMods = 0;
                 if (shift && _modIndexShift != XkbNative.XKB_MOD_INVALID) depressedMods |= (1u << (int)_modIndexShift);
                 if (altGr && _modIndexAltGr != XkbNative.XKB_MOD_INVALID) depressedMods |= (1u << (int)_modIndexAltGr);
                 
                 uint lockedMods = 0;
                 if (capsLock && _modIndexLock != XkbNative.XKB_MOD_INVALID) lockedMods |= (1u << (int)_modIndexLock);
                 
                 // Apply modifiers
                 XkbNative.xkb_state_update_mask(_xkbState, depressedMods, 0, lockedMods, 0, 0, 0);
                 
                 // Get char with modifiers applied
                 // Evdev keycode + 8 for XKB
                 var utf8 = XkbNative.GetUtf8String(_xkbState, (uint)(keyCode + 8));
                 
                 // Reset state back to clean for safety
                 XkbNative.xkb_state_update_mask(_xkbState, 0, 0, 0, 0, 0, 0);
                 
                 if (!string.IsNullOrEmpty(utf8) && utf8.Length == 1)
                 {
                     return utf8[0];
                 }
             }
        }
        
        return null; // Fallback
    }

    private Dictionary<char, (int KeyCode, bool Shift, bool AltGr)>? _charToInputCache;

    public (int KeyCode, bool Shift, bool AltGr)? GetInputForChar(char c)
    {
        lock (_lock)
        {
            if (_charToInputCache == null)
            {
                BuildCharInputCache();
            }

            return _charToInputCache!.TryGetValue(c, out var input) ? input : null;
        }
    }

    private void BuildCharInputCache()
    {
        _charToInputCache = new Dictionary<char, (int KeyCode, bool Shift, bool AltGr)>();

        // Scan codes 1-255 using the existing XKB state
        // Higher keycodes (multimedia etc) are rarely used for chars
        for (int code = 1; code < 255; code++)
        {
            if (IsModifier(code)) continue;

            // Check all 4 modifier combinations
            // 1. None
            TryAddCharToCache(code, false, false);
            // 2. Shift
            TryAddCharToCache(code, true, false);
            // 3. AltGr
            TryAddCharToCache(code, false, true);
            // 4. Shift + AltGr
            TryAddCharToCache(code, true, true);
        }
    }

    private void TryAddCharToCache(int code, bool shift, bool altGr)
    {
        // Must use the internal method that assumes lock is held or works safely?
        // GetCharFromKeyCode takes a lock. We are already in a lock in GetInputForChar, but BuildCharInputCache is called from there.
        // Wait, GetCharFromKeyCode takes `lock (_lock)`. Use `Monitor.IsEntered` or split the method?
        // Actually GetCharFromKeyCode logic is small. I can refactor or just call it.
        // Recursive lock is allowed in C#, so calling GetCharFromKeyCode inside GetInputForChar's lock is fine.
        
        var c = GetCharFromKeyCode(code, shift, altGr, false);
        if (c.HasValue && !_charToInputCache!.ContainsKey(c.Value))
        {
            _charToInputCache[c.Value] = (code, shift, altGr);
        }
    }

    private void UpdateModifierIndices()
    {
        if (_xkbKeymap == IntPtr.Zero) return;
        
        _charToInputCache = null; // Invalidate cache on layout update

        _modIndexShift = GetModIndex("Shift");
        _modIndexLock = GetModIndex("Lock", "Caps_Lock"); 
        _modIndexCtrl = GetModIndex("Control", "Ctrl");
        _modIndexAlt = GetModIndex("Mod1", "Alt", "LAlt"); 
        
        // AltGr can have many names
        _modIndexAltGr = GetModIndex("ISO_Level3_Shift", "Mod5", "AltGr", "RAlt");
        
        // Debug
        Log.Information("[KeyboardLayoutService] Mod Indices - Shift: {Shift}, Lock: {Lock}, AltGr: {AltGr}", 
            _modIndexShift, _modIndexLock, _modIndexAltGr);
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
    
    private bool IsModifier(int keyCode)
    {
        return keyCode is 29 or 97 or 42 or 54 or 56 or 100 or 125 or 126;
    }
    
    public void Dispose()
    {
        if (_xkbState != IntPtr.Zero) XkbNative.xkb_state_unref(_xkbState);
        if (_xkbKeymap != IntPtr.Zero) XkbNative.xkb_keymap_unref(_xkbKeymap);
        if (_xkbContext != IntPtr.Zero) XkbNative.xkb_context_unref(_xkbContext);
    }
}
