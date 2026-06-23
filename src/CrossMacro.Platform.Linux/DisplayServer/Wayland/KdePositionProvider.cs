using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Core.Logging;
using CrossMacro.Core.Services;
using CrossMacro.Platform.Linux.DisplayServer.Wayland.DBus;
using Tmds.DBus.Protocol;

namespace CrossMacro.Platform.Linux.DisplayServer.Wayland
{
    public class KdePositionProvider : IMousePositionProvider
    {
        private static readonly string ScriptDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "crossmacro", "scripts");

        private static readonly TimeSpan ResolutionTimeout = TimeSpan.FromSeconds(2);
        private static readonly TimeSpan PositionTimeout = TimeSpan.FromSeconds(2);

        private string? _scriptId;
        private string? _tempJsFile;
        private int _currentX;
        private int _currentY;
        private bool _hasPosition;
        private readonly Lock _lock = new();
        private readonly TaskCompletionSource<(int X, int Y)> _positionTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<(int Width, int Height)> _resolutionTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly CancellationTokenSource _cts = new();
        private Task? _initializationTask;
        
        private DBusConnection? _dbusConnection;
        private KdeTrackerServiceMethodHandler? _trackerHandler;
        private KdeTrackerService? _trackerService;
        private int _disposed;

        public string ProviderName => "KDE KWin Script (DBus)";
        public bool IsSupported { get; private set; }

        public KdePositionProvider()
            : this(
                string.Equals(Environment.GetEnvironmentVariable("XDG_CURRENT_DESKTOP"), "KDE", StringComparison.OrdinalIgnoreCase),
                autoStartTracking: true)
        {
        }

        internal KdePositionProvider(bool isSupported, bool autoStartTracking)
        {
            IsSupported = isSupported;

            if (IsSupported && autoStartTracking)
            {
                StartTracking();
            }
            else if (!IsSupported)
            {
                _positionTcs.TrySetResult((0, 0));
                _resolutionTcs.TrySetResult((0, 0));
            }
        }



