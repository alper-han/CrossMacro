using System;
using CrossMacro.Platform.Linux.Native.Evdev;
using CrossMacro.Platform.Linux.Native.UInput; // For UInputNative

namespace CrossMacro.Daemon.Services;

public readonly record struct CaptureStartResult(
    bool Success,
    int StartedDeviceCount,
    string? ErrorMessage = null)
{
    public static CaptureStartResult Started(int startedDeviceCount) =>
        new(true, startedDeviceCount);

    public static CaptureStartResult Failed(string errorMessage) =>
        new(false, 0, errorMessage);
}

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
