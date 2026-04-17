using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Core.Diagnostics;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services.Recording.Processors;
using CrossMacro.Core.Services.Recording.Strategies;
using CrossMacro.Core.Logging;

namespace CrossMacro.Core.Services;

public class MacroRecorder : IMacroRecorder, IDisposable
{
    private bool _isRecording;
    private MacroSequence? _currentSequence;
    private Stopwatch? _stopwatch;
    private IInputCapture? _inputCapture;
    private readonly Lock _eventLock = new();
    
    private readonly Func<IInputCapture>? _inputCaptureFactory;
    private readonly ICoordinateStrategyFactory _coordinateStrategyFactory;
    private readonly Func<ICoordinateStrategy, IInputEventProcessor> _processorFactory;
    
    private readonly Func<IInputSimulator>? _inputSimulatorFactory;
    
    // Active components
    private ICoordinateStrategy? _currentStrategy;
    private IInputEventProcessor? _currentProcessor;
    
    public event EventHandler<MacroEvent>? EventRecorded;
    
    public bool IsRecording => _isRecording;

    public MacroRecorder(
        Func<IInputCapture>? inputCaptureFactory,
        ICoordinateStrategyFactory coordinateStrategyFactory,
        Func<ICoordinateStrategy, IInputEventProcessor> processorFactory,
        Func<IInputSimulator>? inputSimulatorFactory = null)
    {
        _inputCaptureFactory = inputCaptureFactory;
        _coordinateStrategyFactory = coordinateStrategyFactory;
        _processorFactory = processorFactory;
        _inputSimulatorFactory = inputSimulatorFactory;
    }

    public async Task StartRecordingAsync(bool recordMouse, bool recordKeyboard, IEnumerable<int>? ignoredKeys = null, bool forceRelative = false, bool skipInitialZero = false, CancellationToken cancellationToken = default)
    {
        if (_isRecording)
            return;

        if (!recordMouse && !recordKeyboard)
            throw new ArgumentException("At least one recording type (mouse or keyboard) must be enabled");

        _isRecording = true;

        bool requestedAbsoluteCoordinates = !forceRelative; // Strategy factory may adjust this based on platform capability.
        bool useAbsoluteCoordinates = requestedAbsoluteCoordinates;

        var ignoredKeysList = ignoredKeys?.ToList();
        Log.Debug("[MacroRecorder] Configuration: Mouse={Mouse}, Keyboard={Keyboard}, RequestedAbsolute={RequestedAbsolute}, ForceRelative={ForceRelative}, SkipInitialZero={SkipZero}, IgnoredKeys={IgnoredKeys}",
            recordMouse, recordKeyboard, useAbsoluteCoordinates, forceRelative, skipInitialZero,
            ignoredKeysList != null ? string.Join(",", ignoredKeysList) : "none");

        _currentSequence = new MacroSequence
        {
            Name = MacroNameDefaults.NewRecordedMacroName,
            CreatedAt = DateTime.UtcNow,
            IsAbsoluteCoordinates = useAbsoluteCoordinates,
            SkipInitialZeroZero = skipInitialZero
        };

        _stopwatch = Stopwatch.StartNew();

        try
        {
            if (_inputCaptureFactory == null)
            {
                throw new InvalidOperationException("No input capture factory configured. Please provide IInputCapture factory via DI.");
            }
            
            
            // 1. Initialize Strategy
            _currentStrategy = _coordinateStrategyFactory.Create(requestedAbsoluteCoordinates, forceRelative, skipInitialZero);
            useAbsoluteCoordinates = DetermineEffectiveAbsoluteCoordinates(requestedAbsoluteCoordinates, _currentStrategy);
            if (useAbsoluteCoordinates != requestedAbsoluteCoordinates)
            {
                Log.Information(
                    "[MacroRecorder] Coordinate mode auto-adjusted from {RequestedMode} to {EffectiveMode} based on strategy {StrategyType}.",
                    requestedAbsoluteCoordinates ? "absolute" : "relative",
                    useAbsoluteCoordinates ? "absolute" : "relative",
                    _currentStrategy.GetType().Name);
            }

            _currentSequence.IsAbsoluteCoordinates = useAbsoluteCoordinates;

            // 2. Perform Corner Reset for relative recordings when requested.
            if (!useAbsoluteCoordinates && !skipInitialZero)
            {
                PerformCornerReset();
            }

            await _currentStrategy.InitializeAsync(cancellationToken);

            // 3. Initialize Processor
            _currentProcessor = _processorFactory(_currentStrategy);
            _currentProcessor.Configure(recordMouse, recordKeyboard, ignoredKeys != null ? new HashSet<int>(ignoredKeys) : null, useAbsoluteCoordinates);

            // 4. Initialize Capture
            _inputCapture = _inputCaptureFactory();
            var inputCapture = _inputCapture;
            var providerName = inputCapture.ProviderName;
            inputCapture.Configure(recordMouse, recordKeyboard);
            inputCapture.InputReceived += OnInputReceived;
            inputCapture.Error += OnInputCaptureError;

            // StartAsync can complete after StopRecording() cleanup; keep a local reference to avoid races.
            Log.Information("[MacroRecorder] Recording started via {ProviderName}", providerName);
            await inputCapture.StartAsync(cancellationToken);
        }
        catch (Exception)
        {
            _isRecording = false;
            CleanupComponents();
            throw;
        }
    }

    private void OnInputCaptureError(object? sender, string errorMessage)
    {
        if (InputBackendErrorClassifier.IsKnownUnavailableMessage(errorMessage))
        {
            Log.Warning("[MacroRecorder] Input capture unavailable: {Error}", errorMessage);
            return;
        }

        Log.Error("[MacroRecorder] Input capture error: {Error}", errorMessage);
    }


