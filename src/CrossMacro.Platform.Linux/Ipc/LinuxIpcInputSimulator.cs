using System;
using System.Threading;
using CrossMacro.Core.Services;
using Serilog;

namespace CrossMacro.Platform.Linux.Ipc;

public class LinuxIpcInputSimulator : IInputSimulator
{
    private readonly IpcClient _client;
    private bool _disposed;

    public string ProviderName => "Secure Daemon (UInput)";
    public bool IsSupported => true; // Assuming daemon handles checks

    public LinuxIpcInputSimulator(IpcClient client)
    {
        _client = client;
    }

    private const ushort EV_KEY = 0x01;
    private const ushort EV_REL = 0x02;
    private const ushort EV_ABS = 0x03;
    private const ushort EV_SYN = 0x00;
    
    private const ushort REL_X = 0x00;
    private const ushort REL_Y = 0x01;
    private const ushort REL_WHEEL = 0x08;
    private const ushort REL_HWHEEL = 0x06;
    
    private const ushort ABS_X = 0x00;
    private const ushort ABS_Y = 0x01;
    
    private const ushort SYN_REPORT = 0x00;
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(2);

    public void Initialize(int screenWidth = 0, int screenHeight = 0)
    {
        // Daemon initializes UInput lazy-loaded or on start. 
        // Resolution support would require protocol update.
        // For now, ignoring resolution, assuming relative movement mostly or default mapping.
        
        // Ensure connection
        if (!_client.IsConnected)
        {
            try
            {
                using var timeoutCts = new CancellationTokenSource(ConnectTimeout);
                _client.ConnectAsync(timeoutCts.Token).GetAwaiter().GetResult();
            }
            catch (OperationCanceledException)
            {
                Log.Warning("[LinuxIpcInputSimulator] Daemon connection timeout ({TimeoutMs}ms)", ConnectTimeout.TotalMilliseconds);
            }
            catch (IpcClientException ex) when (ex.Reason == IpcClientFailureReason.Timeout)
            {
                Log.Warning("[LinuxIpcInputSimulator] Daemon handshake timeout ({TimeoutMs}ms)", ConnectTimeout.TotalMilliseconds);
            }
            catch (IpcClientException ex)
            {
                Log.Warning(ex, "[LinuxIpcInputSimulator] Failed to connect to daemon ({Reason})", ex.Reason);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[LinuxIpcInputSimulator] Error during initialization");
            }
        }

        if (_client.IsConnected && screenWidth > 0 && screenHeight > 0)
        {
            _client.ConfigureResolution(screenWidth, screenHeight);
        }
    }

    public void MoveAbsolute(int x, int y)
    {
        Span<(ushort, ushort, int)> events = stackalloc (ushort, ushort, int)[]
        {
            (EV_ABS, ABS_X, x),
            (EV_ABS, ABS_Y, y),
            (EV_SYN, SYN_REPORT, 0)
        };
        _client.SimulateEvents(events);
    }

    public void MoveRelative(int dx, int dy)
    {
        Span<(ushort, ushort, int)> events = stackalloc (ushort, ushort, int)[]
        {
            (EV_REL, REL_X, dx),
            (EV_REL, REL_Y, dy),
            (EV_SYN, SYN_REPORT, 0)
        };
        _client.SimulateEvents(events);
    }

    public void MouseButton(int button, bool pressed)
    {
        Span<(ushort, ushort, int)> events = stackalloc (ushort, ushort, int)[]
        {
            (EV_KEY, (ushort)button, pressed ? 1 : 0),
            (EV_SYN, SYN_REPORT, 0)
        };
        _client.SimulateEvents(events);
    }

    public void Scroll(int delta, bool isHorizontal = false)
    {
         ushort axis = isHorizontal ? REL_HWHEEL : REL_WHEEL;
         Span<(ushort, ushort, int)> events = stackalloc (ushort, ushort, int)[]
        {
            (EV_REL, axis, delta),
            (EV_SYN, SYN_REPORT, 0)
        };
        _client.SimulateEvents(events);
    }

    public void KeyPress(int keyCode, bool pressed)
    {
        Span<(ushort, ushort, int)> events = stackalloc (ushort, ushort, int)[]
        {
            (EV_KEY, (ushort)keyCode, pressed ? 1 : 0),
            (EV_SYN, SYN_REPORT, 0)
        };
        _client.SimulateEvents(events);
    }

    public void Sync()
    {
        _client.SimulateEvent(EV_SYN, SYN_REPORT, 0);
    }
    
    public void Dispose()
    {
        if (!_disposed)
        {
            // Client lifecycle is likely external or shared
            _disposed = true;
        }
    }
}
