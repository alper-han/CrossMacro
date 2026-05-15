using System;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Core.Services;

namespace CrossMacro.Platform.Linux.Services;

public sealed class UnavailableInputCapture : IInputCapture
{
    public const string DefaultFailureMessage = "No usable Linux input capture backend is available.";

    public UnavailableInputCapture(string? failureMessage = null)
    {
        FailureMessage = string.IsNullOrWhiteSpace(failureMessage)
            ? DefaultFailureMessage
            : failureMessage;
    }

    public string ProviderName => "Unavailable (No Linux Input Backend)";

    public string FailureMessage { get; }

    public bool IsSupported => false;

#pragma warning disable CS0067 // Interface event contract; this implementation never raises input events.
    public event EventHandler<InputCaptureEventArgs>? InputReceived;
#pragma warning restore CS0067

    public event EventHandler<string>? Error;

    public void Configure(bool captureMouse, bool captureKeyboard)
    {
    }

    public Task StartAsync(CancellationToken ct)
    {
        Error?.Invoke(this, FailureMessage);
        return Task.FromException(new InvalidOperationException(FailureMessage));
    }

    public void Stop()
    {
    }

    public void Dispose()
    {
    }
}
