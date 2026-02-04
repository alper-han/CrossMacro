using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Platform.Linux.Native.UInput;
using Serilog;

namespace CrossMacro.Platform.Linux.Native.Evdev;

public class EvdevReader : IDisposable
{
    private readonly string _devicePath;
    private int _fd;
    private CancellationTokenSource? _cts;
    private Task? _readTask;

    // SYN_DROPPED handling
    private bool _syncing = false;
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
            throw new InvalidOperationException($"Failed to open device {_devicePath}. Check permissions (need input group).");
        }

        _cts = new CancellationTokenSource();
        IsListening = true;
        _readTask = Task.Run(() => ReadLoop(_cts.Token));
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
        catch (AggregateException) { }

        IsListening = false;
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

                    // SYN_DROPPED: Events were lost due to buffer overflow
                    if (ev.type == UInputNative.EV_SYN && ev.code == UInputNative.SYN_DROPPED)
                    {
                        _syncing = true;
                        Log.Warning("[{Device}] SYN_DROPPED: Events lost, waiting for SYN_REPORT to resync", DeviceName);
                        continue;
                    }

                    // SYN_REPORT received - resync if needed
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

                    // Skip all events while syncing (they are from corrupted stream)
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

    /// <summary>
    /// Resync key/button state after SYN_DROPPED by reading current state via EVIOCGKEY
    /// </summary>
    private void ResyncKeyState()
    {
        // KEY_MAX (0x2FF = 767) requires 96 bytes: (767 / 8) + 1 = 96
        byte[] currentKeyState = new byte[96];
        int result = EvdevNative.ioctl(_fd, EvdevNative.EVIOCGKEY, currentKeyState);

        if (result < 0)
        {
            Log.Warning("[{Device}] Failed to read key state during resync (errno: {Errno})",
                DeviceName, Marshal.GetLastWin32Error());
            return;
        }

        // Initialize last state on first resync
        if (_lastKeyState == null)
        {
            _lastKeyState = new byte[96];
            // On first resync, emit current state for all pressed keys
            EmitCurrentKeyState(currentKeyState);
            Array.Copy(currentKeyState, _lastKeyState, 96);
            Log.Information("[{Device}] Initial key state sync completed", DeviceName);
            return;
        }

        // Compare and emit events for changed keys
        // KEY_MAX = 0x2FF = 767, so we check 768 key codes (0-767)
        for (int keyCode = 0; keyCode < 768; keyCode++)
        {
            int byteIndex = keyCode / 8;
            int bitIndex = keyCode % 8;

            if (byteIndex >= currentKeyState.Length)
                continue;

            bool currentlyPressed = (currentKeyState[byteIndex] & (1 << bitIndex)) != 0;
            bool wasPressed = (_lastKeyState[byteIndex] & (1 << bitIndex)) != 0;

            // Emit event only if state changed
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

        // Update last known state
        Array.Copy(currentKeyState, _lastKeyState, 96);
        Log.Information("[{Device}] Resync completed after SYN_DROPPED", DeviceName);
    }

    /// <summary>
    /// Emit current state for all pressed keys (used on initial sync)
    /// </summary>
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
