using System;
using System.IO;
using System.Threading.Tasks;
using CrossMacro.Core.Services;
using Serilog;
using CrossMacro.Platform.Linux.DisplayServer.Wayland.DBus;

using Tmds.DBus;

namespace CrossMacro.Platform.Linux.DisplayServer.Wayland
{
    [DBusInterface("io.github.alper_han.crossmacro.Tracker")]
    public interface IGnomeTrackerService : IDBusObject
    {
        Task<(int x, int y)> GetPositionAsync();
        Task<(int width, int height)> GetResolutionAsync();
    }

    public class GnomePositionProvider : IMousePositionProvider, IExtensionStatusNotifier
    {
        private const string TrackerServiceName = "io.github.alper_han.crossmacro.Tracker";
        private const string TrackerObjectPath = "/io/github/alper_han/crossmacro/Tracker";

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
  ""shell-version"": [ ""45"", ""46"", ""47"", ""48"", ""49"" ]
}
";
        private const string ExtensionUuid = "crossmacro@zynix.net";
        
        private static readonly string ExtensionPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".local", "share", "gnome-shell", "extensions", ExtensionUuid);
        private static readonly string ExtensionJsPath = Path.Combine(ExtensionPath, "extension.js");
        private static readonly string MetadataJsonPath = Path.Combine(ExtensionPath, "metadata.json");

        private Connection? _connection;
        private IGnomeTrackerService? _proxy;
        private readonly TaskCompletionSource<bool> _initializationTcs = new();
        private bool _isInitialized;
        private (int Width, int Height)? _cachedResolution;
        private bool _disposed;
        
        public event EventHandler<string>? ExtensionStatusChanged;

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
                ExtensionStatusChanged?.Invoke(this, "Failed to install GNOME extension");
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
                if (_connection == null) return false;
                
                var extensionsProxy = _connection.CreateProxy<IGnomeShellExtensions>("org.gnome.Shell", "/org/gnome/Shell");
                var info = await extensionsProxy.GetExtensionInfoAsync(ExtensionUuid);
                
                if (info != null && info.TryGetValue("state", out var stateObj))
                {
                    // State 1 = ENABLED/ACTIVE
                    double stateValue = 0;
                    if (stateObj is double stateDbl) stateValue = stateDbl;
                    else if (stateObj is int stateInt) stateValue = stateInt;
                    else if (stateObj is uint stateUInt) stateValue = stateUInt;
                    else if (stateObj is long stateLong) stateValue = stateLong;
                    
                    return stateValue == 1;
                }
                
                return false;
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
                if (_connection == null) return false;

                var extensionsProxy = _connection.CreateProxy<IGnomeShellExtensions>("org.gnome.Shell", "/org/gnome/Shell");
                return await extensionsProxy.EnableExtensionAsync(ExtensionUuid);
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
                        ExtensionStatusChanged?.Invoke(this, "GNOME extension enabled successfully");
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
            ExtensionStatusChanged?.Invoke(this, message);
        }

        private async Task InitializeAsync()
        {
            try
            {
                // Ensure extension is installed before connecting
                // This runs on a background thread now, so it won't block startup
                await EnsureExtensionInstalledAsync();

                _connection = new Connection(Address.Session);
                await _connection.ConnectAsync();
                
                // Now that we are connected, check status via DBus
                await ValidateExtensionStatusAsync();
                
                _proxy = _connection.CreateProxy<IGnomeTrackerService>(TrackerServiceName, TrackerObjectPath);
                _isInitialized = true;
                _initializationTcs.SetResult(true);
                Log.Information("[GnomePositionProvider] Connected to DBus service");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[GnomePositionProvider] Failed to initialize DBus connection");
                IsSupported = false;
                _initializationTcs.SetResult(false);
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
            if (!IsSupported || !await EnsureInitializedAsync() || _proxy == null)
                return null;

            try
            {
                var (x, y) = await _proxy.GetPositionAsync();
                return (x, y);
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "[GnomePositionProvider] Failed to get position");
                return null;
            }
        }

        public async Task<(int Width, int Height)?> GetScreenResolutionAsync()
        {
            // Return cached resolution if available
            if (_cachedResolution.HasValue)
                return _cachedResolution;

            if (!IsSupported || !await EnsureInitializedAsync() || _proxy == null)
                return null;

            try
            {
                var (w, h) = await _proxy.GetResolutionAsync();
                _cachedResolution = (w, h);
                Log.Information("[GnomePositionProvider] Got resolution from DBus: {Width}x{Height}", w, h);
                return _cachedResolution;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[GnomePositionProvider] Failed to get resolution");
                return null;
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _connection?.Dispose();
        }
    }
}
