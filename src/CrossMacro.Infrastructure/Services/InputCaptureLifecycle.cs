
namespace CrossMacro.Infrastructure.Services;

internal sealed class InputCaptureLifecycle
{
    private IInputCapture? _capture;
    private CancellationTokenSource? _captureCts;
    private Task? _captureTask;

    public Task? CaptureTask => _captureTask;

    public bool HasActiveResources => _capture is not null || _captureCts is not null || _captureTask is not null;

    public bool IsCurrent(IInputCapture capture)
    {
        return ReferenceEquals(_capture, capture);
    }

    public void Start(
        Func<IInputCapture> inputCaptureFactory,
        bool captureMouse,
        bool captureKeyboard,
        EventHandler<CapturedInputEventArgs> onInputReceived,
        EventHandler<InputCaptureErrorEventArgs> onError,
        Action<IInputCapture> onStarted,
        Action<IInputCapture, Exception> onFault)
    {
        var capture = inputCaptureFactory();
        capture.Configure(captureMouse, captureKeyboard);
        capture.InputReceived += onInputReceived;
        capture.CaptureError += onError;

        var captureCts = new CancellationTokenSource();
        _capture = capture;
        _captureCts = captureCts;
        _captureTask = null;

        var captureTask = capture.StartAsync(captureCts.Token) ?? Task.CompletedTask;
        _captureTask = captureTask;
        _ = ObserveStartupTaskAsync(capture, captureTask, captureCts.Token, onStarted, onFault);
    }

    public void Cleanup(
        EventHandler<CapturedInputEventArgs> onInputReceived,
        EventHandler<InputCaptureErrorEventArgs> onError,
        Action<Exception> onStopError)
    {
        var capture = _capture;
        var captureCts = _captureCts;

        _capture = null;
        _captureCts = null;
        _captureTask = null;

        try
        {
            captureCts?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Already disposed.
        }

        try
        {
            if (capture is not null)
            {
                capture.InputReceived -= onInputReceived;
                capture.CaptureError -= onError;
                capture.StopCapture();
                capture.Dispose();
            }
        }
        catch (Exception ex)
        {
            onStopError(ex);
        }
        finally
        {
            try
            {
                captureCts?.Dispose();
            }
            catch (ObjectDisposedException)
            {
                // Already disposed.
            }
        }
    }

    private static async Task ObserveStartupTaskAsync(
        IInputCapture capture,
        Task captureTask,
        CancellationToken token,
        Action<IInputCapture> onStarted,
        Action<IInputCapture, Exception> onFault)
    {
        try
        {
            await captureTask.ConfigureAwait(false);
            onStarted(capture);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // Normal shutdown path.
        }
        catch (Exception ex)
        {
            onFault(capture, ex);
        }
    }
}
