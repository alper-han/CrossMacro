using CrossMacro.Core.Services;

namespace CrossMacro.Infrastructure.Services;

/// <summary>
/// Maps between mouse button codes and their human-readable names.
/// Uses Linux evdev BTN_* codes.
/// </summary>
public class MouseButtonMapper : IMouseButtonMapper
{
    // BTN_LEFT = 272, BTN_RIGHT = 273, etc.
    private const int BtnLeft = 272;
    private const int BtnRight = 273;
    private const int BtnMiddle = 274;
    private const int BtnSide = 275;
    private const int BtnExtra = 276;
    private const int BtnForward = 277;
    private const int BtnBack = 278;
    private const int BtnTask = 279;
    
    private static readonly Dictionary<int, string> CodeToName = new()
    {
        { BtnLeft, "Mouse Left" },
        { BtnRight, "Mouse Right" },
        { BtnMiddle, "Mouse Middle" },
        { BtnSide, "Mouse Side" },      // Often "back" button
        { BtnExtra, "Mouse Extra" },    // Often "forward" button
        { BtnForward, "Mouse Forward" },
        { BtnBack, "Mouse Back" },
        { BtnTask, "Mouse Task" }
    };
    
    private static readonly Dictionary<string, int> NameToCode;
    
    static MouseButtonMapper()
    {
        NameToCode = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var kvp in CodeToName)
        {
            NameToCode[kvp.Value] = kvp.Key;
        }
    }
    
    public string GetMouseButtonName(int buttonCode)
    {
        if (CodeToName.TryGetValue(buttonCode, out var name))
        {
            return name;
        }
        
        // Generic fallback for unknown buttons
        if (buttonCode >= BtnLeft && buttonCode <= BtnTask + 10)
        {
            return $"Mouse{buttonCode - BtnLeft + 1}";
        }
        
        return string.Empty;
    }
    
    public int GetButtonCode(string buttonName)
    {
        return NameToCode.TryGetValue(buttonName, out var code) ? code : -1;
    }
    
    public bool IsMouseButton(int code)
    {
        // BTN_LEFT (272) through BTN_TASK (279) and a few beyond
        return code >= BtnLeft && code <= BtnTask + 10;
    }
}
