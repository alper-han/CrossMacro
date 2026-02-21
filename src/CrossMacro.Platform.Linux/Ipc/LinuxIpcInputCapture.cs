using System;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Core.Services;
using Serilog;

namespace CrossMacro.Platform.Linux.Ipc;

public class LinuxIpcInputCapture : IInputCapture
{
    private static int _captureInstanceSequence;
    private readonly IpcClient _client;
    private readonly string _consumerId;
    private readonly Lock _stateLock = new();
    private bool _captureMouse = true;
    private bool _captureKeyboard = true;
    private bool _started;
    private bool _disposed;

    public string ProviderName => "Secure Daemon (Evdev)";

    public bool IsSupported => true; // If daemon is installed

    public event EventHandler<InputCaptureEventArgs>? InputReceived;
    public event EventHandler<string>? Error;

    public LinuxIpcInputCapture(IpcClient client, string? consumerId = null)
    {
        _client = client;
        _consumerId = string.IsNullOrWhiteSpace(consumerId)
            ? $"linux-ipc-capture-{Interlocked.Increment(ref _captureInstanceSequence)}"
            : consumerId;

        _client.InputReceived += OnClientInputReceived;
        _client.ErrorOccurred += OnClientErrorOccurred;
    }

    public void Configure(bool captureMouse, bool captureKeyboard)
    {
        bool needsUpdate;

        lock (_stateLock)
        {
            ThrowIfDisposed();

            _captureMouse = captureMouse;
            _captureKeyboard = captureKeyboard;
            needsUpdate = _started;
        }

        if (needsUpdate)
        {
            _client.StartCapture(_consumerId, captureMouse, captureKeyboard);
        }
    }

    public async Task StartAsync(CancellationToken ct)
    {
        ThrowIfDisposed();

        if (!_client.IsConnected)
        {
            try
            {
                await _client.ConnectAsync(ct);
            }
            catch (Exception ex)
            {
                var message = ex.Message;
                if (ex is IpcClientException ipcEx && ipcEx.Reason == IpcClientFailureReason.Timeout)
                {
                    message = "Timed out while waiting for daemon handshake. Check that crossmacro.service is running and responsive.";
                }
                else if (ex is System.IO.IOException || 
                         ex.InnerException is System.IO.IOException ||
                         ex.InnerException is System.Net.Sockets.SocketException)
                {
                    message = "Connection rejected by daemon. Polkit authorization was denied or timed out. (System details: " + ex.Message + ")";
                }
                
                Error?.Invoke(this, message);
                return;
            }
        }

        bool shouldStart = false;
        bool captureMouse = false;
        bool captureKeyboard = false;

        lock (_stateLock)
        {
            if (!_started)
            {
                _started = true;
                shouldStart = true;
                captureMouse = _captureMouse;
                captureKeyboard = _captureKeyboard;
            }
        }

        if (shouldStart)
        {
            _client.StartCapture(_consumerId, captureMouse, captureKeyboard);
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
        bool wasStarted = false;

        lock (_stateLock)
        {
            if (_started)
            {
                _started = false;
                wasStarted = true;
            }
        }

        if (wasStarted)
        {
            _client.StopCapture(_consumerId);
        }
    }

    private void OnClientInputReceived(object? sender, InputCaptureEventArgs e)
    {
        InputReceived?.Invoke(this, e);
    }

    private void OnClientErrorOccurred(object? sender, string error)
    {
        Error?.Invoke(this, error);
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(LinuxIpcInputCapture));
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Stop();
        _client.InputReceived -= OnClientInputReceived;
        _client.ErrorOccurred -= OnClientErrorOccurred;
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
