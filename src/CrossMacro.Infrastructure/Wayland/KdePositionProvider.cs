using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using CrossMacro.Core.Wayland;
using CrossMacro.Infrastructure.Wayland.DBus;
using Serilog;
using Tmds.DBus;

namespace CrossMacro.Infrastructure.Wayland
{
    public class KdePositionProvider : IMousePositionProvider
    {
        private string? _scriptId;
        private string? _tempJsFile;
        private int _currentX;
        private int _currentY;
        private bool _hasPosition;
        private readonly object _lock = new();
        private readonly TaskCompletionSource<(int Width, int Height)> _resolutionTcs = new();
        private readonly System.Threading.CancellationTokenSource _cts = new();
        
        private Connection? _dbusConnection;
        private KdeTrackerService? _trackerService;
        
        // Event for notifying about dependency status
        public event EventHandler<string>? ExtensionStatusChanged;

        public string ProviderName => "KDE KWin Script (DBus)";
        public bool IsSupported { get; private set; }

        public KdePositionProvider()
        {
            // Check if we are running on KDE
            var currentDesktop = Environment.GetEnvironmentVariable("XDG_CURRENT_DESKTOP") ?? "";
            IsSupported = currentDesktop.Equals("KDE", StringComparison.OrdinalIgnoreCase);

            if (IsSupported)
            {
                // Check if qdbus is available
                if (!IsQdbusAvailable())
                {
                    Log.Warning("[KdePositionProvider] qdbus not found. Please install Qt tools package (qt5-tools, qt6-tools, or qttools5-dev-tools).");
                    IsSupported = false;
                    _resolutionTcs.TrySetResult((0, 0));
                    
                    // Notify UI about missing dependency
                    ExtensionStatusChanged?.Invoke(this, "qdbus not found. Please install Qt tools package (qt5-tools, qt6-tools, or qttools5-dev-tools).");
                    return;
                }

                StartTracking();
            }
            else
            {
                _resolutionTcs.TrySetResult((0, 0));
            }
        }

        private bool IsQdbusAvailable()
        {
            try
            {
                var which = Process.Start(new ProcessStartInfo
                {
                    FileName = "which",
                    Arguments = "qdbus",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
                });

                if (which != null)
                {
                    which.WaitForExit();
                    return which.ExitCode == 0;
                }

                return false;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[KdePositionProvider] Failed to check for qdbus availability");
                return false;
            }
        }

        private void StartTracking()
        {
            Task.Run(async () =>
            {
                try
                {
                    await InitializeAsync(_cts.Token);
                }
                catch (OperationCanceledException)
                {
                    // Normal shutdown
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "[KdePositionProvider] Failed to initialize tracking");
                    IsSupported = false;
                    _resolutionTcs.TrySetResult((0, 0));
                }
            });
        }

        private string GetSafeScriptPath(string fileName)
        {
            var localShare = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var scriptDir = Path.Combine(localShare, "crossmacro", "scripts");
            
            if (!Directory.Exists(scriptDir))
                Directory.CreateDirectory(scriptDir);
                
            return Path.Combine(scriptDir, fileName);
        }

