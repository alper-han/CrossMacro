using System;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Core.Diagnostics;
using CrossMacro.Core.Services;
using Serilog;

namespace CrossMacro.Infrastructure.Services;

/// <summary>
/// Service for interactively capturing mouse coordinates and keyboard keys.
/// Uses existing platform input capture infrastructure.
/// </summary>
public class CoordinateCaptureService : ICoordinateCaptureService
{
    private readonly IMousePositionProvider _positionProvider;
    private readonly Func<IInputCapture>? _inputCaptureFactory;
    private readonly Lock _lock = new();
    
    private CancellationTokenSource? _currentCts;
    
    public CoordinateCaptureService(
        IMousePositionProvider positionProvider,
        Func<IInputCapture>? inputCaptureFactory = null)
    {
        _positionProvider = positionProvider ?? throw new ArgumentNullException(nameof(positionProvider));
        _inputCaptureFactory = inputCaptureFactory;
    }
    
    /// <inheritdoc/>
    public bool IsCapturing
    {
        get
        {
            lock (_lock)
            {
                return _currentCts != null && !_currentCts.IsCancellationRequested;
            }
        }
    }
    
    /// <inheritdoc/>
    public async Task<(int X, int Y)?> CaptureMousePositionAsync(CancellationToken ct = default)
    {
        var captureCts = BeginCapture(ct);
        
        try
        {
            if (_inputCaptureFactory == null)
            {
                // Fallback: Just get current position immediately
                Log.Warning("[CoordinateCaptureService] No input capture factory available, using current position");
                return await _positionProvider.GetAbsolutePositionAsync();
            }
            
            using var capture = _inputCaptureFactory();
            capture.Configure(captureMouse: true, captureKeyboard: true);
            
            var tcs = new TaskCompletionSource<(int X, int Y)?>(TaskCreationOptions.RunContinuationsAsynchronously);
            
            capture.InputReceived += async (s, e) =>
            {
                // Cancel on ESC key (keycode 1)
                if (e.Type == InputEventType.Key && e.Value == 1 && e.Code == InputEventCode.KEY_ESC)
                {
                    tcs.TrySetResult(null);
                    return;
                }
                
                // Capture on any mouse button click or Enter key (keycode 28)
                if ((e.Type == InputEventType.MouseButton && e.Value == 1) || // Button press
                    (e.Type == InputEventType.Key && e.Value == 1 && e.Code == InputEventCode.KEY_ENTER))
                {
                    var position = await _positionProvider.GetAbsolutePositionAsync();
                    tcs.TrySetResult(position);
                }
            };

            capture.Error += (s, error) =>
            {
                if (InputBackendErrorClassifier.IsKnownUnavailableMessage(error))
                {
                    Log.Warning("[CoordinateCaptureService] Mouse position capture unavailable: {Error}", error);
                }
                else
                {
                    Log.Error("[CoordinateCaptureService] Input capture error while capturing mouse position: {Error}", error);
                }

                tcs.TrySetResult(null);
            };

            using (captureCts.Token.Register(() => tcs.TrySetResult(null)))
            {
                await capture.StartAsync(captureCts.Token);
                return await tcs.Task;
            }
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (Exception ex)
        {
            if (InputBackendErrorClassifier.IsKnownUnavailable(ex))
            {
                Log.Warning("[CoordinateCaptureService] Mouse position capture skipped: {Error}", ex.Message);
                return null;
            }

            Log.Error(ex, "[CoordinateCaptureService] Error during mouse position capture");
            return null;
        }
        finally
        {
            EndCapture(captureCts);
        }
    }
    
    /// <inheritdoc/>
    public async Task<int?> CaptureKeyCodeAsync(CancellationToken ct = default)
    {
        var captureCts = BeginCapture(ct);
        
        try
        {
            if (_inputCaptureFactory == null)
            {
                Log.Warning("[CoordinateCaptureService] No input capture factory available");
                return null;
            }
            
            using var capture = _inputCaptureFactory();
            capture.Configure(captureMouse: false, captureKeyboard: true);
            
            var tcs = new TaskCompletionSource<int?>(TaskCreationOptions.RunContinuationsAsynchronously);
            
            capture.InputReceived += (s, e) =>
            {
                // Capture any keyboard key press (value == 1 means press)
                // ESC is a valid key and should be captured, not used for cancellation
                if (e.Type == InputEventType.Key && e.Value == 1)
                {
                    tcs.TrySetResult(e.Code);
                }
            };

            capture.Error += (s, error) =>
            {
                if (InputBackendErrorClassifier.IsKnownUnavailableMessage(error))
                {
                    Log.Warning("[CoordinateCaptureService] Key code capture unavailable: {Error}", error);
                }
                else
                {
                    Log.Error("[CoordinateCaptureService] Input capture error while capturing key code: {Error}", error);
                }

                tcs.TrySetResult(null);
            };

            using (captureCts.Token.Register(() => tcs.TrySetResult(null)))
            {
                await capture.StartAsync(captureCts.Token);
                return await tcs.Task;
            }
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (Exception ex)
        {
            if (InputBackendErrorClassifier.IsKnownUnavailable(ex))
            {
                Log.Warning("[CoordinateCaptureService] Key code capture skipped: {Error}", ex.Message);
                return null;
            }

            Log.Error(ex, "[CoordinateCaptureService] Error during key code capture");
            return null;
        }
        finally
        {
            EndCapture(captureCts);
        }
    }
    
    /// <inheritdoc/>
    public void CancelCapture()
    {
        CancellationTokenSource? ctsToCancel;
        lock (_lock)
        {
            ctsToCancel = _currentCts;
            _currentCts = null;
        }

        try
        {
            ctsToCancel?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Already disposed, ignore
        }
    }

    private CancellationTokenSource BeginCapture(CancellationToken externalToken)
    {
        CancellationTokenSource? previousCts;
        var captureCts = CancellationTokenSource.CreateLinkedTokenSource(externalToken);

        lock (_lock)
        {
            previousCts = _currentCts;
            _currentCts = captureCts;
        }

        try
        {
            previousCts?.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }

        return captureCts;
    }

    private void EndCapture(CancellationTokenSource captureCts)
    {
        lock (_lock)
        {
            if (ReferenceEquals(_currentCts, captureCts))
            {
                _currentCts = null;
            }
        }
    }
}
