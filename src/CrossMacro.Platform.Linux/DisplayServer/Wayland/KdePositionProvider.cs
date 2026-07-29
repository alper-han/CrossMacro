
namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

public sealed class KdePositionProvider : IMousePositionProvider, IAsyncDisposable
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
    private int _disposed;

    public string ProviderName => "KDE KWin Script (DBus)";
    public bool IsSupported { get; private set; }

    public KdePositionProvider()
        : this(LinuxEnvironmentVariables.CaptureCurrentSnapshot()) { /* Empty */ }

    public KdePositionProvider(LinuxEnvironmentSnapshot environment)
        : this(
            !string.IsNullOrEmpty(environment.CurrentDesktop) &&
            (environment.CurrentDesktop.Contains("KDE", StringComparison.OrdinalIgnoreCase) ||
             environment.CurrentDesktop.Contains("PLASMA", StringComparison.OrdinalIgnoreCase)),
            autoStartTracking: true)
    { /* Empty */ }

    internal KdePositionProvider(bool isSupported, bool autoStartTracking)
    {
        IsSupported = isSupported;

        if (IsSupported && autoStartTracking)
        {
            StartTracking();
        }
        else if (!IsSupported)
        {
            _ = _positionTcs.TrySetResult((0, 0));
            _ = _resolutionTcs.TrySetResult((0, 0));
        }
    }



    private void StartTracking()
    {
        _initializationTask = InitializeAsync(_cts.Token);
    }

    private static string GetSafeScriptPath(string fileName)
    {
        if (!Directory.Exists(ScriptDirectory))
        {
            _ = Directory.CreateDirectory(ScriptDirectory);
        }

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

        _ = _positionTcs.TrySetResult((x, y));
    }

    internal void ApplyResolutionUpdate(int width, int height)
    {
        if (IsDisposed)
        {
            return;
        }

        Log.Information("[KdePositionProvider] Resolution received via DBus: {W}x{H}", width, height);
        _ = _resolutionTcs.TrySetResult((width, height));
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

    internal static async Task StopLoadedScriptAsync(
        string? scriptId,
        Func<string, Task> stopScriptAsync,
        Func<string, Task> unloadScriptAsync,
        Action<Exception> onError,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(scriptId) || !int.TryParse(scriptId, CultureInfo.InvariantCulture, out _))
        {
            return;
        }

        try
        {
            await stopScriptAsync(scriptId).WaitAsync(cancellationToken).ConfigureAwait(false);
            await unloadScriptAsync(scriptId).WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            onError(ex);
        }
    }

    internal static async Task<bool> CleanupLoadedScriptIfShutdownRequestedAsync(
        bool disposed,
        string? scriptId,
        Func<string, Task> stopScriptAsync,
        Func<string, Task> unloadScriptAsync,
        Action<Exception> onError,
        CancellationToken cancellationToken)
    {
        if (!disposed && !cancellationToken.IsCancellationRequested)
        {
            return false;
        }

        await StopLoadedScriptAsync(scriptId, stopScriptAsync, unloadScriptAsync, onError, cancellationToken).ConfigureAwait(false);
        return true;
    }

    internal bool IsDisposed => Volatile.Read(ref _disposed) is not 0;

    private void ThrowIfDisposedOrCanceled(CancellationToken ct)
    {
        if (IsDisposed)
        {
            throw new OperationCanceledException(ct);
        }

        ct.ThrowIfCancellationRequested();
    }

    private async Task ThrowIfShutdownRequestedAfterScriptLoadAsync(CancellationToken ct)
    {
        if (_dbusConnection is null)
        {
            throw new InvalidOperationException("DBus session was not initialized.");
        }

        if (await CleanupLoadedScriptIfShutdownRequestedAsync(
            IsDisposed,
            _scriptId,
            scriptId => new KWinScriptClient(_dbusConnection, scriptId).StopAsync(),
            scriptId => new KWinScriptingClient(_dbusConnection).UnloadScriptAsync(scriptId),
            ex => Log.Debug(ex, "[KdePositionProvider] Error stopping/unloading KWin script during shutdown"),
            ct).ConfigureAwait(false))
        {
            throw new OperationCanceledException(ct);
        }
    }

    private async Task InitializeAsync(System.Threading.CancellationToken ct)
    {
        try
        {
            await InitializeDbusAndScriptAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.LogError(ex, "[KdePositionProvider] Initialization failed");
            IsSupported = false;
            _ = _positionTcs.TrySetResult((0, 0));
            _ = _resolutionTcs.TrySetResult((0, 0));
        }
    }

    private async Task InitializeDbusAndScriptAsync(System.Threading.CancellationToken ct)
    {
        try
        {
            Log.Information("[KdePositionProvider] Initializing DBus service...");
            _dbusConnection = LinuxDbusTransportBoundary.CreateSessionConnection();
            await _dbusConnection.ConnectAsync().AsTask().WaitAsync(ct).ConfigureAwait(false);
            ThrowIfDisposedOrCanceled(ct);

            var trackerService = new KdeTrackerService(ApplyPositionUpdate, ApplyResolutionUpdate);
            var trackerHandler = new KdeTrackerServiceMethodHandler(trackerService);
            _dbusConnection.AddMethodHandler(trackerHandler);
            await _dbusConnection
                .RequestNameAsync(KdeTrackerService.TrackerServiceName, RequestNameOptions.Default)
                .WaitAsync(ct)
                .ConfigureAwait(false);
            Log.Information("[KdePositionProvider] DBus service registered at {ServiceName}", KdeTrackerService.TrackerServiceName);
            ThrowIfDisposedOrCanceled(ct);

            await PrepareAndLoadTrackerScriptAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.LogError(ex, "[KdePositionProvider] DBus error during script loading/execution");
            throw;
        }
    }

    private async Task PrepareAndLoadTrackerScriptAsync(System.Threading.CancellationToken ct)
    {
        _tempJsFile = GetSafeScriptPath($"kde_tracker_{Guid.NewGuid()}.js");

        var scriptContent = TrackerScriptContent;
        scriptContent = scriptContent
            .Replace("__TRACKER_SERVICE_NAME__", KdeTrackerService.TrackerServiceName, StringComparison.Ordinal)
            .Replace("__TRACKER_OBJECT_PATH__", KdeTrackerService.TrackerObjectPath, StringComparison.Ordinal)
            .Replace("__TRACKER_INTERFACE__", KdeTrackerService.TrackerInterface, StringComparison.Ordinal);
        await File.WriteAllTextAsync(_tempJsFile, scriptContent, ct).ConfigureAwait(false);
        ThrowIfDisposedOrCanceled(ct);

        await Task.Delay(200, ct).ConfigureAwait(false);
        ThrowIfDisposedOrCanceled(ct);

        Log.Information("[KdePositionProvider] Loading KWin script via DBus...");
        if (_dbusConnection is null)
        {
            throw new InvalidOperationException("DBus session was not initialized.");
        }

        var scriptingProxy = new KWinScriptingClient(_dbusConnection);
        var scriptIdInt = await scriptingProxy.LoadScriptAsync(_tempJsFile).WaitAsync(ct).ConfigureAwait(false);
        _scriptId = scriptIdInt.ToString(CultureInfo.InvariantCulture);
        await ThrowIfShutdownRequestedAfterScriptLoadAsync(ct).ConfigureAwait(false);

        if (string.IsNullOrEmpty(_scriptId) || scriptIdInt < 0)
        {
            Log.LogError("[KdePositionProvider] Failed to load KWin script. Invalid ID: '{ScriptId}'", _scriptId);
            IsSupported = false;
            _ = _positionTcs.TrySetResult((0, 0));
            _ = _resolutionTcs.TrySetResult((0, 0));
            return;
        }

        Log.Information("[KdePositionProvider] KWin script loaded with ID: {ScriptId}", _scriptId);

        var scriptProxy = new KWinScriptClient(_dbusConnection, _scriptId);
        await scriptProxy.RunAsync().WaitAsync(ct).ConfigureAwait(false);
        await ThrowIfShutdownRequestedAfterScriptLoadAsync(ct).ConfigureAwait(false);

        Log.Information("[KdePositionProvider] Tracking started successfully via DBus");
    }

    internal static string BuildTrackerScriptContent() => TrackerScriptContent;

    private static readonly string TrackerScriptContent = LoadEmbeddedScript("CrossMacro.Platform.Linux.DisplayServer.Wayland.KdePositionProvider.js");

    private static string LoadEmbeddedScript(string resourceName)
    {
        using var stream = typeof(KdePositionProvider).Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{resourceName}' not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd().Replace("\r\n", "\n", StringComparison.Ordinal);
    }

    public async Task<(int X, int Y)?> GetAbsolutePositionAsync()
    {
        if (!IsSupported || IsDisposed)
        {
            return null;
        }

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
            timeout => Task.Delay(timeout, _cts.Token)).ConfigureAwait(false);
        if (position is null || !IsSupported || IsDisposed)
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
        if (!IsSupported || IsDisposed)
        {
            return null;
        }

        // Wait for initialization to complete before starting the resolution timeout.
        // InitializeAsync runs fire-and-forget; if we don't await it first, the 2 s
        // window starts counting before the KWin script has even been loaded.
        if (_initializationTask is { IsCompleted: false })
        {
            try
            {
                await _initializationTask.WaitAsync(_cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return null;
            }
            catch (ObjectDisposedException)
            {
                return null;
            }
        }

        if (!IsSupported || IsDisposed)
        {
            return null;
        }

        try
        {
            var resolution = await AwaitResolutionAsync(_resolutionTcs.Task, ResolutionTimeout, timeout => Task.Delay(timeout, _cts.Token)).ConfigureAwait(false);
            if (resolution is not null)
            {
                return resolution;
            }
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (ObjectDisposedException)
        {
            return null;
        }

        Log.Warning("[KdePositionProvider] Resolution detection timed out; downgrading to unknown resolution mode.");
        return null;
    }

    public async ValueTask DisposeAsync()
    {
        if (IsDisposed)
        {
            return;
        }

        Volatile.Write(ref _disposed, 1);
        await _cts.CancelAsync().ConfigureAwait(false);

        if (_initializationTask is not null)
        {
            try
            {
                await _initializationTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected when startup is canceled during disposal.
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                Log.Debug(ex, "[KdePositionProvider] Initialization task failed during disposal");
            }
        }

        // Stop script
        if (_dbusConnection is not null)
        {
            await StopLoadedScriptAsync(
                _scriptId,
                scriptId => new KWinScriptClient(_dbusConnection, scriptId).StopAsync(),
                scriptId => new KWinScriptingClient(_dbusConnection).UnloadScriptAsync(scriptId),
                ex => Log.Debug(ex, "[KdePositionProvider] Error stopping/unloading KWin script"),
                CancellationToken.None).ConfigureAwait(false);
        }

        // Clean up DBus
        _dbusConnection?.Dispose();
        _cts.Dispose();

        if (_tempJsFile is not null && File.Exists(_tempJsFile))
        {
            try
            {
                File.Delete(_tempJsFile);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                Log.Debug(ex, "[KdePositionProvider] Failed to delete temp script file: {File}", _tempJsFile);
            }
        }
        GC.SuppressFinalize(this);
    }

    public void Dispose()
    {
        if (IsDisposed)
        {
            return;
        }

        Volatile.Write(ref _disposed, 1);
        _cts.Cancel();
        _dbusConnection?.Dispose();
        _cts.Dispose();
        GC.SuppressFinalize(this);
    }
}
