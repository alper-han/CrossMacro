using System;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services;
using CrossMacro.Core.Services.TextExpansion;
using Serilog;

namespace CrossMacro.Infrastructure.Services;

/// <summary>
/// Service for monitoring keystrokes and performing text expansion.
/// Refactored to coordinate InputProcessor, BufferState, and Executor.
/// </summary>
public class TextExpansionService : ITextExpansionService
{
    private readonly ISettingsService _settingsService;
    private readonly ITextExpansionStorageService _storageService;
    private readonly Func<IInputCapture> _inputCaptureFactory;
    
    // Decomposed Components
    private readonly IInputProcessor _inputProcessor;
    private readonly ITextBufferState _bufferState;
    private readonly ITextExpansionExecutor _startExecutor;
    
    // Lifecycle management
    private IInputCapture? _inputCapture;
    private CancellationTokenSource? _captureCts;
    private Task? _captureTask;
    private readonly Lock _lock;
    private bool _isRunning;
    private readonly SemaphoreSlim _expansionLock; 

    public bool IsRunning => _isRunning;

    public TextExpansionService(
        ISettingsService settingsService, 
        ITextExpansionStorageService storageService,
        Func<IInputCapture> inputCaptureFactory,
        IInputProcessor inputProcessor,
        ITextBufferState bufferState,
        ITextExpansionExecutor startExecutor)
    {
        _settingsService = settingsService;
        _storageService = storageService;
        _inputCaptureFactory = inputCaptureFactory;
        
        _inputProcessor = inputProcessor;
        _bufferState = bufferState;
        _startExecutor = startExecutor;
        
        _lock = new Lock();
        _expansionLock = new SemaphoreSlim(1, 1);
        
        // Subscribe to Processor events
        _inputProcessor.CharacterReceived += OnCharacterReceived;
        _inputProcessor.SpecialKeyReceived += OnSpecialKeyReceived;
    }

    public void Start()
    {
        if (!_settingsService.Current.EnableTextExpansion)
        {
            Log.Information("[TextExpansionService] Not starting because feature is disabled");
            return;
        }

        lock (_lock)
        {
            if (_isRunning) return;

            try
            {
                // Initialize Capture
                _inputCapture = _inputCaptureFactory();
                _inputCapture.Configure(captureMouse: false, captureKeyboard: true);
                _inputCapture.InputReceived += OnInputReceived;
                _inputCapture.Error += OnInputCaptureError;
                _captureCts = new CancellationTokenSource();
                _captureTask = _inputCapture.StartAsync(_captureCts.Token) ?? Task.CompletedTask;
                _ = ObserveCaptureTaskAsync(_captureTask, _captureCts.Token);
                
                // Reset State
                _inputProcessor.Reset();
                _bufferState.Clear();
                
                _isRunning = true;
                Log.Information("[TextExpansionService] Started via {Provider}", _inputCapture.ProviderName);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[TextExpansionService] Failed to start");
                Stop();
            }
        }
    }

    public void Stop()
    {
        lock (_lock)
        {
            if (!_isRunning) return;

            try 
            {
                _captureCts?.Cancel();

                if (_inputCapture != null)
                {
                    _inputCapture.InputReceived -= OnInputReceived;
                    _inputCapture.Error -= OnInputCaptureError;
                    _inputCapture.Stop();
                    _inputCapture.Dispose();
                    _inputCapture = null;
                }

                _captureCts?.Dispose();
                _captureCts = null;
                _captureTask = null;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[TextExpansionService] Error stopping");
            }

            _isRunning = false;
            Log.Information("[TextExpansionService] Stopped");
        }
    }

    public void Dispose()
    {
        Stop();
        _inputProcessor.CharacterReceived -= OnCharacterReceived;
        _inputProcessor.SpecialKeyReceived -= OnSpecialKeyReceived;
        _expansionLock.Dispose();
    }

    private void OnInputCaptureError(object? sender, string error)
    {
        Log.Error("[TextExpansionService] Capture error: {Error}", error);
    }

    private async Task ObserveCaptureTaskAsync(Task captureTask, CancellationToken token)
    {
        try
        {
            await captureTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // Normal shutdown path.
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[TextExpansionService] Capture task faulted");
            OnInputCaptureError(this, ex.Message);
        }
    }

    private void OnInputReceived(object? sender, InputCaptureEventArgs e)
    {
        lock (_lock)
        {
            if (!_isRunning) return;
            // Delegate to Processor
            _inputProcessor.ProcessEvent(e);
        }
    }

    private void OnCharacterReceived(char c)
    {
        // Update Buffer
        _bufferState.Append(c);

        Log.Debug("[TextExpansionService] Buffer: '{Buffer}' (added: '{Char}')",
            _bufferState.ToString(), c);

        // Check for Trigger
        var expansions = _storageService.GetCurrent();
        if (_bufferState.TryGetMatch(expansions, out var match) && match != null)
        {
             Log.Information("[TextExpansionService] Trigger detected: '{Trigger}' -> expanding to '{Replacement}'",
                 match.Trigger, match.Replacement.Length > 50 ? match.Replacement[..50] + "..." : match.Replacement);

             // Clear buffer immediately to prevent re-triggering
             _bufferState.Clear();

             // Run Execution
             _ = Task.Run(() => RunExpansionSafelyAsync(match));
        }
    }

    private void OnSpecialKeyReceived(int keyCode)
    {
        if (keyCode == 14) // Backspace
        {
            _bufferState.Backspace();
            Log.Debug("[TextExpansionService] Backspace - Buffer: '{Buffer}'", _bufferState.ToString());
        }
        else if (keyCode == 28) // Enter
        {
             _bufferState.Clear();
             Log.Debug("[TextExpansionService] Enter - Buffer cleared");
        }
    }

    private async Task PerformExpansionAsync(Core.Models.TextExpansion expansion)
    {
        // Ensure serialization of expansions
        await _expansionLock.WaitAsync();
        try
        {
            // Wait for Modifiers to be released (Safety)
            int timeoutMs = 2000;
            int elapsed = 0;
            while (_inputProcessor.AreModifiersPressed && elapsed < timeoutMs)
            {
                await Task.Delay(50);
                elapsed += 50;
            }

            Log.Debug("[TextExpansionService] Executing expansion: '{Trigger}' -> {ReplacementLength} chars",
                expansion.Trigger, expansion.Replacement.Length);

            await _startExecutor.ExpandAsync(expansion);

            Log.Debug("[TextExpansionService] Expansion completed: '{Trigger}'", expansion.Trigger);
        }
        finally
        {
            _expansionLock.Release();
        }
    }

    private async Task RunExpansionSafelyAsync(Core.Models.TextExpansion expansion)
    {
        try
        {
            await PerformExpansionAsync(expansion).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[TextExpansionService] Expansion failed for trigger '{Trigger}'", expansion.Trigger);
        }
    }
}
