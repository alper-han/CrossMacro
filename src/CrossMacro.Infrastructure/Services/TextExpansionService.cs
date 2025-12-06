using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services;
using CrossMacro.Infrastructure.Helpers;
using CrossMacro.Native.Evdev;
using CrossMacro.Native.UInput;
using Serilog;

namespace CrossMacro.Infrastructure.Services;

/// <summary>
/// Service for monitoring keystrokes and performing text expansion
/// </summary>
public class TextExpansionService : ITextExpansionService
{
    private readonly ISettingsService _settingsService;
    private readonly IClipboardService _clipboardService;
    private readonly TextExpansionStorageService _storageService;
    private readonly List<EvdevReader> _readers = new();
    private readonly object _lock; // Use monitor for synchronous operations
    private bool _isRunning;
    
    // Buffer for tracking typed characters
    private readonly StringBuilder _buffer;
    private const int MaxBufferLength = 50; // Max supported trigger length
    
    // UInput device for injecting keystrokes
    private UInputDevice? _outputDevice;
    private readonly SemaphoreSlim _outputLock; // Use SemaphoreSlim for async operations

    // Modifier state
    private bool _isShiftPressed;
    private bool _isAltGrPressed;

    public bool IsRunning => _isRunning;

    public TextExpansionService(ISettingsService settingsService, IClipboardService clipboardService, TextExpansionStorageService storageService)
    {
        _settingsService = settingsService;
        _clipboardService = clipboardService;
        _storageService = storageService;
        _buffer = new StringBuilder();
        _lock = new object();
        _outputLock = new SemaphoreSlim(1, 1);
    }


    public void Start()
    {
        // Only start if enabled in settings
        if (!_settingsService.Current.EnableTextExpansion)
        {
            Log.Information("[TextExpansionService] Not starting because feature is disabled in settings");
            return;
        }

        lock (_lock)
        {
            if (_isRunning) return;

            try
            {
                // Auto-detect keyboards
                var devices = InputDeviceHelper.GetAvailableDevices();
                var keyboards = devices.Where(d => d.IsKeyboard).ToList();

                if (keyboards.Count == 0)
                {
                    Log.Warning("[TextExpansionService] No keyboards found, cannot start monitoring");
                    return;
                }

                foreach (var kbd in keyboards)
                {
                    try
                    {
                        var reader = new EvdevReader(kbd.Path, kbd.Name);
                        reader.EventReceived += OnEventReceived;
                        reader.Start();
                        _readers.Add(reader);
                        Log.Information("[TextExpansionService] Monitoring {Name} for text expansion", kbd.Name);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "[TextExpansionService] Failed to open keyboard {Name}", kbd.Name);
                    }
                }

                if (_readers.Count > 0)
                {
                    _isRunning = true;
                    // Initialize output device lazily when needed or now? 
                    // Better to init now to fail fast if permissions are bad, 
                    // but let's do lazy or concurrent to not block startup too much.
                    // Actually, let's just create it on demand or keep it simple.
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[TextExpansionService] Failed to start service");
            }
        }
    }

    public void Stop()
    {
        lock (_lock)
        {
            if (!_isRunning) return;

            foreach (var reader in _readers)
            {
                reader.Stop();
                reader.Dispose();
            }
            _readers.Clear();
            
            // Dispose output device if exists
            // We can't await here easily in a void method, but we should try to acquire the lock
            // or just dispose and let the lock handle potential errors? 
            // Better to fire and forget the disposal wrapped in async if needed, or just block lightly.
            // Since Stop might be called from UI, blocking on SemaphoreSlim.Wait might be risky if held long.
            // But _outputLock is only held during expansion which is fast.
            try 
            {
                _outputLock.Wait();
                _outputDevice?.Dispose();
                _outputDevice = null;
            }
            finally
            {
                _outputLock.Release();
            }

            _isRunning = false;
            Log.Information("[TextExpansionService] Stopped");
        }
    }

    public void Dispose()
    {
        Stop();
        _outputLock.Dispose();
    }

    // Debouncing state
    private int _lastKey;
    private long _lastPressTime;
    private const long DebounceTicks = 20 * 10000; // 20ms in ticks

    private void OnEventReceived(EvdevReader sender, UInputNative.input_event ev)
    {
        // Only process key events
        if (ev.type != UInputNative.EV_KEY) return;

        lock (_lock)
        {
            // Update shift state
            if (ev.code == 42 || ev.code == 54) // Left/Right Shift
            {
                _isShiftPressed = ev.value == 1 || ev.value == 2;
                return;
            }
            // Update AltGr state (Right Alt = 100)
            if (ev.code == 100)
            {
                _isAltGrPressed = ev.value == 1 || ev.value == 2;
                return;
            }

            // Only process key PRESS (value == 1)
            // Ignore Repeat (2) and Release (0)
            if (ev.value != 1) return;

            // Debouncing check
            // If same key is pressed very quickly, assume it's a duplicate event from dual interfaces
            long now = DateTime.UtcNow.Ticks;
            if (ev.code == _lastKey && (now - _lastPressTime) < DebounceTicks)
            {
               // Duplicate event, ignore
               return;
            }
            _lastKey = ev.code;
            _lastPressTime = now;

            // Map key to char with AltGr
            var charValue = KeyMapHelper.MapKeyCodeToChar(ev.code, _isShiftPressed, _isAltGrPressed);

            // Manage buffer
            if (charValue.HasValue)
            {
                AppendToBuffer(charValue.Value);
                CheckForExpansion();
            }
            else if (ev.code == 14) // Backspace
            {
                // Remove last char from buffer if possible
                if (_buffer.Length > 0) _buffer.Length--;
            }
            else if (ev.code == 28 || ev.code == 57) // Enter or Space
            {
                 // Reset usage on Enter logic can stay
                 if (ev.code == 28) _buffer.Clear();
                 // Space (57) is already mapped to ' ', so let it fall through or be handled by map?
                 // KeyMapHelper has Space -> ' '. So it goes to code block above.
            }
        }
    }