    private void OnInputReceived(object? sender, InputCaptureEventArgs e)
    {
        using (_eventLock.EnterScope())
        {
            if (!_isRecording || _currentSequence == null || _stopwatch == null || _currentProcessor == null) return;

            try
            {
                var macroEvent = _currentProcessor.Process(e, _stopwatch.ElapsedMilliseconds);
                
                if (macroEvent != null)
                {
                    AddMacroEvent(macroEvent.Value);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[MacroRecorder] Error processing input event");
            }
        }
    }

    private void AddMacroEvent(MacroEvent macroEvent)
    {
        if (_currentSequence != null)
        {
            if (_currentSequence.Events.Count > 0)
            {
                var lastEvent = _currentSequence.Events[^1];
                macroEvent.DelayMs = (int)(macroEvent.Timestamp - lastEvent.Timestamp);
            }
            else
            {
                macroEvent.DelayMs = 0;
            }

            _currentSequence.Events.Add(macroEvent);

            Log.Debug("[MacroRecorder] Event #{Count}: {Type} | X={X} Y={Y} | Key={Key} Button={Button} | Delay={Delay}ms",
                _currentSequence.Events.Count, macroEvent.Type, macroEvent.X, macroEvent.Y,
                macroEvent.KeyCode, macroEvent.Button, macroEvent.DelayMs);

            EventRecorded?.Invoke(this, macroEvent);
        }
    }

    public MacroSequence StopRecording()
    {
        if (!_isRecording)
            throw new InvalidOperationException("Not currently recording");

        Log.Information("[MacroRecorder] Stopping recording...");
        
        _isRecording = false;
        _stopwatch?.Stop();
        
        CleanupComponents();

        if (_currentSequence != null && _stopwatch != null)
        {
            FinalizeSequence(_currentSequence, _stopwatch);
        }

        return _currentSequence ?? new MacroSequence();
    }
    
    private void FinalizeSequence(MacroSequence sequence, Stopwatch stopwatch)
    {
        sequence.CalculateDuration();
        sequence.RecordedAt = DateTime.UtcNow;
        sequence.ActualDuration = stopwatch.Elapsed;
        
        sequence.MouseMoveCount = sequence.Events.Count(e => e.Type == Models.EventType.MouseMove);
        sequence.ClickCount = sequence.Events.Count(e => 
            e.Type == Models.EventType.Click || 
            e.Type == Models.EventType.ButtonPress || 
            e.Type == Models.EventType.ButtonRelease);
        
        if (stopwatch.Elapsed.TotalSeconds > 0)
        {
             sequence.EventsPerSecond = sequence.Events.Count / stopwatch.Elapsed.TotalSeconds;
        }
        
        // Debug: Count event types
        var moveCount = sequence.Events.Count(e => e.Type == Models.EventType.MouseMove);
        var buttonCount = sequence.Events.Count(e => e.Type == Models.EventType.ButtonPress || e.Type == Models.EventType.ButtonRelease);
        var nonZeroMoves = sequence.Events.Where(e => e.Type == Models.EventType.MouseMove && (e.X != 0 || e.Y != 0)).Take(5).ToList();
        
        Log.Information("[MacroRecorder] Recording completed: Duration={Duration:F2}s, TotalEvents={Events}, MouseMoves={Moves}, Buttons={Buttons}", 
            stopwatch.Elapsed.TotalSeconds, sequence.Events.Count, moveCount, buttonCount);
        
        if (nonZeroMoves.Count > 0)
        {
            foreach (var m in nonZeroMoves)
            {
                Log.Debug("[MacroRecorder] Sample Move: X={X}, Y={Y}", m.X, m.Y);
            }
        }
        else if (moveCount > 0)
        {
            Log.Warning("[MacroRecorder] All {Count} MouseMove events have X=0 and Y=0!", moveCount);
        }
    }
    
    private void CleanupComponents()
    {
        if (_inputCapture != null)
        {
            try
            {
                _inputCapture.InputReceived -= OnInputReceived;
                _inputCapture.Error -= OnInputCaptureError;
                _inputCapture.Stop();
                _inputCapture.Dispose();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[MacroRecorder] Error cleaning up input capture");
            }
            _inputCapture = null;
        }
        
        if (_currentStrategy != null)
        {
             try
             {
                 _currentStrategy.Dispose();
             }
             catch(Exception ex)
             {
                  Log.Error(ex, "[MacroRecorder] Error disposing strategy");
             }
             _currentStrategy = null;
        }
        _currentProcessor = null;
    }
    
    public MacroSequence? GetCurrentRecording()
    {
        return _currentSequence;
    }

    private static bool DetermineEffectiveAbsoluteCoordinates(bool requestedAbsoluteCoordinates, ICoordinateStrategy strategy)
    {
        if (!requestedAbsoluteCoordinates)
        {
            return false;
        }

        return strategy is not RelativeCoordinateStrategy;
    }

    private void PerformCornerReset()
    {
        if (_inputSimulatorFactory == null)
        {
            Log.Warning("[MacroRecorder] Relative recording requires corner reset, but no input simulator is available.");
            return;
        }

        try
        {
            Log.Information("[MacroRecorder] Performing Corner Reset (Force 0,0)...");
            using var simulator = _inputSimulatorFactory();
            simulator.Initialize();
            simulator.MoveRelative(-20000, -20000);
            Log.Information("[MacroRecorder] Corner Reset complete.");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[MacroRecorder] Failed to perform Corner Reset");
        }
    }
    
    public void Dispose()
    {
        CleanupComponents();
    }
}
