namespace CrossMacro.Platform.Abstractions;

/// <summary>
/// Carries a critical global hotkey service error message.
/// </summary>
public sealed class GlobalHotkeyErrorEventArgs(string message) : EventArgs
{
    public string Message { get; } = message;
}
