namespace CrossMacro.Platform.Abstractions;

/// <summary>
/// Carries a critical global hotkey service error message.
/// </summary>
public sealed class GlobalHotkeyErrorEventArgs : EventArgs
{
    public string Message { get; }

    public GlobalHotkeyErrorEventArgs(string message)
    {
        Message = message;
    }
}
