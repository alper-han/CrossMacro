using System;
using System.IO;
using CrossMacro.Core.Services;
using CrossMacro.Platform.Linux;
using CrossMacro.Platform.Linux.Ipc;
using Serilog;

namespace CrossMacro.UI.Services;

/// <summary>
/// Factory to create Linux Input Simulator and Capture implementations based on system capabilities.
/// Supports Hybrid Mode (Daemon Priority -> Direct/Root Fallback).
/// </summary>
public class LinuxInputProviderFactory
{
    private readonly IpcClient _ipcClient;
    private readonly Func<LinuxInputSimulator> _legacySimulatorFactory;
    private readonly Func<LinuxInputCapture> _legacyCaptureFactory;
    private readonly Func<LinuxIpcInputSimulator> _ipcSimulatorFactory;
    private readonly Func<LinuxIpcInputCapture> _ipcCaptureFactory;

    // Cache the decision to ensure consistent behavior
    private bool? _useLegacy;
    
    public LinuxInputProviderFactory(
        IpcClient ipcClient,
        Func<LinuxInputSimulator> legacySimulatorFactory,
        Func<LinuxInputCapture> legacyCaptureFactory,
        Func<LinuxIpcInputSimulator> ipcSimulatorFactory,
        Func<LinuxIpcInputCapture> ipcCaptureFactory)
    {
        _ipcClient = ipcClient;
        _legacySimulatorFactory = legacySimulatorFactory;
        _legacyCaptureFactory = legacyCaptureFactory;
        _ipcSimulatorFactory = ipcSimulatorFactory;
        _ipcCaptureFactory = ipcCaptureFactory;
    }

    public IInputSimulator CreateSimulator()
    {
        if (ShouldUseLegacy())
        {
            Log.Information("[LinuxInputFactory] Using Legacy (Direct) Input Simulator");
            return _legacySimulatorFactory();
        }
        
        Log.Information("[LinuxInputFactory] Using Daemon (IPC) Input Simulator");
        return _ipcSimulatorFactory();
    }

    public IInputCapture CreateCapture()
    {
        if (ShouldUseLegacy())
        {
            Log.Information("[LinuxInputFactory] Using Legacy (Direct) Input Capture");
            return _legacyCaptureFactory();
        }

        Log.Information("[LinuxInputFactory] Using Daemon (IPC) Input Capture");
        return _ipcCaptureFactory();
    }

    private bool ShouldUseLegacy()
    {
        if (_useLegacy.HasValue) return _useLegacy.Value;

        // 1. Check if we are Root (UID 0) or effectively have permission
        // NOTE: Even if we are not root, if we are in 'input' group, we might be able to use legacy.
        // But the main differentiator is: Can we connect to the daemon?
        
        bool canConnectToDaemon = false;
        try 
        {
            // Check both primary and fallback socket paths
            if (File.Exists(CrossMacro.Core.Ipc.IpcProtocol.DefaultSocketPath) ||
                File.Exists(CrossMacro.Core.Ipc.IpcProtocol.FallbackSocketPath))
            {
                canConnectToDaemon = true;
            }
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "[LinuxInputFactory] Failed to check daemon socket paths");
        }

        // 2. Check if we can write to uinput (Legacy Requirement)
        bool canUseDirectUInput = false;
        try
        {
            if (File.Exists("/dev/uinput"))
            {
                 // Try to open for write? Or just assume if writable.
                 // For safety, let's assume if socket exists, we prefer daemon.
                 // If socket MISSING, we fallback.
                 using (File.OpenWrite("/dev/uinput")) { canUseDirectUInput = true; }
            }
        }
        catch (UnauthorizedAccessException) 
        {
            canUseDirectUInput = false;
        }
        catch (Exception ex)
        { 
             Log.Debug(ex, "[LinuxInputFactory] Failed to check /dev/uinput access");
        }

        if (canConnectToDaemon)
        {
            _useLegacy = false;
            return false;
        }

        if (canUseDirectUInput)
        {
            Log.Warning("[LinuxInputFactory] Daemon socket not found, but we have /dev/uinput access. Falling back to LEGACY mode.");
            _useLegacy = true;
            return true;
        }

        // If neither found, default to LEGACY mode as a last resort for portability (AppImage/Native).
        // This allows the application to attempt direct access, which might work if permissions are granted via other means (e.g. capabilities),
        // or will produce a clear "Permission Denied" error instead of a confusing "Daemon Not Found".
        Log.Warning("[LinuxInputFactory] Neither Daemon socket nor /dev/uinput write access found. Defaulting to LEGACY mode (expect permission errors if not running as root/input group).");
        _useLegacy = true;
        return true;
    }
}
