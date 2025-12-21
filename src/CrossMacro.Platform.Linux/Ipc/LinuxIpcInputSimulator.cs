using System;
using System.Threading.Tasks;
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
    
    private const ushort ABS_X = 0x00;
    private const ushort ABS_Y = 0x01;
    
    private const ushort SYN_REPORT = 0x00;

    public void Initialize(int screenWidth = 0, int screenHeight = 0)
    {
        // Daemon initializes UInput lazy-loaded or on start. 
        // Resolution support would require protocol update.
        // For now, ignoring resolution, assuming relative movement mostly or default mapping.
        
        // Ensure connection
        if (!_client.IsConnected)
        {
            // Fix: Block until connected to avoid race condition where simulation starts before connection.
            // Using a timeout to prevent hanging the UI indefinitely if daemon is down.
            try 
            {
                 var connectTask = Task.Run(async () => 
                 {
                     try 
                     { 
                         await _client.ConnectAsync(System.Threading.CancellationToken.None);
                         // If we have resolution, send it
                         if (screenWidth > 0 && screenHeight > 0)
                         {
                             _client.ConfigureResolution(screenWidth, screenHeight);
                         }
                     }
                     catch (Exception ex)
                     {
                         if (ex is System.IO.IOException || 
                             ex.InnerException is System.IO.IOException ||
                             ex.InnerException is System.Net.Sockets.SocketException)
                         {
                             Log.Warning("[LinuxIpcInputSimulator] Connection rejected by daemon. Polkit authorization was denied or timed out.");
                         }
                         else
                         {
                             Log.Warning(ex, "[LinuxIpcInputSimulator] Failed to connect to daemon");
                         }
                     }
                 });
                 
                 // Wait up to 2 seconds
                 if (!connectTask.Wait(2000))
                 {
                     Log.Warning("[LinuxIpcInputSimulator] Daemon connection timeout (2s)");
                 }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[LinuxIpcInputSimulator] Error during initialization");
            }
        }
        else if (screenWidth > 0 && screenHeight > 0)
        {
             // Already connected, just configure
             _client.ConfigureResolution(screenWidth, screenHeight);
        }
    }

    public void MoveAbsolute(int x, int y)
    {
        _client.SimulateEvent(EV_ABS, ABS_X, x);
        _client.SimulateEvent(EV_ABS, ABS_Y, y);
        Sync();
    }

    public void MoveRelative(int dx, int dy)
    {
        _client.SimulateEvent(EV_REL, REL_X, dx);
        _client.SimulateEvent(EV_REL, REL_Y, dy);
        Sync();
    }

    public void MouseButton(int button, bool pressed)
    {
        _client.SimulateEvent(EV_KEY, (ushort)button, pressed ? 1 : 0);
        Sync();
    }

    public void Scroll(int delta)
    {
         _client.SimulateEvent(EV_REL, REL_WHEEL, delta);
         Sync();
    }

    public void KeyPress(int keyCode, bool pressed)
    {
        _client.SimulateEvent(EV_KEY, (ushort)keyCode, pressed ? 1 : 0);
        Sync();
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