        private async Task InitializeAsync(System.Threading.CancellationToken ct)
        {
            try 
            {
                // 1. Initialize DBus Service
                Log.Information("[KdePositionProvider] Initializing DBus service...");
                _dbusConnection = new Connection(Address.Session);
                await _dbusConnection.ConnectAsync();
                
                _trackerService = new KdeTrackerService(OnPositionUpdate, OnResolutionUpdate);
                await _dbusConnection.RegisterObjectAsync(_trackerService);
                
                // Register the service name so KWin can find it
                await _dbusConnection.RegisterServiceAsync("org.crossmacro.Tracker");
                Log.Information("[KdePositionProvider] DBus service registered at org.crossmacro.Tracker");

                ct.ThrowIfCancellationRequested();

                // 2. Create KWin script with DBus calls
                _tempJsFile = GetSafeScriptPath($"kde_tracker_{Guid.NewGuid()}.js");
                
                await File.WriteAllTextAsync(_tempJsFile, @"
var dbusService = 'org.crossmacro.Tracker';
var dbusPath = '/Tracker';
var dbusInterface = 'org.crossmacro.Tracker';

console.error('[CrossMacro] Script started, attempting DBus connection...');

// Send Resolution
try {
    console.error('[CrossMacro] Sending resolution: ' + workspace.virtualScreenGeometry.width + 'x' + workspace.virtualScreenGeometry.height);
    callDBus(dbusService, dbusPath, dbusInterface, 'UpdateResolution', 
             workspace.virtualScreenGeometry.width, 
             workspace.virtualScreenGeometry.height);
    console.error('[CrossMacro] Resolution sent successfully');
} catch (e) {
    console.error('[CrossMacro] DBus Error (Res): ' + e);
}

// Start cursor tracking
var timer = new QTimer();
timer.interval = 1;  // 1ms interval for 1000Hz mouse support
var lastX = -1;
var lastY = -1;
var errorCount = 0;

timer.timeout.connect(function() {
    try {
        var x = workspace.cursorPos.x;
        var y = workspace.cursorPos.y;
        
        // Only send update if position changed
        if (x !== lastX || y !== lastY) {
            callDBus(dbusService, dbusPath, dbusInterface, 'UpdatePosition', x, y);
            lastX = x;
            lastY = y;
            errorCount = 0;
        }
    } catch (e) {
        errorCount++;
        if (errorCount <= 3) {
            console.error('[CrossMacro] DBus Error (Pos #' + errorCount + '): ' + e);
        }
    }
});
timer.start();
console.error('[CrossMacro] Position tracking started');
", ct);
                
                await Task.Delay(200, ct);

                // 3. Load KWin script
                var loadProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "qdbus",
                        Arguments = $"org.kde.KWin /Scripting org.kde.kwin.Scripting.loadScript {_tempJsFile}",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
                    }
                };
                
                loadProcess.Start();
                _scriptId = (await loadProcess.StandardOutput.ReadToEndAsync(ct)).Trim();
                var loadError = await loadProcess.StandardError.ReadToEndAsync(ct);
                await loadProcess.WaitForExitAsync(ct);

                if (!string.IsNullOrEmpty(loadError))
                {
                    Log.Error("[KdePositionProvider] KWin script load error: {Error}", loadError);
                }

                if (string.IsNullOrEmpty(_scriptId) || !int.TryParse(_scriptId, out _))
                {
                    Log.Error("[KdePositionProvider] Failed to load KWin script. Invalid ID: '{ScriptId}'", _scriptId);
                    IsSupported = false;
                    _resolutionTcs.TrySetResult((0, 0));
                    return;
                }

                Log.Information("[KdePositionProvider] KWin script loaded with ID: {ScriptId}", _scriptId);

                // 4. Run script
                var runProcess = Process.Start(new ProcessStartInfo
                {
                    FileName = "qdbus",
                    Arguments = $"org.kde.KWin /Scripting/Script{_scriptId} run",
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
                });
                
                if (runProcess != null)
                {
                    await runProcess.WaitForExitAsync(ct);
                }

                Log.Information("[KdePositionProvider] Tracking started successfully via DBus");
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[KdePositionProvider] Initialization failed");
                IsSupported = false;
                _resolutionTcs.TrySetResult((0, 0));
            }
        }

        private void OnPositionUpdate(int x, int y)
        {
            lock (_lock)
            {
                _currentX = x;
                _currentY = y;
                _hasPosition = true;
            }
        }

        private void OnResolutionUpdate(int width, int height)
        {
            Log.Information("[KdePositionProvider] Resolution received via DBus: {W}x{H}", width, height);
            _resolutionTcs.TrySetResult((width, height));
        }

        public Task<(int X, int Y)?> GetAbsolutePositionAsync()
        {
            if (!IsSupported)
                return Task.FromResult<(int X, int Y)?>(null);

            lock (_lock)
            {
                if (!_hasPosition)
                    return Task.FromResult<(int X, int Y)?>(null);
                    
                return Task.FromResult<(int X, int Y)?>( (_currentX, _currentY) );
            }
        }

        public async Task<(int Width, int Height)?> GetScreenResolutionAsync()
        {
            if (!IsSupported)
                return null;

            var completedTask = await Task.WhenAny(_resolutionTcs.Task, Task.Delay(2000));
            
            if (completedTask == _resolutionTcs.Task)
            {
                var res = await _resolutionTcs.Task;
                if (res.Width > 0 && res.Height > 0)
                    return res;
            }
            
            Log.Warning("[KdePositionProvider] Resolution detection timed out. Using fallback (5120x1440).");
            return (5120, 1440);
        }



        public void Dispose()
        {
            _cts.Cancel();
            _cts.Dispose();

            // Stop script
            if (!string.IsNullOrEmpty(_scriptId) && int.TryParse(_scriptId, out _))
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "qdbus",
                        Arguments = $"org.kde.KWin /Scripting/Script{_scriptId} stop",
                        CreateNoWindow = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
                    })?.WaitForExit();

                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "qdbus",
                        Arguments = $"org.kde.KWin /Scripting org.kde.kwin.Scripting.unloadScript {_scriptId}",
                        CreateNoWindow = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
                    })?.WaitForExit();
                }
                catch { }
            }

            // Clean up DBus
            _dbusConnection?.Dispose();

            if (_tempJsFile != null && File.Exists(_tempJsFile))
            {
                try { File.Delete(_tempJsFile); } catch { }
            }
        }
    }
}
