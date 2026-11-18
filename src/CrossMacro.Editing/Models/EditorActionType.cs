namespace CrossMacro.Editing.Models;

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
    /// Wait for a fixed or random duration, stored in microseconds.
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
    /// Restored only when the macro includes an explicit TextInput boundary.
    /// </summary>
    TextInput,

    /// <summary>
    /// Set or update a script variable (name in ScriptVariableName, value in ScriptValue).
    /// </summary>
    SetVariable,

    /// <summary>
    /// Increase a numeric script variable by ScriptNumericValue.
    /// </summary>
    IncrementVariable,

    /// <summary>
    /// Decrease a numeric script variable by ScriptNumericValue.
    /// </summary>
    DecrementVariable,

    /// <summary>
    /// Multiply a numeric script variable by ScriptNumericValue (a number or a $variable reference).
    /// </summary>
    MultiplyVariable,

    /// <summary>
    /// Divide a numeric script variable by ScriptNumericValue (a number or a $variable reference).
    /// </summary>
    DivideVariable,

    /// <summary>
    /// Repeat block start; the count is a number or a $variable reference.
    /// </summary>
    RepeatBlockStart,

    /// <summary>
    /// If block start; the condition is held in the script left/right operand and operator fields.
    /// </summary>
    IfBlockStart,

    /// <summary>
    /// Else block start.
    /// </summary>
    ElseBlockStart,

    /// <summary>
    /// While block start; the condition is held in the script left/right operand and operator fields.
    /// </summary>
    WhileBlockStart,

    /// <summary>
    /// For block start (variable, from, to and optional step).
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
    /// Reads one screen pixel color into a script variable.
    /// </summary>
    PixelColor,

    /// <summary>
    /// Waits until one screen pixel matches a target color.
    /// </summary>
    WaitColor,

    /// <summary>
    /// Searches a screen region for a target color.
    /// </summary>
    PixelSearch,

    /// <summary>
    /// Searches a screen region for a named image asset.
    /// </summary>
    ImageSearch,

    /// <summary>
    /// Searches a screen region for a named image asset and clicks its center.
    /// </summary>
    ImageClick,

    /// <summary>
    /// Waits until a named image asset appears in a screen region.
    /// </summary>
    WaitImage,

    /// <summary>
    /// Reads clipboard text into a script variable.
    /// </summary>
    ClipboardGet,

    /// <summary>
    /// Writes text to the clipboard after script variable substitution.
    /// </summary>
    ClipboardSet,

    /// <summary>
    /// Runs a shell command, optionally with stdin and capture targets.
    /// </summary>
    ShellCommand,

    /// <summary>
    /// Captures a screenshot to a file and/or the clipboard.
    /// </summary>
    Screenshot,

    /// <summary>
    /// Runs a window-management script command with typed editor fields.
    /// </summary>
    WindowCommand,

    /// <summary>
    /// Raw run-script line that could not be mapped to a structured editor action.
    /// Preserved to avoid data loss during round-trip save.
    /// </summary>
    RawScriptStep,

    /// <summary>
    /// Captures the current mouse position into script variables.
    /// </summary>
    MousePosition,

    /// <summary>
    /// Sends a copy shortcut, then stores the selected clipboard text in a script variable.
    /// </summary>
    CopySelectionToVariable,
}
