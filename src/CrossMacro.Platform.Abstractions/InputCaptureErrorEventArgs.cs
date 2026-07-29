namespace CrossMacro.Platform.Abstractions;

/// <summary>
/// Carries an input capture provider error message.
/// </summary>
public sealed class InputCaptureErrorEventArgs(string message) : EventArgs
{
    public string Message { get; } = message;
}
