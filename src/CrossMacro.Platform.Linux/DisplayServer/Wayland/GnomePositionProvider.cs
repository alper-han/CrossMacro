
namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

public sealed class GnomePositionProvider : IMousePositionProvider, IExtensionStatusNotifier
{
    // Embedded GNOME Shell Extension files - auto-installed/updated when needed
    private static readonly string EXTENSION_JS = LoadEmbeddedScript("CrossMacro.Platform.Linux.DisplayServer.Wayland.GnomePositionProvider.js");

    private static string LoadEmbeddedScript(string resourceName)
    {
        using var stream = typeof(GnomePositionProvider).Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{resourceName}' not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd().Replace("\r\n", "\n", StringComparison.Ordinal);
    }

    private const string METADATA_JSON = "{\n  \"name\": \"CrossMacro Integration\",\n  \"description\": \"Window management, screen capture, and cursor tracking for CrossMacro\",\n  \"uuid\": \"crossmacro@zynix.net\",\n  \"shell-version\": [ \"45\", \"46\", \"47\", \"48\", \"49\", \"50\", \"51\" ]\n}\n";
    private const string ExtensionUuid = "crossmacro@zynix.net";

    private static readonly string ExtensionPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".local", "share", "gnome-shell", "extensions", ExtensionUuid);
    private static readonly string ExtensionJsPath = Path.Combine(ExtensionPath, "extension.js");
    private static readonly string MetadataJsonPath = Path.Combine(ExtensionPath, "metadata.json");

    private LinuxDbusSession? _dbusSession;
    private GnomeTrackerClient? _trackerClient;
    private GnomeShellExtensionsClient? _extensionsClient;
    private readonly TaskCompletionSource<bool> _initializationTcs = new();
    private bool _isInitialized;
    private (int Width, int Height)? _cachedResolution;
    private bool _resolutionUnavailableLogged;
    private bool _disposed;

    public event EventHandler<ExtensionStatusChangedEventArgs>? ExtensionStatusUpdated;
    public event EventHandler<ExtensionStatusMessageEventArgs>? ExtensionStatusChanged;

    public ExtensionStatusChangedEventArgs? CurrentExtensionStatus { get; private set; }

    public Task<bool> InitializationTask => _initializationTcs.Task;

    public string ProviderName => "GNOME Shell Extension (DBus)";
    public bool IsSupported { get; private set; }

    public GnomePositionProvider()
        : this(LinuxEnvironmentVariables.CaptureCurrentSnapshot()) { /* Empty */ }

    public GnomePositionProvider(LinuxEnvironmentSnapshot environment)
    {
        var currentDesktop = environment.CurrentDesktop;
        var session = environment.GdmSession;

        IsSupported = (currentDesktop?.Contains("GNOME", StringComparison.OrdinalIgnoreCase) ?? false) ||
                      (session?.Contains("gnome", StringComparison.OrdinalIgnoreCase) ?? false);

        if (IsSupported)
        {
            _ = Task.Run(InitializeAsync, CancellationToken.None);
        }
        else
        {
            _initializationTcs.SetResult(false);
        }
    }

    private async Task EnsureExtensionInstalledAsync()
    {
        try
        {
            bool jsExisted = File.Exists(ExtensionJsPath);
            bool metadataExisted = File.Exists(MetadataJsonPath);
            bool wasFreshInstall = !jsExisted || !metadataExisted;

            if (wasFreshInstall)
            {
                Log.Information("[GnomePositionProvider] Installing GNOME Shell extension to {Path}", ExtensionPath);
            }

            _ = Directory.CreateDirectory(ExtensionPath);

            bool jsUpdated = await EnsureFileContentAsync(ExtensionJsPath, EXTENSION_JS).ConfigureAwait(false);
            bool metadataUpdated = await EnsureFileContentAsync(MetadataJsonPath, METADATA_JSON).ConfigureAwait(false);

            if (jsUpdated || metadataUpdated)
            {
                var action = wasFreshInstall ? "installed" : "updated";
                Log.Information("[GnomePositionProvider] Extension files {Action} successfully", action);
            }
            else
            {
                Log.Debug("[GnomePositionProvider] Extension files already up to date at {Path}", ExtensionPath);
            }

            // Wait for files to be fully written to disk
            const int maxWaitMs = 3000;
            var elapsedMs = 0;

            while (elapsedMs < maxWaitMs)
            {
                var jsInfo = new FileInfo(ExtensionJsPath);
                var metaInfo = new FileInfo(MetadataJsonPath);

                if (jsInfo.Exists && jsInfo.Length > 0 &&
                    metaInfo.Exists && metaInfo.Length > 0)
                {
                    Log.Debug("[GnomePositionProvider] Files verified on disk after {Ms}ms", elapsedMs);
                    break;
                }

                await Task.Delay(100, CancellationToken.None).ConfigureAwait(false);
                elapsedMs += 100;
            }

            if (elapsedMs >= maxWaitMs)
            {
                Log.Warning("[GnomePositionProvider] File verification timeout, proceeding anyway");
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.LogError(ex, "[GnomePositionProvider] Failed to install GNOME extension");
            PublishExtensionStatus(ExtensionStatusCode.Error, "Failed to install GNOME extension");
        }
    }

    internal static async Task<bool> EnsureFileContentAsync(string filePath, string expectedContent)
    {
        if (File.Exists(filePath))
        {
            var existingContent = await File.ReadAllTextAsync(filePath, CancellationToken.None).ConfigureAwait(false);
            if (string.Equals(existingContent, expectedContent, StringComparison.Ordinal))
            {
                return false;
            }
        }

        await File.WriteAllTextAsync(filePath, expectedContent, CancellationToken.None).ConfigureAwait(false);
        return true;
    }

    private async Task<bool> CheckExtensionEnabledAsync()
    {
        try
        {
            if (_extensionsClient is null)
            {
                return false;
            }

            return await IsExtensionEnabledAsync(() => _extensionsClient.GetExtensionInfoAsync(ExtensionUuid)).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.Debug(ex, "[GnomePositionProvider] Failed to check extension status via DBus");
            return false;
        }
    }

    private async Task<bool> EnableExtensionAsync()
    {
        try
        {
            if (_extensionsClient is null)
            {
                return false;
            }

            return await _extensionsClient.EnableExtensionAsync(ExtensionUuid).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.LogError(ex, "[GnomePositionProvider] Exception while trying to enable extension via DBus");
            return false;
        }
    }

    private async Task ValidateExtensionStatusAsync()
    {
        // Check if extension is enabled
        bool isEnabled = await CheckExtensionEnabledAsync().ConfigureAwait(false);

        if (!isEnabled)
        {
            Log.Information("[GnomePositionProvider] Extension is not enabled, attempting to enable via DBus...");

            // Try to enable it
            bool enableSuccess = await EnableExtensionAsync().ConfigureAwait(false);

            if (enableSuccess)
            {
                // Verify it's actually enabled now
                await Task.Delay(500, CancellationToken.None).ConfigureAwait(false); // Give it a moment
                isEnabled = await CheckExtensionEnabledAsync().ConfigureAwait(false);

                if (isEnabled)
                {
                    Log.Information("[GnomePositionProvider] Extension enabled and verified successfully via DBus");
                    PublishExtensionStatus(ExtensionStatusCode.Enabled, "GNOME extension enabled successfully");
                }
                else
                {
                    Log.Warning("[GnomePositionProvider] Extension enable command succeeded but verification failed");
                    NotifyExtensionIssue("GNOME extension requires logout/login to activate");
                }
            }
            else
            {
                Log.Warning("[GnomePositionProvider] Failed to enable extension automatically");
                NotifyExtensionIssue("Please enable GNOME extension manually or restart your session");
            }
        }
        else
        {
            Log.Debug("[GnomePositionProvider] Extension is already enabled");
            PublishExtensionStatus(ExtensionStatusCode.Enabled, "GNOME extension is already enabled");
        }
    }

    private void NotifyExtensionIssue(string message)
    {
        Log.Warning("[GnomePositionProvider] {Message}", message);
        PublishExtensionStatus(ExtensionStatusCode.Warning, message);
    }

    private void PublishExtensionStatus(ExtensionStatusCode code, string message)
    {
        var args = new ExtensionStatusChangedEventArgs(code, message);
        CurrentExtensionStatus = args;
        ExtensionStatusUpdated?.Invoke(this, args);
        ExtensionStatusChanged?.Invoke(this, new ExtensionStatusMessageEventArgs(message));
    }

    private async Task InitializeAsync()
    {
        LinuxDbusSession? dbusSession = null;

        try
        {
            // Ensure extension is installed before connecting
            // This runs on a background thread now, so it won't block startup
            await EnsureExtensionInstalledAsync().ConfigureAwait(false);

            if (_disposed)
            {
                _ = _initializationTcs.TrySetResult(false);
                return;
            }

            dbusSession = await LinuxDbusSession.ConnectAsync(CancellationToken.None).ConfigureAwait(false);

            if (_disposed)
            {
                dbusSession.Dispose();
                _ = _initializationTcs.TrySetResult(false);
                return;
            }

            _dbusSession = dbusSession;
            _extensionsClient = dbusSession.CreateGnomeShellExtensionsClient();
            _trackerClient = dbusSession.CreateGnomeTrackerClient();

            // Now that we are connected, check status via DBus
            await ValidateExtensionStatusAsync().ConfigureAwait(false);

            if (_disposed)
            {
                dbusSession.Dispose();
                _dbusSession = null;
                _extensionsClient = null;
                _trackerClient = null;
                _ = _initializationTcs.TrySetResult(false);
                return;
            }

            _isInitialized = true;
            _ = _initializationTcs.TrySetResult(true);
            Log.Information("[GnomePositionProvider] Connected to DBus service");
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            dbusSession?.Dispose();
            _dbusSession = null;
            _extensionsClient = null;
            _trackerClient = null;
            Log.LogError(ex, "[GnomePositionProvider] Failed to initialize DBus connection");
            IsSupported = false;
            _ = _initializationTcs.TrySetResult(false);
        }
    }

    private async Task<bool> EnsureInitializedAsync()
    {
        if (_disposed)
        {
            return false;
        }

        if (_isInitialized)
        {
            return true;
        }

        // Wait for initialization with timeout (only on first call)
        var completedTask = await Task.WhenAny(_initializationTcs.Task, Task.Delay(2000, CancellationToken.None)).ConfigureAwait(false);
        return completedTask == _initializationTcs.Task && await _initializationTcs.Task.ConfigureAwait(false);
    }

    public async Task<(int X, int Y)?> GetAbsolutePositionAsync()
    {
        if (!IsSupported || !await EnsureInitializedAsync().ConfigureAwait(false) || _trackerClient is null)
        {
            return null;
        }

        return await TryGetAbsolutePositionAsync(_trackerClient.GetPositionAsync).ConfigureAwait(false);
    }

    public async Task<(int Width, int Height)?> GetScreenResolutionAsync()
    {
        if (!IsSupported || !await EnsureInitializedAsync().ConfigureAwait(false) || _trackerClient is null)
        {
            return null;
        }

        var queryResult = await TryGetScreenResolutionAsync(
            _trackerClient.GetResolutionAsync,
            _cachedResolution,
            _resolutionUnavailableLogged).ConfigureAwait(false);

        if (!_resolutionUnavailableLogged && queryResult.ResolutionUnavailableLogged)
        {
            NotifyExtensionIssue("GNOME extension is installed but not active. Enable the CrossMacro GNOME extension manually or restart your session.");
        }

        _cachedResolution = queryResult.CachedResolution;
        _resolutionUnavailableLogged = queryResult.ResolutionUnavailableLogged;
        return queryResult.Resolution;
    }

    public async Task<(byte[] Pixels, int Stride, ScreenPixelFormat Format)?> CaptureAreaAsync(ScreenRect region)
    {
        if (!IsSupported || !await EnsureInitializedAsync().ConfigureAwait(false) || _trackerClient is null)
        {
            return null;
        }

        try
        {
            var (base64, stride, hasAlpha) = await _trackerClient.CaptureAreaAsync(region.X, region.Y, region.Width, region.Height).ConfigureAwait(false);
            var pixels = Convert.FromBase64String(base64);
            var format = hasAlpha ? ScreenPixelFormat.Abgr8888 : ScreenPixelFormat.Rgb24;
            return (pixels, stride, format);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.Debug(ex, "[GnomePositionProvider] Failed to capture area via DBus extension");
            return null;
        }
    }

    internal static async Task<bool> IsExtensionEnabledAsync(Func<Task<IDictionary<string, object>>> getExtensionInfo)
    {
        var info = await getExtensionInfo().ConfigureAwait(false);
        return TryReadEnabledState(info);
    }

    internal static bool TryReadEnabledState(IDictionary<string, object>? info)
    {
        if (info is null || !info.TryGetValue("state", out var stateObj))
        {
            return false;
        }

        return stateObj switch
        {
            double stateValue => Math.Abs(stateValue - 1) < double.Epsilon * 10,
            int stateValue => stateValue is 1,
            uint stateValue => stateValue is 1,
            long stateValue => stateValue is 1,
            _ => false,
        };
    }

    internal static async Task<(int X, int Y)?> TryGetAbsolutePositionAsync(Func<Task<(int x, int y)>> getPosition)
    {
        try
        {
            var (x, y) = await getPosition().ConfigureAwait(false);
            return (x, y);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.Debug(ex, "[GnomePositionProvider] Failed to get position");
            return null;
        }
    }

    internal static async Task<ResolutionQueryResult> TryGetScreenResolutionAsync(
        Func<Task<(int width, int height)>> getResolution,
        (int Width, int Height)? cachedResolution,
        bool resolutionUnavailableLogged)
    {
        if (cachedResolution is not null)
        {
            return new ResolutionQueryResult(cachedResolution, cachedResolution, resolutionUnavailableLogged);
        }

        try
        {
            var (width, height) = await getResolution().ConfigureAwait(false);
            var resolved = (width, height);
            Log.Information("[GnomePositionProvider] Got resolution from DBus: {Width}x{Height}", width, height);
            return new ResolutionQueryResult(resolved, resolved, resolutionUnavailableLogged);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            if (IsResolutionServiceUnavailable(ex))
            {
                if (!resolutionUnavailableLogged)
                {
                    Log.Warning("[GnomePositionProvider] Resolution unavailable until extension is active: {Error}", ex.Message);
                    resolutionUnavailableLogged = true;
                }
                else
                {
                    Log.Debug("[GnomePositionProvider] Resolution service still unavailable: {Error}", ex.Message);
                }

                return new ResolutionQueryResult(Resolution: null, CachedResolution: null, resolutionUnavailableLogged);
            }

            Log.LogError(ex, "[GnomePositionProvider] Failed to get resolution");
            return new ResolutionQueryResult(Resolution: null, CachedResolution: null, resolutionUnavailableLogged);
        }
    }

    internal readonly record struct ResolutionQueryResult(
        (int Width, int Height)? Resolution,
        (int Width, int Height)? CachedResolution,
        bool ResolutionUnavailableLogged);

    private static bool IsResolutionServiceUnavailable(Exception ex)
    {
        var message = ex.Message;
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        return message.Contains("org.freedesktop.DBus.Error.ServiceUnknown", StringComparison.OrdinalIgnoreCase)
            || message.Contains("The name is not activatable", StringComparison.OrdinalIgnoreCase)
            || message.Contains("org.freedesktop.DBus.Error.UnknownObject", StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _extensionsClient = null;
        _trackerClient = null;
        _isInitialized = false;
        _dbusSession?.Dispose();
        _dbusSession = null;
        GC.SuppressFinalize(this);
    }
}