    private void AppendToBuffer(char c)
    {
        // Append char
        _buffer.Append(c);

        // Keep buffer size checked
        if (_buffer.Length > MaxBufferLength)
        {
            _buffer.Remove(0, _buffer.Length - MaxBufferLength);
        }
    }

    private void CheckForExpansion()
    {
        var currentText = _buffer.ToString();
        var expansions = _storageService.GetCurrent()
            .Where(e => e.IsEnabled && !string.IsNullOrEmpty(e.Trigger))
            .ToList();

        foreach (var expansion in expansions)
        {
            if (currentText.EndsWith(expansion.Trigger))
            {
                Log.Information("[TextExpansionService] Trigger detected: {Trigger} -> {Replacement}", expansion.Trigger, expansion.Replacement);
                
                // Perform expansion
                // 1. We must run this on a separate task to not block the input reader callback
                Task.Run(() => PerformExpansionAsync(expansion));
                
                // Clear buffer to prevent double triggering or recursive triggering
                _buffer.Clear();
                return; 
            }
        }
    }


    private async Task PerformExpansionAsync(TextExpansion expansion)
    {
        try
        {
            await _outputLock.WaitAsync();
            try
            {
                if (_outputDevice == null)
                {
                    _outputDevice = new UInputDevice();
                    _outputDevice.CreateVirtualInputDevice();
                    await Task.Delay(200);
                }

                // 1. Backspace the trigger
                Log.Debug("Backspacing {Length} chars", expansion.Trigger.Length);
                for (int i = 0; i < expansion.Trigger.Length; i++)
                {
                    SendKey(14); // Backspace
                }

                // 2. Paste the replacement (Universal Language Support)
                Log.Debug("Pasting replacement: {Replacement}", expansion.Replacement);
                
                // Backup clipboard
                var oldClipboard = await _clipboardService.GetTextAsync();
                
                // Set new text
                await _clipboardService.SetTextAsync(expansion.Replacement);
                await Task.Delay(50); // Wait for clipboard to accept
                
                // Send Ctrl+V
                SendKey(47, shift: false, altGr: false, ctrl: true); // 47 = v, ctrl=true
                
                // Wait for paste to complete
                await Task.Delay(150);
                
                // Restore clipboard
                if (!string.IsNullOrEmpty(oldClipboard))
                {
                    await _clipboardService.SetTextAsync(oldClipboard);
                }
            }
            finally
            {
                _outputLock.Release();
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error performing expansion");
        }
    }

    private void SendKey(int keyCode, bool shift = false, bool altGr = false, bool ctrl = false)
    {
        if (_outputDevice == null) return;

        // Modifiers Down
        if (ctrl)
        {
            _outputDevice.SendEvent(UInputNative.EV_KEY, 29, 1); // Left Ctrl
            _outputDevice.SendEvent(UInputNative.EV_SYN, UInputNative.SYN_REPORT, 0);
        }
        if (shift) 
        {
            _outputDevice.SendEvent(UInputNative.EV_KEY, 42, 1); // Left Shift
            _outputDevice.SendEvent(UInputNative.EV_SYN, UInputNative.SYN_REPORT, 0);
        }
        if (altGr)
        {
            _outputDevice.SendEvent(UInputNative.EV_KEY, 100, 1); // Right Alt
            _outputDevice.SendEvent(UInputNative.EV_SYN, UInputNative.SYN_REPORT, 0);
        }

        // Key Press
        _outputDevice.SendEvent(UInputNative.EV_KEY, (ushort)keyCode, 1);
        _outputDevice.SendEvent(UInputNative.EV_SYN, UInputNative.SYN_REPORT, 0);
        
        _outputDevice.SendEvent(UInputNative.EV_KEY, (ushort)keyCode, 0);
        _outputDevice.SendEvent(UInputNative.EV_SYN, UInputNative.SYN_REPORT, 0);

        // Modifiers Up (Reverse Order)
        if (altGr)
        {
            _outputDevice.SendEvent(UInputNative.EV_KEY, 100, 0);
            _outputDevice.SendEvent(UInputNative.EV_SYN, UInputNative.SYN_REPORT, 0);
        }
        if (shift) 
        {
            _outputDevice.SendEvent(UInputNative.EV_KEY, 42, 0);
            _outputDevice.SendEvent(UInputNative.EV_SYN, UInputNative.SYN_REPORT, 0);
        }
        if (ctrl)
        {
            _outputDevice.SendEvent(UInputNative.EV_KEY, 29, 0);
            _outputDevice.SendEvent(UInputNative.EV_SYN, UInputNative.SYN_REPORT, 0);
        }
    }
}
