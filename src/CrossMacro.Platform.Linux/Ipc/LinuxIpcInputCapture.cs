using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Core.Services;
using Serilog;

namespace CrossMacro.Platform.Linux.Ipc;

public class LinuxIpcInputCapture : IInputCapture
{
    private readonly IpcClient _client;
    private bool _captureMouse = true;
    private bool _captureKeyboard = true;

    public string ProviderName => "Secure Daemon (Evdev)";

    public bool IsSupported => true; // If daemon is installed

    public event EventHandler<InputCaptureEventArgs>? InputReceived;
    public event EventHandler<string>? Error;

    public LinuxIpcInputCapture(IpcClient client)
    {
        _client = client;
        _client.InputReceived += (s, e) => InputReceived?.Invoke(this, e);
        _client.ErrorOccurred += (s, e) => Error?.Invoke(this, e);
    }

    public void Configure(bool captureMouse, bool captureKeyboard)
    {
        _captureMouse = captureMouse;
        _captureKeyboard = captureKeyboard;
    }

    public IReadOnlyList<InputDeviceInfo> GetAvailableDevices()
    {
        return
        [ 
            new InputDeviceInfo { Name = "All Daemon Devices", Path = "daemon", IsMouse = true, IsKeyboard = true } 
        ];
    }

    private bool _started;

    public async Task StartAsync(CancellationToken ct)
    {
        if (!_client.IsConnected)
        {
            try
            {
                await _client.ConnectAsync(ct);
            }
            catch (Exception ex)
            {
                var message = ex.Message;
                if (ex is System.IO.IOException || 
                    ex.InnerException is System.IO.IOException ||
                    ex.InnerException is System.Net.Sockets.SocketException)
                {
                    message = "Connection rejected by daemon. Polkit authorization was denied or timed out. (System details: " + ex.Message + ")";
                }
                
                Error?.Invoke(this, message);
                return;
            }
        }

        if (!_started)
        {
            _client.StartCapture(_captureMouse, _captureKeyboard);
            _started = true;
            Log.Information("[LinuxIpcInputCapture] Started capture via daemon");
        }


        try
        {
            await Task.Delay(-1, ct);
        }
        catch (TaskCanceledException)
        {
            Stop();
        }
    }

    public void Stop()
    {
        if (_ClientIsConnected() && _started)
        {
             _client.StopCapture();
             _started = false;
        }
    }
    
    private bool _ClientIsConnected() => _client?.IsConnected ?? false;

    public void Dispose()
    {
        Stop();
    }
}
