namespace CrossMacro.Core.Models;

/// <summary>
/// Types of actions available in the macro editor.
/// </summary>
public enum EditorActionType
{
    /// <summary>
    /// Move mouse to coordinates (absolute or relative based on IsAbsolute flag).
    /// </summary>
    MouseMove,
    
    /// <summary>
    /// Click mouse button (press + release).
    /// </summary>
    MouseClick,
    
    /// <summary>
    /// Press and hold mouse button.
    /// </summary>
    MouseDown,
    
    /// <summary>
    /// Release mouse button.
    /// </summary>
    MouseUp,
    
    /// <summary>
    /// Press and release a keyboard key.
    /// </summary>
    KeyPress,
    
    /// <summary>
    /// Press and hold a keyboard key.
    /// </summary>
    KeyDown,
    
    /// <summary>
    /// Release a keyboard key.
    /// </summary>
    KeyUp,
    
    /// <summary>
    /// Wait for specified milliseconds.
    /// </summary>
    Delay,
    
    /// <summary>
    /// Scroll vertically (up/down).
    /// </summary>
    ScrollVertical,
    
    /// <summary>
    /// Scroll horizontally (left/right).
    /// </summary>
    ScrollHorizontal,
    
    /// <summary>
    /// Type a sequence of characters as KeyPress events.
    /// Expands to multiple KeyPress events when saving.
    /// Consecutive KeyPress events are merged into this type when loading.
    /// </summary>
    TextInput,

    /// <summary>
    /// Set or update a script variable. Payload example: "i=0" or "name value".
    /// </summary>
    SetVariable,

    /// <summary>
    /// Increase numeric script variable. Payload example: "i" or "i 2".
    /// </summary>
    IncrementVariable,

    /// <summary>
    /// Decrease numeric script variable. Payload example: "i" or "i 2".
    /// </summary>
    DecrementVariable,

    /// <summary>
    /// Repeat block start. Payload example: "5" or "$n".
    /// </summary>
    RepeatBlockStart,

    /// <summary>
    /// If block start. Payload example: "$i < 10".
    /// </summary>
    IfBlockStart,

    /// <summary>
    /// Else block start.
    /// </summary>
    ElseBlockStart,

    /// <summary>
    /// While block start. Payload example: "$i < 10".
    /// </summary>
    WhileBlockStart,

    /// <summary>
    /// For block start. Payload example: "i from 1 to 10 step 1".
    /// </summary>
    ForBlockStart,

    /// <summary>
    /// Generic block end "}".
    /// </summary>
    BlockEnd,

    /// <summary>
    /// Breaks out of the nearest loop block.
    /// </summary>
    Break,

    /// <summary>
    /// Skips to the next iteration of the nearest loop block.
    /// </summary>
    Continue,

    /// <summary>
    /// Raw run-script line that could not be mapped to a structured editor action.
    /// Preserved to avoid data loss during round-trip save.
    /// </summary>
    RawScriptStep
}
