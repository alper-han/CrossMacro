namespace CrossMacro.Platform.Abstractions;

/// <summary>
/// Carries an input capture provider error message.
/// </summary>
public sealed class InputCaptureErrorEventArgs : EventArgs
{
    public string Message { get; }

    public InputCaptureErrorEventArgs(string message)
    {
        Message = message;
    }
}
