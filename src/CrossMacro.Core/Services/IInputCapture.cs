using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CrossMacro.Core.Services;

public class InputDeviceInfo
{
    public string Path { get; set; } = "";
    public string Name { get; set; } = "";
    public bool IsMouse { get; set; }
    public bool IsKeyboard { get; set; }
    public bool IsTouchpad { get; set; }
}

public interface IInputCapture : IDisposable
{
    string ProviderName { get; }
    
    bool IsSupported { get; }
    
    event EventHandler<InputCaptureEventArgs>? InputReceived;
    
    event EventHandler<string>? Error;
    
    void Configure(bool captureMouse, bool captureKeyboard);
    
    IReadOnlyList<InputDeviceInfo> GetAvailableDevices();
    
    Task StartAsync(CancellationToken ct);
    
    void Stop();
}
