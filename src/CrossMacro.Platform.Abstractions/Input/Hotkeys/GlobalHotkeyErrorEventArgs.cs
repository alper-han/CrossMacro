namespace CrossMacro.Platform.Abstractions.Input.Hotkeys;

/// <summary>
/// Carries a critical global hotkey service error message.
/// </summary>
public sealed class GlobalHotkeyErrorEventArgs(string message) : EventArgs
{
    public string Message { get; } = message;
}
