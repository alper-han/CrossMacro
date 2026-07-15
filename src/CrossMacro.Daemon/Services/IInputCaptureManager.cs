
namespace CrossMacro.Daemon.Services;

public interface IInputCaptureManager : IDisposable
{
    /// <summary>
    /// Starts capturing input from physical devices.
    /// </summary>
    /// <param name="captureMouse">Whether to capture mouse devices.</param>
    /// <param name="captureKeyboard">Whether to capture keyboard devices.</param>
    /// <param name="onEvent">Callback invoked for every captured event.</param>
    CaptureStartResult StartCapture(bool captureMouse, bool captureKeyboard, Action<UInputNative.input_event> onEvent);

    /// <summary>
    /// Stops any active capture.
    /// </summary>
    void StopCapture();
}
