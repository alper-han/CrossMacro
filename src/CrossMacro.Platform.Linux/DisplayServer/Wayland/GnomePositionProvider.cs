using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using CrossMacro.Core.Logging;
using CrossMacro.Core.Services;
using CrossMacro.Platform.Linux.DisplayServer.Wayland.DBus;

namespace CrossMacro.Platform.Linux.DisplayServer.Wayland
{
    public class GnomePositionProvider : IMousePositionProvider, IExtensionStatusNotifier
    {
        // Embedded GNOME Shell Extension files - auto-installed/updated when needed
        private const string EXTENSION_JS = @"import Gio from 'gi://Gio';
import GLib from 'gi://GLib';
import * as Main from 'resource:///org/gnome/shell/ui/main.js';
import { Extension } from 'resource:///org/gnome/shell/extensions/extension.js';

const MouseInterface = `
<node>
  <interface name=""io.github.alper_han.crossmacro.Tracker"">
    <method name=""GetPosition"">
      <arg type=""i"" direction=""out"" name=""x""/>
      <arg type=""i"" direction=""out"" name=""y""/>
    </method>
    <method name=""GetResolution"">
      <arg type=""i"" direction=""out"" name=""width""/>
      <arg type=""i"" direction=""out"" name=""height""/>
    </method>
  </interface>
</node>`;

export default class CursorSpyExtension extends Extension {
    enable() {
        this._dbusImpl = Gio.DBusExportedObject.wrapJSObject(MouseInterface, this);
        this._dbusImpl.export(Gio.DBus.session, '/io/github/alper_han/crossmacro/Tracker');

        Gio.DBus.session.own_name(
            'io.github.alper_han.crossmacro.Tracker',
            Gio.BusNameOwnerFlags.NONE,
            null,
            null
        );

        console.log('CursorSpyExtension enabled');
    }

    disable() {
        if (this._dbusImpl) {
            this._dbusImpl.unexport();
            this._dbusImpl = null;
        }
        console.log('CursorSpyExtension disabled');
    }

    GetPosition() {
        let [x, y, mask] = global.get_pointer();
        return [x, y];
    }

    GetResolution() {
        // Use global.stage to get the full desktop dimensions (all monitors combined)
        // This ensures the virtual mouse maps 1:1 to the coordinate space used by GetPosition
        let width = global.stage.get_width();
        let height = global.stage.get_height();
        console.log(`CursorSpyExtension: GetResolution called, returning ${width}x${height}`);
        return [width, height];
    }
}
";

        private const string METADATA_JSON = @"{
  ""name"": ""Cursor Spy"",
  ""description"": ""Exposes cursor position via DBus"",
  ""uuid"": ""crossmacro@zynix.net"",
  ""shell-version"": [ ""45"", ""46"", ""47"", ""48"", ""49"", ""50"", ""51"" ]
}
";
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
        private ExtensionStatusChangedEventArgs? _currentExtensionStatus;
        
        public event EventHandler<ExtensionStatusChangedEventArgs>? ExtensionStatusUpdated;
        public event EventHandler<string>? ExtensionStatusChanged;

        public ExtensionStatusChangedEventArgs? CurrentExtensionStatus => _currentExtensionStatus;

        public string ProviderName => "GNOME Shell Extension (DBus)";
        public bool IsSupported { get; private set; }

        public GnomePositionProvider()
        {
            var currentDesktop = Environment.GetEnvironmentVariable("XDG_CURRENT_DESKTOP");
            var session = Environment.GetEnvironmentVariable("GDMSESSION");
            
            IsSupported = (currentDesktop?.Contains("GNOME", StringComparison.OrdinalIgnoreCase) ?? false) || 
                          (session?.Contains("gnome", StringComparison.OrdinalIgnoreCase) ?? false);

            if (IsSupported)
            {
                _ = Task.Run(InitializeAsync);
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
                    Log.Information("[GnomePositionProvider] Installing GNOME Shell extension to {Path}", ExtensionPath);

                Directory.CreateDirectory(ExtensionPath);

                bool jsUpdated = await EnsureFileContentAsync(ExtensionJsPath, EXTENSION_JS);
                bool metadataUpdated = await EnsureFileContentAsync(MetadataJsonPath, METADATA_JSON);

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

                    await Task.Delay(100);
                    elapsedMs += 100;
                }

                if (elapsedMs >= maxWaitMs)
                {
                    Log.Warning("[GnomePositionProvider] File verification timeout, proceeding anyway");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[GnomePositionProvider] Failed to install GNOME extension");
                PublishExtensionStatus(ExtensionStatusCode.Error, "Failed to install GNOME extension");
            }
        }

        internal static async Task<bool> EnsureFileContentAsync(string filePath, string expectedContent)
        {
            if (File.Exists(filePath))
            {
                var existingContent = await File.ReadAllTextAsync(filePath);
                if (string.Equals(existingContent, expectedContent, StringComparison.Ordinal))
                    return false;
            }

            await File.WriteAllTextAsync(filePath, expectedContent);
            return true;
        }
        
