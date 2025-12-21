using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services;
using Serilog;

namespace CrossMacro.Infrastructure.Services;

/// <summary>
/// Service for monitoring keystrokes and performing text expansion
/// </summary>
public class TextExpansionService : ITextExpansionService
{
    private readonly IKeyboardLayoutService _layoutService;
    private readonly ISettingsService _settingsService;
    private readonly IClipboardService _clipboardService;
    private readonly TextExpansionStorageService _storageService;
    
    private readonly Func<IInputCapture> _inputCaptureFactory;
    private readonly Func<IInputSimulator> _inputSimulatorFactory;
    
    private IInputCapture? _inputCapture;
    private IInputSimulator? _inputSimulator;
    
    private readonly object _lock; // Use monitor for synchronous operations
    private bool _isRunning;
    
    // Buffer for tracking typed characters
    private readonly StringBuilder _buffer;
    private const int MaxBufferLength = 50; // Max supported trigger length
    
    private readonly SemaphoreSlim _outputLock; // Use SemaphoreSlim for async operations

    // Modifier state
    private bool _isShiftPressed;
    private bool _isAltGrPressed;
    private bool _isCapsLockOn;

    public bool IsRunning => _isRunning;

    public TextExpansionService(
        ISettingsService settingsService, 
        IClipboardService clipboardService, 
        TextExpansionStorageService storageService,
        IKeyboardLayoutService layoutService,
        Func<IInputCapture> inputCaptureFactory,
        Func<IInputSimulator> inputSimulatorFactory)
    {
        _settingsService = settingsService;
        _clipboardService = clipboardService;
        _storageService = storageService;
        _layoutService = layoutService;
        _inputCaptureFactory = inputCaptureFactory;
        _inputSimulatorFactory = inputSimulatorFactory;
        
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
                // Initialize Input Capture
                _inputCapture = _inputCaptureFactory();
                _inputCapture.Configure(captureMouse: false, captureKeyboard: true);
                _inputCapture.InputReceived += OnInputReceived;
                _inputCapture.Error += OnInputCaptureError;
                
                // Fire and forget start (it's async but Start() is void)
                _ = _inputCapture.StartAsync(CancellationToken.None);
                
                // Initialize Input Simulator (lazy loaded or upfront is fine, we'll do it upfront here for readiness)
                // We'll actually initialize it on demand or here. Let's do it on demand to match previous pattern, 
                // but create the instance here.
                _inputSimulator = _inputSimulatorFactory();
                // Initialize with 0,0 since we don't care about mouse position for text expansion
                _inputSimulator.Initialize(0, 0); 
                
                _isRunning = true;
                Log.Information("[TextExpansionService] Started monitoring via {Provider}", _inputCapture.ProviderName);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[TextExpansionService] Failed to start service");
                Stop(); // Clean up if partial start
            }
        }
    }
    
    private void OnInputCaptureError(object? sender, string error)
    {
        Log.Error("[TextExpansionService] Input capture error: {Error}", error);
    }

    public void Stop()
    {
        lock (_lock)
        {
            if (!_isRunning) return;

            try 
            {
                if (_inputCapture != null)
                {
                    _inputCapture.InputReceived -= OnInputReceived;
                    _inputCapture.Error -= OnInputCaptureError;
                    _inputCapture.Stop();
                    _inputCapture.Dispose();
                    _inputCapture = null;
                }
                
                if (_inputSimulator != null)
                {
                    _inputSimulator.Dispose();
                    _inputSimulator = null;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[TextExpansionService] Error stopping service");
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

    private void OnInputReceived(object? sender, InputCaptureEventArgs e)
    {
        // Only process key events
        if (e.Type != InputEventType.Key) return;
        
        // We only care about Press (1). 
        // Note: Release (0) and Repeat (2) are ignored for typing logic, 
        // BUT we need Release for modifiers to track state correctly?
        // Actually, existing logic for modifiers checked value == 1 || value == 2.
        // Let's adapt carefully.
        
        lock (_lock)
        {
            // Update shift state
            if (e.Code == 42 || e.Code == 54) // Left/Right Shift
            {
                // value 1=down, 2=repeat, 0=up
                _isShiftPressed = e.Value == 1 || e.Value == 2;
                return;
            }
            // Update AltGr state (Right Alt = 100)
            if (e.Code == 100)
            {
                _isAltGrPressed = e.Value == 1 || e.Value == 2;
                return;
            }
            // Update CapsLock state (Toggle on Press)
            if (e.Code == 58 && e.Value == 1) // Caps Lock Press
            {
                _isCapsLockOn = !_isCapsLockOn;
                return;
            }

            // Only process key PRESS (value == 1) for actual typing
            // Ignore Repeat (2) and Release (0)
            if (e.Value != 1) return;

            // Debouncing check
            // If same key is pressed very quickly, assume it's a duplicate event from dual interfaces
            long now = DateTime.UtcNow.Ticks;
            if (e.Code == _lastKey && (now - _lastPressTime) < DebounceTicks)
            {
               // Duplicate event, ignore
               return;
            }
            _lastKey = e.Code;
            _lastPressTime = now;

            // Map key to char using IKeyboardLayoutService (XKB aware)
            var charValue = _layoutService.GetCharFromKeyCode(e.Code, _isShiftPressed, _isAltGrPressed, _isCapsLockOn);

            // Manage buffer
            if (charValue.HasValue)
            {
                AppendToBuffer(charValue.Value);
                CheckForExpansion();
            }
            else if (e.Code == 14) // Backspace
            {
                // Remove last char from buffer if possible
                if (_buffer.Length > 0) _buffer.Length--;
            }
            else if (e.Code == 28 || e.Code == 57) // Enter or Space
            {
                 // Reset usage on Enter logic can stay
                 if (e.Code == 28) _buffer.Clear();
                 // Space (57) special handling
                 if (e.Code == 57)
                 {
                    var spaceChar = _layoutService.GetCharFromKeyCode(57, false, false, false);
                    if (spaceChar.HasValue) 
                    {
                        AppendToBuffer(spaceChar.Value);
                        CheckForExpansion();
                    }
                 }
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
                if (_inputSimulator == null)
                {
                    // Should be initialized in Start, but safe guard
                    _inputSimulator = _inputSimulatorFactory();
                    _inputSimulator.Initialize(0,0);
                    await Task.Delay(200);
                }

                // 0. Wait for Modifiers (AltGr AND Shift) to be released
                int timeoutMs = 2000;
                int elapsed = 0;
                while ((_isAltGrPressed || _isShiftPressed) && elapsed < timeoutMs)
                {
                    await Task.Delay(50);
                    elapsed += 50;
                }

                // 1. Backspace the trigger
                Log.Debug("Backspacing {Length} chars", expansion.Trigger.Length);
                for (int i = 0; i < expansion.Trigger.Length; i++)
                {
                    await SendKeyAsync(14); // Backspace
                }

                // 2. Insert Replacement
                bool clipboardSuccess = false;

                if (_clipboardService.IsSupported)
                {
                    Log.Debug("Attempting to paste replacement: {Replacement}", expansion.Replacement);
                    
                    try 
                    {
                        // Backup clipboard (with timeout)
                        string? oldClipboard = null;
                        try 
                        {
                            var getTask = _clipboardService.GetTextAsync();
                            if (await Task.WhenAny(getTask, Task.Delay(100)) == getTask)
                            {
                                oldClipboard = await getTask;
                            }
                        }
                        catch (Exception ex) 
                        {
                            Log.Warning(ex, "Failed to backup clipboard");
                        }
                        
                        // Set new text (with timeout)
                        var setsTask = _clipboardService.SetTextAsync(expansion.Replacement);
                        if (await Task.WhenAny(setsTask, Task.Delay(100)) == setsTask)
                        {
                            await setsTask; // Propagate exceptions if any
                            
                            await Task.Delay(50); // Wait for clipboard to accept
                            
                            // Send Ctrl+V
                            await SendKeyAsync(47, shift: false, altGr: false, ctrl: true); // 47 = v, ctrl=true
                            
                            // Wait for paste to complete
                            await Task.Delay(150);
                            
                            clipboardSuccess = true;
                            
                            // Restore clipboard (fire and forget)
                            if (!string.IsNullOrEmpty(oldClipboard))
                            {
                                _ = Task.Run(async () => {
                                    try {
                                        var restoreTask = _clipboardService.SetTextAsync(oldClipboard);
                                        await Task.WhenAny(restoreTask, Task.Delay(200));
                                    } catch (Exception ex) 
                                    { 
                                        Log.Debug(ex, "[TextExpansionService] Failed to restore clipboard");
                                    }
                                });
                            }
                        }
                        else
                        {
                             Log.Warning("Clipboard SetText timed out (100ms). Switching to type fallback.");
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Clipboard paste operation failed via exception");
                    }
                }

                if (!clipboardSuccess)
                {
                    // Fallback: Type the characters directly if clipboard failed/timed-out
                    Log.Information("Typing replacement directly (Fallback active): {Replacement}", expansion.Replacement);
                    
                    // Iterate using index to handle Surrogate Pairs (UTF-16) correctly
                    string text = expansion.Replacement;
                    for (int i = 0; i < text.Length; i++)
                    {
                        char c = text[i];
                        if (c == '\r') continue; // Ignore CR, rely on LF
                        
                        if (c == '\n')
                        {
                            await SendKeyAsync(28); // Enter
                            await Task.Delay(5);
                            continue;
                        }

                        int codePoint = c;
                        bool isSurrogatePair = false;

                        // Check for Surrogate Pair (High followed by Low)
                        if (char.IsHighSurrogate(c) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
                        {
                            codePoint = char.ConvertToUtf32(text, i);
                            isSurrogatePair = true;
                        }

                        bool handled = false;
                        if (!isSurrogatePair)
                        {
                           // Standard BMP char: Try simple key injection first
                            var input = _layoutService.GetInputForChar(c);
                            if (input.HasValue)
                            {
                                await SendKeyAsync(input.Value.KeyCode, input.Value.Shift, input.Value.AltGr);
                                handled = true;
                            }
                        }

                        if (!handled)
                        {
                            // Fallback for Emojis (Surrogates) or chars not on layout
                            // Sequence: Ctrl+Shift+U -> Hex Code -> Enter
                            Log.Debug("Unicode Hex Fallback for: {CodePoint:X}", codePoint);
                            await TypeUnicodeHexAsync(codePoint);
                        }

                        if (isSurrogatePair)
                        {
                            i++; // Skip low surrogate
                        }

                        // Reduced inter-char delay for speed
                        await Task.Delay(1); 
                    }
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

    private async Task TypeUnicodeHexAsync(int codePoint)
    {
        if (_inputSimulator == null) return;

        Log.Debug("Initiating Ctrl+Shift+U sequence...");
        
        // 1. Send Ctrl+Shift+U Explicit Sequence with proper delays
        // Ctrl Down
        _inputSimulator.KeyPress(29, true); // Left Ctrl
        _inputSimulator.Sync();
        await Task.Delay(10);
        
        // Shift Down
        _inputSimulator.KeyPress(42, true); // Left Shift
        _inputSimulator.Sync();
        await Task.Delay(10);
        
        // Press U (22)
        await SendKeyAsync(22); 
        
        // Release Modifiers
        // Shift Up
        _inputSimulator.KeyPress(42, false);
        _inputSimulator.Sync();
        await Task.Delay(10);
        
        // Ctrl Up
        _inputSimulator.KeyPress(29, false);
        _inputSimulator.Sync();
        
        // CRITICAL: Wait for the IME/Toolkit to display the underlined 'u'
        // Increased to 200ms to ensure stability on slower systems/compositors
        await Task.Delay(200);

        // 2. Type Hex Digits Fast
        string hex = codePoint.ToString("x");
        foreach (char h in hex)
        {
            var input = _layoutService.GetInputForChar(h);
            if (input.HasValue)
            {
                await SendKeyAsync(input.Value.KeyCode, input.Value.Shift, input.Value.AltGr);
            }
            await Task.Delay(5); // Fast typing (5ms)
        }
        
        await Task.Delay(20);
        
        // 3. Commit
        Log.Debug("Committing Unicode entry...");
        await SendKeyAsync(28); // Enter
    }

    private async Task SendKeyAsync(int keyCode, bool shift = false, bool altGr = false, bool ctrl = false)
    {
        if (_inputSimulator == null) return;

        // Modifiers Down
        if (ctrl)
        {
            _inputSimulator.KeyPress(29, true);
            _inputSimulator.Sync();
        }
        if (shift) 
        {
            _inputSimulator.KeyPress(42, true);
            _inputSimulator.Sync();
        }
        if (altGr)
        {
            _inputSimulator.KeyPress(100, true);
            _inputSimulator.Sync();
        }

        // Key Press
        _inputSimulator.KeyPress(keyCode, true);
        _inputSimulator.Sync();
        
        // Hold key briefly to ensure OS registers it
        await Task.Delay(15); 

        // Key Release
        _inputSimulator.KeyPress(keyCode, false);
        _inputSimulator.Sync();

        // Modifiers Up (Reverse Order)
        if (altGr)
        {
            _inputSimulator.KeyPress(100, false);
            _inputSimulator.Sync();
        }
        if (shift) 
        {
            _inputSimulator.KeyPress(42, false);
            _inputSimulator.Sync();
        }
        if (ctrl)
        {
            _inputSimulator.KeyPress(29, false);
            _inputSimulator.Sync();
        }
    }
}
