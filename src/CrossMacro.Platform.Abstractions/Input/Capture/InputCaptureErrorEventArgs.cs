namespace CrossMacro.Platform.Abstractions.Input.Capture;

/// <summary>
/// Carries an input capture provider error message.
/// </summary>
public sealed class InputCaptureErrorEventArgs(string message) : EventArgs
{
    public string Message { get; } = message ?? throw new ArgumentNullException(nameof(message));
}