        private async Task<bool> CheckExtensionEnabledAsync()
        {
            try
            {
                if (_extensionsClient == null)
                {
                    return false;
                }

                return await IsExtensionEnabledAsync(() => _extensionsClient.GetExtensionInfoAsync(ExtensionUuid)).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "[GnomePositionProvider] Failed to check extension status via DBus");
                return false;
            }
        }
        
        private async Task<bool> EnableExtensionAsync()
        {
            try
            {
                if (_extensionsClient == null)
                {
                    return false;
                }

                return await _extensionsClient.EnableExtensionAsync(ExtensionUuid).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[GnomePositionProvider] Exception while trying to enable extension via DBus");
                return false;
            }
        }
        
        private async Task ValidateExtensionStatusAsync()
        {
            // Check if extension is enabled
            bool isEnabled = await CheckExtensionEnabledAsync();
            
            if (!isEnabled)
            {
                Log.Information("[GnomePositionProvider] Extension is not enabled, attempting to enable via DBus...");
                
                // Try to enable it
                bool enableSuccess = await EnableExtensionAsync();
                
                if (enableSuccess)
                {
                    // Verify it's actually enabled now
                    await Task.Delay(500); // Give it a moment
                    isEnabled = await CheckExtensionEnabledAsync();
                    
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
            _currentExtensionStatus = args;
            ExtensionStatusUpdated?.Invoke(this, args);
            ExtensionStatusChanged?.Invoke(this, message);
        }

        private async Task InitializeAsync()
        {
            LinuxDbusSession? dbusSession = null;

            try
            {
                // Ensure extension is installed before connecting
                // This runs on a background thread now, so it won't block startup
                await EnsureExtensionInstalledAsync();

                if (_disposed)
                {
                    _initializationTcs.TrySetResult(false);
                    return;
                }

                dbusSession = await LinuxDbusSession.ConnectAsync().ConfigureAwait(false);

                if (_disposed)
                {
                    dbusSession.Dispose();
                    _initializationTcs.TrySetResult(false);
                    return;
                }

                _dbusSession = dbusSession;
                _extensionsClient = dbusSession.CreateGnomeShellExtensionsClient();
                _trackerClient = dbusSession.CreateGnomeTrackerClient();

                // Now that we are connected, check status via DBus
                await ValidateExtensionStatusAsync();

                if (_disposed)
                {
                    dbusSession.Dispose();
                    _dbusSession = null;
                    _extensionsClient = null;
                    _trackerClient = null;
                    _initializationTcs.TrySetResult(false);
                    return;
                }

                _isInitialized = true;
                _initializationTcs.TrySetResult(true);
                Log.Information("[GnomePositionProvider] Connected to DBus service");
            }
            catch (Exception ex)
            {
                dbusSession?.Dispose();
                _dbusSession = null;
                _extensionsClient = null;
                _trackerClient = null;
                Log.Error(ex, "[GnomePositionProvider] Failed to initialize DBus connection");
                IsSupported = false;
                _initializationTcs.TrySetResult(false);
            }
        }

        private async Task<bool> EnsureInitializedAsync()
        {
            if (_disposed)
                return false;

            if (_isInitialized)
                return true;

            // Wait for initialization with timeout (only on first call)
            var completedTask = await Task.WhenAny(_initializationTcs.Task, Task.Delay(2000));
            return completedTask == _initializationTcs.Task && await _initializationTcs.Task;
        }

        public async Task<(int X, int Y)?> GetAbsolutePositionAsync()
        {
            if (!IsSupported || !await EnsureInitializedAsync().ConfigureAwait(false) || _trackerClient == null)
                return null;

            return await TryGetAbsolutePositionAsync(_trackerClient.GetPositionAsync).ConfigureAwait(false);
        }

        public async Task<(int Width, int Height)?> GetScreenResolutionAsync()
        {
            if (!IsSupported || !await EnsureInitializedAsync().ConfigureAwait(false) || _trackerClient == null)
                return null;

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

        internal static async Task<bool> IsExtensionEnabledAsync(Func<Task<IDictionary<string, object>>> getExtensionInfo)
        {
            var info = await getExtensionInfo().ConfigureAwait(false);
            return TryReadEnabledState(info);
        }

        internal static bool TryReadEnabledState(IDictionary<string, object>? info)
        {
            if (info == null || !info.TryGetValue("state", out var stateObj))
            {
                return false;
            }

            return stateObj switch
            {
                double stateValue => stateValue == 1,
                int stateValue => stateValue == 1,
                uint stateValue => stateValue == 1,
                long stateValue => stateValue == 1,
                _ => false
            };
        }

        internal static async Task<(int X, int Y)?> TryGetAbsolutePositionAsync(Func<Task<(int x, int y)>> getPosition)
        {
            try
            {
                var (x, y) = await getPosition().ConfigureAwait(false);
                return (x, y);
            }
            catch (Exception ex)
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
            if (cachedResolution.HasValue)
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
            catch (Exception ex)
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

                    return new ResolutionQueryResult(null, null, resolutionUnavailableLogged);
                }

                Log.Error(ex, "[GnomePositionProvider] Failed to get resolution");
                return new ResolutionQueryResult(null, null, resolutionUnavailableLogged);
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
                return;

            _disposed = true;
            _extensionsClient = null;
            _trackerClient = null;
            _isInitialized = false;
            _dbusSession?.Dispose();
            _dbusSession = null;
        }
    }
}