        private void StartTracking()
        {
            _initializationTask = Task.Run(async () =>
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
                    _positionTcs.TrySetResult((0, 0));
                    _resolutionTcs.TrySetResult((0, 0));
                }
            });
        }

        private static string GetSafeScriptPath(string fileName)
        {
            if (!Directory.Exists(ScriptDirectory))
                Directory.CreateDirectory(ScriptDirectory);
                
            return Path.Combine(ScriptDirectory, fileName);
        }

        internal void ApplyPositionUpdate(int x, int y)
        {
            if (IsDisposed)
            {
                return;
            }

            lock (_lock)
            {
                _currentX = x;
                _currentY = y;
                _hasPosition = true;
            }

            _positionTcs.TrySetResult((x, y));
        }

        internal void ApplyResolutionUpdate(int width, int height)
        {
            if (IsDisposed)
            {
                return;
            }

            Log.Information("[KdePositionProvider] Resolution received via DBus: {W}x{H}", width, height);
            _resolutionTcs.TrySetResult((width, height));
        }

        internal static async Task<(int Width, int Height)?> AwaitResolutionAsync(
            Task<(int Width, int Height)> resolutionTask,
            TimeSpan timeout,
            Func<TimeSpan, Task> delayAsync)
        {
            var completedTask = await Task.WhenAny(resolutionTask, delayAsync(timeout)).ConfigureAwait(false);

            if (completedTask == resolutionTask)
            {
                var resolution = await resolutionTask.ConfigureAwait(false);
                if (resolution.Width > 0 && resolution.Height > 0)
                {
                    return resolution;
                }
            }

            return null;
        }

        internal static async Task<(int X, int Y)?> AwaitPositionAsync(
            Task<(int X, int Y)> positionTask,
            TimeSpan timeout,
            Func<TimeSpan, Task> delayAsync)
        {
            var completedTask = await Task.WhenAny(positionTask, delayAsync(timeout)).ConfigureAwait(false);

            if (completedTask == positionTask)
            {
                return await positionTask.ConfigureAwait(false);
            }

            return null;
        }

        internal static void StopLoadedScript(
            string? scriptId,
            Func<string, Task> stopScriptAsync,
            Func<string, Task> unloadScriptAsync,
            Action<Exception> onError)
        {
            if (string.IsNullOrEmpty(scriptId) || !int.TryParse(scriptId, out _))
            {
                return;
            }

            try
            {
                stopScriptAsync(scriptId).GetAwaiter().GetResult();
                unloadScriptAsync(scriptId).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                onError(ex);
            }
        }

        internal static bool CleanupLoadedScriptIfShutdownRequested(
            bool disposed,
            CancellationToken cancellationToken,
            string? scriptId,
            Func<string, Task> stopScriptAsync,
            Func<string, Task> unloadScriptAsync,
            Action<Exception> onError)
        {
            if (!disposed && !cancellationToken.IsCancellationRequested)
            {
                return false;
            }

            StopLoadedScript(scriptId, stopScriptAsync, unloadScriptAsync, onError);
            return true;
        }

        internal bool IsDisposed => Volatile.Read(ref _disposed) != 0;

        private void ThrowIfDisposedOrCanceled(CancellationToken ct)
        {
            if (IsDisposed)
            {
                throw new OperationCanceledException(ct);
            }

            ct.ThrowIfCancellationRequested();
        }

        private void ThrowIfShutdownRequestedAfterScriptLoad(CancellationToken ct)
        {
            if (_dbusConnection == null)
            {
                throw new InvalidOperationException("DBus session was not initialized.");
            }

            if (CleanupLoadedScriptIfShutdownRequested(
                    IsDisposed,
                    ct,
                    _scriptId,
                    scriptId => new KWinScriptClient(_dbusConnection, scriptId).StopAsync(),
                    scriptId => new KWinScriptingClient(_dbusConnection).UnloadScriptAsync(scriptId),
                    ex => Log.Debug(ex, "[KdePositionProvider] Error stopping/unloading KWin script during shutdown")))
            {
                throw new OperationCanceledException(ct);
            }
        }

        private async Task InitializeAsync(System.Threading.CancellationToken ct)
        {
            try 
            {
                // 1. Initialize DBus Service
                Log.Information("[KdePositionProvider] Initializing DBus service...");
                _dbusConnection = LinuxDbusTransportBoundary.CreateSessionConnection();
                await _dbusConnection.ConnectAsync().AsTask().WaitAsync(ct).ConfigureAwait(false);
                ThrowIfDisposedOrCanceled(ct);
                
                _trackerService = new KdeTrackerService(ApplyPositionUpdate, ApplyResolutionUpdate);
                _trackerHandler = new KdeTrackerServiceMethodHandler(_trackerService);
                _dbusConnection.AddMethodHandler(_trackerHandler);
                await _dbusConnection
                    .RequestNameAsync(KdeTrackerService.TrackerServiceName, RequestNameOptions.Default)
                    .WaitAsync(ct)
                    .ConfigureAwait(false);
                Log.Information("[KdePositionProvider] DBus service registered at {ServiceName}", KdeTrackerService.TrackerServiceName);
                ThrowIfDisposedOrCanceled(ct);

                // 2. Create KWin script with DBus calls
                _tempJsFile = GetSafeScriptPath($"kde_tracker_{Guid.NewGuid()}.js");
                
                var scriptContent = BuildTrackerScriptContent();
                scriptContent = scriptContent.Replace("__TRACKER_OBJECT_PATH__", KdeTrackerService.TrackerObjectPath, StringComparison.Ordinal);
                await File.WriteAllTextAsync(_tempJsFile, scriptContent, ct);
                ThrowIfDisposedOrCanceled(ct);
                
                await Task.Delay(200, ct);
                ThrowIfDisposedOrCanceled(ct);

                // 3. Load KWin script
                try 
                {
                    Log.Information("[KdePositionProvider] Loading KWin script via DBus...");
                    if (_dbusConnection == null)
                    {
                        throw new InvalidOperationException("DBus session was not initialized.");
                    }

                    var scriptingProxy = new KWinScriptingClient(_dbusConnection);
                    var scriptIdInt = await scriptingProxy.LoadScriptAsync(_tempJsFile).WaitAsync(ct).ConfigureAwait(false);
                    _scriptId = scriptIdInt.ToString();
                    ThrowIfShutdownRequestedAfterScriptLoad(ct);
                    
                    if (string.IsNullOrEmpty(_scriptId) || scriptIdInt < 0)
                    {
                         Log.Error("[KdePositionProvider] Failed to load KWin script. Invalid ID: '{ScriptId}'", _scriptId);
                         IsSupported = false;
                         _positionTcs.TrySetResult((0, 0));
                         _resolutionTcs.TrySetResult((0, 0));
                         return;
                    }

                    Log.Information("[KdePositionProvider] KWin script loaded with ID: {ScriptId}", _scriptId);

                    // 4. Run script
                    var scriptProxy = new KWinScriptClient(_dbusConnection, _scriptId);
                    await scriptProxy.RunAsync().WaitAsync(ct).ConfigureAwait(false);
                    ThrowIfShutdownRequestedAfterScriptLoad(ct);
                    
                    Log.Information("[KdePositionProvider] Tracking started successfully via DBus");
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "[KdePositionProvider] DBus error during script loading/execution");
                    throw;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[KdePositionProvider] Initialization failed");
                IsSupported = false;
                _positionTcs.TrySetResult((0, 0));
                _resolutionTcs.TrySetResult((0, 0));
            }
        }

        internal static string BuildTrackerScriptContent()
        {
            return @"
var dbusService = 'io.github.alper_han.crossmacro.Tracker';
var dbusPath = '__TRACKER_OBJECT_PATH__';
var dbusInterface = 'io.github.alper_han.crossmacro.Tracker';

console.error('[CrossMacro] Script started, attempting DBus connection...');

var lastX = -1;
var lastY = -1;
var errorCount = 0;

// Send initial cursor position before any other startup calls so short-lived
// CLI commands such as `pixelcolor rel 0 0` have a cached position immediately.
try {
    var initialPos = workspace.cursorPos;
    callDBus(dbusService, dbusPath, dbusInterface, 'UpdatePosition', initialPos.x, initialPos.y);
    lastX = initialPos.x;
    lastY = initialPos.y;
} catch (e) {
    console.error('[CrossMacro] DBus Error (Initial Pos): ' + e);
}

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

// Start cursor tracking. KWin scripting reliably exposes QTimer here; do not
// depend on cursor-position change signals that are not available everywhere.
var timer = new QTimer();
timer.interval = 1;  // 1ms interval for 1000Hz mouse support

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
";
        }

        public async Task<(int X, int Y)?> GetAbsolutePositionAsync()
        {
            if (!IsSupported || IsDisposed)
                return null;

            lock (_lock)
            {
                if (_hasPosition)
                {
                    return (_currentX, _currentY);
                }
            }

            var position = await AwaitPositionAsync(
                _positionTcs.Task,
                PositionTimeout,
                timeout => Task.Delay(timeout)).ConfigureAwait(false);
            if (position == null || !IsSupported || IsDisposed)
            {
                return null;
            }

            lock (_lock)
            {
                return _hasPosition ? (_currentX, _currentY) : null;
            }
        }

        public async Task<(int Width, int Height)?> GetScreenResolutionAsync()
        {
            if (!IsSupported)
                return null;

            var resolution = await AwaitResolutionAsync(_resolutionTcs.Task, ResolutionTimeout, timeout => Task.Delay(timeout)).ConfigureAwait(false);
            if (resolution != null)
            {
                return resolution;
            }
            
            Log.Warning("[KdePositionProvider] Resolution detection timed out; downgrading to unknown resolution mode.");
            return null;
        }

        public void Dispose()
        {
            if (IsDisposed)
                return;

            Volatile.Write(ref _disposed, 1);
            _cts.Cancel();

            try
            {
                _initializationTask?.GetAwaiter().GetResult();
            }
            catch (OperationCanceledException)
            {
                // Expected when startup is canceled during disposal.
            }
            catch (AggregateException ex) when (ex.InnerException is OperationCanceledException)
            {
                // Expected when startup is canceled during disposal.
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "[KdePositionProvider] Initialization task failed during disposal");
            }

            // Stop script
            if (_dbusConnection != null)
            {
                StopLoadedScript(
                    _scriptId,
                    scriptId => new KWinScriptClient(_dbusConnection, scriptId).StopAsync(),
                    scriptId => new KWinScriptingClient(_dbusConnection).UnloadScriptAsync(scriptId),
                    ex => Log.Debug(ex, "[KdePositionProvider] Error stopping/unloading KWin script"));
            }

            // Clean up DBus
            _dbusConnection?.Dispose();
            _cts.Dispose();

            if (_tempJsFile != null && File.Exists(_tempJsFile))
            {
                try 
                { 
                    File.Delete(_tempJsFile); 
                } 
                catch (Exception ex)
                {
                    Log.Debug(ex, "[KdePositionProvider] Failed to delete temp script file: {File}", _tempJsFile);
                }
            }
        }
    }
}
