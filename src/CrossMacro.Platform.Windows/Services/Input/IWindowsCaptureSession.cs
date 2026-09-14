namespace CrossMacro.Platform.Windows.Services.Input;

internal interface IWindowsCaptureSession : IInputCapture, IMouseCoordinateModeInputCapture
{
    public Task Completion { get; }
    public bool IsStopRequested { get; }
}
