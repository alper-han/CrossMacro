using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Native.UInput;

namespace CrossMacro.Native.Evdev;

public class EvdevReader : IDisposable
{
    private readonly string _devicePath;
    private int _fd;
    private CancellationTokenSource? _cts;
    private Task? _readTask;

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
        
        // Close device BEFORE waiting - this will immediately unblock the blocking read() call
        CloseDevice();
        
        try
        {
            // Reduced timeout since device close immediately unblocks the read
            _readTask?.Wait(200);
        }
        catch (AggregateException) { /* Ignore cancellation */ }
        
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
                // Blocking read - will wait here until event arrives or FD is closed
                IntPtr bytesRead = EvdevNative.read(_fd, buffer, (IntPtr)eventSize);
                
                if (bytesRead.ToInt64() == eventSize)
                {
                    var ev = Marshal.PtrToStructure<UInputNative.input_event>(buffer);
                    EventReceived?.Invoke(this, ev);
                }
                else if (bytesRead.ToInt64() < 0)
                {
                    var errno = Marshal.GetLastWin32Error();
                    
                    // EBADF (9) means file descriptor was closed (likely by Stop())
                    if (errno == 9) 
                    {
                        break; // Exit loop gracefully
                    }
                    
                    // EINTR (4) - Interrupted system call, just retry
                    if (errno == 4) 
                    {
                        continue;
                    }

                    // Real error
                    throw new System.IO.IOException($"Read error: {errno}");
                }
                else if (bytesRead.ToInt64() == 0)
                {
                     // EOF
                     break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal cancellation
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

    public void Dispose()
    {
        Stop();
        _cts?.Dispose();
    }
}
