using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services.Recording.Processors;
using CrossMacro.Core.Services.Recording.Strategies;
using Serilog;

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

        bool useAbsoluteCoordinates = !forceRelative; // Strategy Factory handles the rest

        var ignoredKeysList = ignoredKeys?.ToList();
        Log.Debug("[MacroRecorder] Configuration: Mouse={Mouse}, Keyboard={Keyboard}, Absolute={Absolute}, ForceRelative={ForceRelative}, SkipInitialZero={SkipZero}, IgnoredKeys={IgnoredKeys}",
            recordMouse, recordKeyboard, useAbsoluteCoordinates, forceRelative, skipInitialZero,
            ignoredKeysList != null ? string.Join(",", ignoredKeysList) : "none");

        _currentSequence = new MacroSequence
        {
            Name = "New Macro",
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
            
            
            // 0. Perform Corner Reset if needed
            // This is a physical environment preparation step, best handled here before capture starts.
            if (forceRelative && !skipInitialZero && _inputSimulatorFactory != null)
            {
                 try
                 {
                     Log.Information("[MacroRecorder] Performing Corner Reset (Force 0,0)...");
                     // We create a temporary simulator just for this op
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

            // 1. Initialize Strategy
            _currentStrategy = _coordinateStrategyFactory.Create(useAbsoluteCoordinates, forceRelative, skipInitialZero);
            await _currentStrategy.InitializeAsync(cancellationToken);

            // 2. Initialize Processor
            _currentProcessor = _processorFactory(_currentStrategy);
            _currentProcessor.Configure(recordMouse, recordKeyboard, ignoredKeys != null ? new HashSet<int>(ignoredKeys) : null, useAbsoluteCoordinates);

            // 3. Initialize Capture
            _inputCapture = _inputCaptureFactory();
            _inputCapture.Configure(recordMouse, recordKeyboard);
            _inputCapture.InputReceived += OnInputReceived;
            _inputCapture.Error += OnInputCaptureError;
            
            await _inputCapture.StartAsync(cancellationToken);
            
            Log.Information("[MacroRecorder] Recording started via {ProviderName}", _inputCapture.ProviderName);
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
                // NOTE: Do NOT call Stop() or Dispose() here!
                // LinuxIpcInputCapture is now a Singleton shared with GlobalHotkeyService.
                // GlobalHotkeyService manages the capture lifecycle.
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
    
    public void Dispose()
    {
        CleanupComponents();
    }
}
