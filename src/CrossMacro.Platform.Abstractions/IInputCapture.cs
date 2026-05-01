using System;
using System.Threading;
using System.Threading.Tasks;

namespace CrossMacro.Platform.Abstractions;

public interface IInputCapture : IDisposable
{
    string ProviderName { get; }

    bool IsSupported { get; }

    event EventHandler<InputCaptureEventArgs>? InputReceived;

    event EventHandler<string>? Error;

    void Configure(bool captureMouse, bool captureKeyboard);

    Task StartAsync(CancellationToken ct);

    void Stop();
}
