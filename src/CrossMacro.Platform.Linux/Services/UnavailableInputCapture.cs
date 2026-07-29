
namespace CrossMacro.Platform.Linux.Services;

public sealed class UnavailableInputCapture(string? failureMessage = null) : IInputCapture
{
    public const string DefaultFailureMessage = "No usable Linux input capture backend is available.";

    public string ProviderName => "Unavailable (No Linux Input Backend)";

    public string FailureMessage { get; } = string.IsNullOrWhiteSpace(failureMessage)
            ? DefaultFailureMessage
            : failureMessage;

    public bool IsSupported => false;

    event EventHandler<CapturedInputEventArgs>? IInputCapture.InputReceived
    {
        add => _ = value;
        remove => _ = value;
    }

    public event EventHandler<InputCaptureErrorEventArgs>? CaptureError;

    public void Configure(bool captureMouse, bool captureKeyboard) { /* Empty */ }

    public Task StartAsync(CancellationToken ct)
    {
        CaptureError?.Invoke(this, new InputCaptureErrorEventArgs(FailureMessage));
        return Task.FromException(new InvalidOperationException(FailureMessage));
    }

    public void StopCapture() { /* Empty */ }

    public void Dispose() { /* Empty */ }
}
