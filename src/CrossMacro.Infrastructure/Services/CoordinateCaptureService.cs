using System;
using System.Threading;
using System.Threading.Tasks;
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
    
    private CancellationTokenSource? _currentCts;
    
    public CoordinateCaptureService(
        IMousePositionProvider positionProvider,
        Func<IInputCapture>? inputCaptureFactory = null)
    {
        _positionProvider = positionProvider ?? throw new ArgumentNullException(nameof(positionProvider));
        _inputCaptureFactory = inputCaptureFactory;
    }
    
    /// <inheritdoc/>
    public bool IsCapturing => _currentCts != null && !_currentCts.IsCancellationRequested;
    
    /// <inheritdoc/>
    public async Task<(int X, int Y)?> CaptureMousePositionAsync(CancellationToken ct = default)
    {
        CancelCapture();
        _currentCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        
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
            
            var tcs = new TaskCompletionSource<(int X, int Y)?>();
            
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
            
            _ = capture.StartAsync(_currentCts.Token);
            
            using (_currentCts.Token.Register(() => tcs.TrySetResult(null)))
            {
                return await tcs.Task;
            }
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[CoordinateCaptureService] Error during mouse position capture");
            return null;
        }
        finally
        {
            _currentCts = null;
        }
    }
    
    /// <inheritdoc/>
    public async Task<int?> CaptureKeyCodeAsync(CancellationToken ct = default)
    {
        CancelCapture();
        _currentCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        
        try
        {
            if (_inputCaptureFactory == null)
            {
                Log.Warning("[CoordinateCaptureService] No input capture factory available");
                return null;
            }
            
            using var capture = _inputCaptureFactory();
            capture.Configure(captureMouse: false, captureKeyboard: true);
            
            var tcs = new TaskCompletionSource<int?>();
            
            capture.InputReceived += (s, e) =>
            {
                // Capture any keyboard key press (value == 1 means press)
                // ESC is a valid key and should be captured, not used for cancellation
                if (e.Type == InputEventType.Key && e.Value == 1)
                {
                    tcs.TrySetResult(e.Code);
                }
            };
            
            _ = capture.StartAsync(_currentCts.Token);
            
            using (_currentCts.Token.Register(() => tcs.TrySetResult(null)))
            {
                return await tcs.Task;
            }
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[CoordinateCaptureService] Error during key code capture");
            return null;
        }
        finally
        {
            _currentCts = null;
        }
    }
    
    /// <inheritdoc/>
    public void CancelCapture()
    {
        try
        {
            _currentCts?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Already disposed, ignore
        }
    }
}
