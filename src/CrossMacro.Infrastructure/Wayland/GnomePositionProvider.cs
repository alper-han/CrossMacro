using System;
using System.IO;
using System.Threading.Tasks;
using CrossMacro.Core.Services;
using Serilog;
using Tmds.DBus;

namespace CrossMacro.Infrastructure.Wayland
{
    [DBusInterface("org.crossmacro.Tracker")]
    public interface IGnomeTrackerService : IDBusObject
    {
        Task<(int x, int y)> GetPositionAsync();
        Task<(int width, int height)> GetResolutionAsync();
    }

    public class GnomePositionProvider : IMousePositionProvider
    {
        // Embedded GNOME Shell Extension files - auto-deployed if missing
        private const string EXTENSION_JS = @"import Gio from 'gi://Gio';
import GLib from 'gi://GLib';
import * as Main from 'resource:///org/gnome/shell/ui/main.js';
import { Extension } from 'resource:///org/gnome/shell/extensions/extension.js';

const MouseInterface = `
<node>
  <interface name=""org.crossmacro.Tracker"">
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
        this._dbusImpl.export(Gio.DBus.session, '/org/crossmacro/Tracker');

        Gio.DBus.session.own_name(
            'org.crossmacro.Tracker',
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
        private Connection? _connection;
        private IGnomeTrackerService? _proxy;
        private readonly TaskCompletionSource<bool> _initializationTcs = new();
        private bool _isInitialized;
        private (int Width, int Height)? _cachedResolution;
        private bool _disposed;
        
        // Event for notifying about extension status
        public event EventHandler<string>? ExtensionStatusChanged;

        public string ProviderName => "GNOME Shell Extension (DBus)";
        public bool IsSupported { get; private set; }

        public GnomePositionProvider()
        {
            var currentDesktop = Environment.GetEnvironmentVariable("XDG_CURRENT_DESKTOP") ?? "";
            var session = Environment.GetEnvironmentVariable("GDMSESSION") ?? "";
            
            IsSupported = currentDesktop.Contains("GNOME", StringComparison.OrdinalIgnoreCase) || 
                          session.Contains("gnome", StringComparison.OrdinalIgnoreCase);

            if (IsSupported)
            {
                // Fire and forget, but exceptions are caught in InitializeAsync
                _ = Task.Run(InitializeAsync);
            }
            else
            {
                _initializationTcs.SetResult(false);
            }
        }

        private void EnsureExtensionInstalled()
        {
            try
            {
                var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                var extensionPath = Path.Combine(homeDir, ".local/share/gnome-shell/extensions/crossmacro@zynix.net");
                
                bool extensionFilesExist = Directory.Exists(extensionPath) && 
                    File.Exists(Path.Combine(extensionPath, "extension.js")) &&
                    File.Exists(Path.Combine(extensionPath, "metadata.json"));
                
                if (!extensionFilesExist)
                {
                    // Install extension files
                    Log.Information("[GnomePositionProvider] Installing GNOME Shell extension to {Path}", extensionPath);
                    Directory.CreateDirectory(extensionPath);
                    
                    File.WriteAllText(Path.Combine(extensionPath, "extension.js"), EXTENSION_JS);
                    File.WriteAllText(Path.Combine(extensionPath, "metadata.json"), METADATA_JSON);
                    
                    // Wait for files to be fully written to disk
                    var extensionJsPath = Path.Combine(extensionPath, "extension.js");
                    var metadataJsonPath = Path.Combine(extensionPath, "metadata.json");
                    var maxWaitMs = 3000;
                    var elapsedMs = 0;
                    
                    while (elapsedMs < maxWaitMs)
                    {
                        var extensionJsInfo = new FileInfo(extensionJsPath);
                        var metadataJsonInfo = new FileInfo(metadataJsonPath);
                        
                        if (extensionJsInfo.Exists && extensionJsInfo.Length > 0 &&
                            metadataJsonInfo.Exists && metadataJsonInfo.Length > 0)
                        {
                            Log.Debug("[GnomePositionProvider] Files verified on disk after {Ms}ms", elapsedMs);
                            break;
                        }
                        
                        System.Threading.Thread.Sleep(100);
                        elapsedMs += 100;
                    }
                    
                    if (elapsedMs >= maxWaitMs)
                    {
                        Log.Warning("[GnomePositionProvider] File verification timeout, proceeding anyway");
                    }
                    
                    Log.Information("[GnomePositionProvider] Extension files installed successfully");
                }
                else
                {
                    Log.Debug("[GnomePositionProvider] Extension files already exist at {Path}", extensionPath);
                }
                
                // Always check if extension is enabled, regardless of file existence
                ValidateExtensionStatus();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[GnomePositionProvider] Failed to install GNOME extension");
                ExtensionStatusChanged?.Invoke(this, "Failed to install GNOME extension");
            }
        }
        
        private bool CheckExtensionEnabled()
        {
            try
            {
                using var checkProcess = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "gnome-extensions",
                        Arguments = "list --enabled",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                
                checkProcess.Start();
                var output = checkProcess.StandardOutput.ReadToEnd();
                checkProcess.WaitForExit();
                
                if (checkProcess.ExitCode == 0)
                {
                    bool isEnabled = output.Contains("crossmacro@zynix.net");
                    Log.Debug("[GnomePositionProvider] Extension enabled status: {Status}", isEnabled);
                    return isEnabled;
                }
                else
                {
                    var error = checkProcess.StandardError.ReadToEnd();
                    Log.Warning("[GnomePositionProvider] Failed to check extension status: {Error}", error);
                    return false;
                }
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "[GnomePositionProvider] Could not check extension status (gnome-extensions command may not be available)");
                return false;
            }
        }
        
        private bool EnableExtension()
        {
            // Try to enable the extension automatically
            try
            {
                using var enableProcess = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "gnome-extensions",
                        Arguments = "enable crossmacro@zynix.net",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                
                enableProcess.Start();
                var output = enableProcess.StandardOutput.ReadToEnd();
                var error = enableProcess.StandardError.ReadToEnd();
                enableProcess.WaitForExit();
                
                if (enableProcess.ExitCode == 0)
                {
                    Log.Information("[GnomePositionProvider] Extension enabled successfully");
                    return true;
                }
                else
                {
                    Log.Warning("[GnomePositionProvider] Failed to enable extension. Exit code: {Code}, Error: {Error}", 
                        enableProcess.ExitCode, error);
                    return false;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[GnomePositionProvider] Exception while trying to enable extension");
                return false;
            }
        }
        
        private void ValidateExtensionStatus()
        {
            // Check if extension is enabled
            bool isEnabled = CheckExtensionEnabled();
            
            if (!isEnabled)
            {
                Log.Information("[GnomePositionProvider] Extension is not enabled, attempting to enable...");
                
                // Try to enable it
                bool enableSuccess = EnableExtension();
                
                if (enableSuccess)
                {
                    // Verify it's actually enabled now
                    System.Threading.Thread.Sleep(500); // Give it a moment
                    isEnabled = CheckExtensionEnabled();
                    
                    if (isEnabled)
                    {
                        Log.Information("[GnomePositionProvider] Extension enabled and verified successfully");
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
                // Ensure extension is installed/enabled before connecting
                // This runs on a background thread now, so it won't block startup
                EnsureExtensionInstalled();

                _connection = new Connection(Address.Session);
                await _connection.ConnectAsync();
                _proxy = _connection.CreateProxy<IGnomeTrackerService>("org.crossmacro.Tracker", "/org/crossmacro/Tracker");
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
