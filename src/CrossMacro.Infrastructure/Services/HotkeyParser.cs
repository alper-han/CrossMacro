using CrossMacro.Core.Services;
using Serilog;

namespace CrossMacro.Infrastructure.Services;

/// <summary>
/// Parses hotkey strings into structured HotkeyMapping objects.
/// </summary>
public class HotkeyParser : IHotkeyParser
{
    private readonly IKeyCodeMapper _keyCodeMapper;
    
    public HotkeyParser(IKeyCodeMapper keyCodeMapper)
    {
        _keyCodeMapper = keyCodeMapper;
    }
    
    public HotkeyMapping Parse(string hotkeyString)
    {
        var mapping = new HotkeyMapping();
        
        if (string.IsNullOrWhiteSpace(hotkeyString))
            return mapping;
        
        var parts = hotkeyString.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        
        foreach (var part in parts)
        {
            var keyCode = _keyCodeMapper.GetKeyCode(part);
            if (keyCode == -1)
            {
                Log.Warning("[HotkeyParser] Unknown key: {Key}", part);
                continue;
            }

            if (_keyCodeMapper.IsModifierKeyCode(keyCode))
            {
                mapping.RequiredModifiers.Add(keyCode);
            }
            else
            {
                mapping.MainKey = keyCode;
            }
        }

        return mapping;
    }
}
