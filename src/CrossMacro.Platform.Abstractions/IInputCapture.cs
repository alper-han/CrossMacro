
namespace CrossMacro.Platform.Abstractions;

public interface IInputCapture : IDisposable
{
    public string ProviderName { get; }

    public bool IsSupported { get; }

    public event EventHandler<CapturedInputEventArgs>? InputReceived;

    public event EventHandler<InputCaptureErrorEventArgs>? CaptureError;

    public void Configure(bool captureMouse, bool captureKeyboard);

    public Task StartAsync(CancellationToken ct);

    public void StopCapture();
}
