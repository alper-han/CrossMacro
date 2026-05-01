using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Core.Logging;
using CrossMacro.Infrastructure.Linux.Native.UInput;

namespace CrossMacro.Infrastructure.Linux.Native.Evdev;

public class EvdevReader : IDisposable
{
    private readonly string _devicePath;
    private int _fd;
    private CancellationTokenSource? _cts;
    private Task? _readTask;
    private bool _syncing;
    private byte[]? _lastKeyState;

    public string DeviceName { get; }

    public event Action<EvdevReader, UInputNative.input_event>? EventReceived;
    public event Action<Exception>? ErrorOccurred;

    public bool IsListening { get; private set; }

    public EvdevReader(string devicePath, string deviceName)
    {
        _devicePath = devicePath;
        DeviceName = deviceName;
        _fd = -1;
    }

    public void Start()
    {
        if (IsListening) return;

        _fd = EvdevNative.open(_devicePath, EvdevNative.O_RDONLY);
        if (_fd < 0)
        {
            Log.Error("[EvdevReader] Failed to open device {Path} - Check permissions (need input group)", _devicePath);
            throw new InvalidOperationException($"Failed to open device {_devicePath}. Check permissions (need input group).");
        }

        _cts = new CancellationTokenSource();
        IsListening = true;
        _readTask = Task.Run(() => ReadLoop(_cts.Token));

        Log.Debug("[EvdevReader] Started reading from {Device} ({Path})", DeviceName, _devicePath);
    }

    public void Stop()
    {
        if (!IsListening) return;

        _cts?.Cancel();
        CloseDevice();

        try
        {
            _readTask?.Wait(200);
        }
        catch (AggregateException)
        {
        }

        IsListening = false;
        Log.Debug("[EvdevReader] Stopped reading from {Device}", DeviceName);
    }

    private void CloseDevice()
    {
        if (_fd >= 0)
        {
            EvdevNative.close(_fd);
            _fd = -1;
        }
    }

    private void ReadLoop(CancellationToken token)
    {
        int eventSize = Marshal.SizeOf<UInputNative.input_event>();
        IntPtr buffer = Marshal.AllocHGlobal(eventSize);

        try
        {
            while (!token.IsCancellationRequested && _fd >= 0)
            {
                IntPtr bytesRead = EvdevNative.read(_fd, buffer, (IntPtr)eventSize);

                if (bytesRead.ToInt64() == eventSize)
                {
                    var ev = Marshal.PtrToStructure<UInputNative.input_event>(buffer);

                    if (ev.type == UInputNative.EV_SYN && ev.code == UInputNative.SYN_DROPPED)
                    {
                        _syncing = true;
                        Log.Warning("[{Device}] SYN_DROPPED: Events lost, waiting for SYN_REPORT to resync", DeviceName);
                        continue;
                    }

                    if (ev.type == UInputNative.EV_SYN && ev.code == UInputNative.SYN_REPORT)
                    {
                        if (_syncing)
                        {
                            ResyncKeyState();
                            _syncing = false;
                        }
                        EventReceived?.Invoke(this, ev);
                        continue;
                    }

                    if (_syncing)
                        continue;

                    EventReceived?.Invoke(this, ev);
                }
                else if (bytesRead.ToInt64() < 0)
                {
                    var errno = Marshal.GetLastWin32Error();

                    if (errno == 9)
                    {
                        break;
                    }

                    if (errno == 4)
                    {
                        continue;
                    }

                    throw new System.IO.IOException($"Read error: {errno}");
                }
                else if (bytesRead.ToInt64() == 0)
                {
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            if (!token.IsCancellationRequested)
            {
                ErrorOccurred?.Invoke(ex);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private void ResyncKeyState()
    {
        byte[] currentKeyState = new byte[96];
        int result = EvdevNative.ioctl(_fd, EvdevNative.EVIOCGKEY, currentKeyState);

        if (result < 0)
        {
            Log.Warning("[{Device}] Failed to read key state during resync (errno: {Errno})",
                DeviceName, Marshal.GetLastWin32Error());
            return;
        }

        if (_lastKeyState == null)
        {
            _lastKeyState = new byte[96];
            EmitCurrentKeyState(currentKeyState);
            Array.Copy(currentKeyState, _lastKeyState, 96);
            Log.Debug("[{Device}] Initial key state sync completed", DeviceName);
            return;
        }

        for (int keyCode = 0; keyCode < 768; keyCode++)
        {
            int byteIndex = keyCode / 8;
            int bitIndex = keyCode % 8;

            if (byteIndex >= currentKeyState.Length)
                continue;

            bool currentlyPressed = (currentKeyState[byteIndex] & (1 << bitIndex)) != 0;
            bool wasPressed = (_lastKeyState[byteIndex] & (1 << bitIndex)) != 0;

            if (currentlyPressed != wasPressed)
            {
                var ev = new UInputNative.input_event
                {
                    type = UInputNative.EV_KEY,
                    code = (ushort)keyCode,
                    value = currentlyPressed ? 1 : 0
                };
                EventReceived?.Invoke(this, ev);
            }
        }

        Array.Copy(currentKeyState, _lastKeyState, 96);
        Log.Debug("[{Device}] Resync completed after SYN_DROPPED", DeviceName);
    }

    private void EmitCurrentKeyState(byte[] keyState)
    {
        for (int keyCode = 0; keyCode < 768; keyCode++)
        {
            int byteIndex = keyCode / 8;
            int bitIndex = keyCode % 8;

            if (byteIndex >= keyState.Length)
                continue;

            bool pressed = (keyState[byteIndex] & (1 << bitIndex)) != 0;

            if (pressed)
            {
                var ev = new UInputNative.input_event
                {
                    type = UInputNative.EV_KEY,
                    code = (ushort)keyCode,
                    value = 1
                };
                EventReceived?.Invoke(this, ev);
            }
        }
    }

    public void Dispose()
    {
        Stop();
        _cts?.Dispose();
    }
}
