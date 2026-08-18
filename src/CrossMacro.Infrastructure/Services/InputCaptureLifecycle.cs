
namespace CrossMacro.Infrastructure.Services;

internal sealed class InputCaptureLifecycle
{
    private IInputCapture? _capture;
    private CancellationTokenSource? _captureCts;

    public Task? CaptureTask { get; private set; }

    public bool HasActiveResources => _capture is not null || _captureCts is not null || CaptureTask is not null;

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
        var (capture, captureTask, captureCts) = StartCore(
            inputCaptureFactory,
            captureMouse,
            captureKeyboard,
            onInputReceived,
            onError,
            CancellationToken.None);
        _ = ObserveStartupTaskAsync(capture, captureTask, onStarted, onFault, captureCts.Token);
    }

    public async Task StartAsync(
        Func<IInputCapture> inputCaptureFactory,
        bool captureMouse,
        bool captureKeyboard,
        EventHandler<CapturedInputEventArgs> onInputReceived,
        EventHandler<InputCaptureErrorEventArgs> onError,
        Action<IInputCapture> onStarted,
        Action<IInputCapture, Exception> onFault,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var (capture, captureTask, captureCts) = StartCore(
            inputCaptureFactory,
            captureMouse,
            captureKeyboard,
            onInputReceived,
            onError,
            cancellationToken);

        try
        {
            await captureTask.ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            onStarted(capture);
        }
        catch (OperationCanceledException) when (captureCts.IsCancellationRequested)
        {
            await CleanupAsync(
                capture,
                captureCts,
                captureTask,
                onInputReceived,
                onError,
                static ex => Log.Debug(ex, "[InputCaptureLifecycle] Error cleaning up canceled startup"))
                .ConfigureAwait(false);
            ClearOwnership(capture, captureCts, captureTask);
            throw;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            await CleanupAsync(
                capture,
                captureCts,
                captureTask,
                onInputReceived,
                onError,
                static cleanupException => Log.Debug(cleanupException, "[InputCaptureLifecycle] Error cleaning up failed startup"))
                .ConfigureAwait(false);
            ClearOwnership(capture, captureCts, captureTask);
            onFault(capture, ex);
            throw;
        }
        catch (OutOfMemoryException)
        {
            await CleanupAsync(
                capture,
                captureCts,
                captureTask,
                onInputReceived,
                onError,
                static ex => Log.Debug(ex, "[InputCaptureLifecycle] Error cleaning up failed startup"))
                .ConfigureAwait(false);
            ClearOwnership(capture, captureCts, captureTask);
            throw;
        }
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
        CaptureTask = null;

        try
        {
            captureCts?.Cancel();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            ReportCleanupError(onStopError, ex);
        }

        DetachHandlers(capture, onInputReceived, onError, onStopError);

        StopCapture(capture, onStopError);
        DisposeCapture(capture, onStopError);

        try
        {
            captureCts?.Dispose();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            ReportCleanupError(onStopError, ex);
        }
    }

    public async Task CleanupAsync(
        EventHandler<CapturedInputEventArgs> onInputReceived,
        EventHandler<InputCaptureErrorEventArgs> onError,
        Action<Exception> onStopError)
    {
        var capture = _capture;
        var captureCts = _captureCts;
        var captureTask = CaptureTask;

        _capture = null;
        _captureCts = null;
        CaptureTask = null;

        await CleanupAsync(
            capture,
            captureCts,
            captureTask,
            onInputReceived,
            onError,
            onStopError).ConfigureAwait(false);
    }

    private static async Task CleanupAsync(
        IInputCapture? capture,
        CancellationTokenSource? captureCts,
        Task? captureTask,
        EventHandler<CapturedInputEventArgs> onInputReceived,
        EventHandler<InputCaptureErrorEventArgs> onError,
        Action<Exception> onStopError)
    {
        if (captureCts is not null)
        {
            try
            {
                await captureCts.CancelAsync().ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                ReportCleanupError(onStopError, ex);
            }
        }

        DetachHandlers(capture, onInputReceived, onError, onStopError);

        if (capture is IAsyncDisposable asyncDisposable)
        {
            try
            {
                await asyncDisposable.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                ReportCleanupError(onStopError, ex);
            }
        }
        else
        {
            StopCapture(capture, onStopError);
            DisposeCapture(capture, onStopError);
        }

        if (captureTask is not null)
        {
            try
            {
                await captureTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Cancellation is the expected result while stopping capture.
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                ReportCleanupError(onStopError, ex);
            }
        }

        try
        {
            captureCts?.Dispose();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            ReportCleanupError(onStopError, ex);
        }
    }

    private (IInputCapture Capture, Task CaptureTask, CancellationTokenSource CaptureCts) StartCore(
        Func<IInputCapture> inputCaptureFactory,
        bool captureMouse,
        bool captureKeyboard,
        EventHandler<CapturedInputEventArgs> onInputReceived,
        EventHandler<InputCaptureErrorEventArgs> onError,
        CancellationToken cancellationToken = default)
    {
        var capture = inputCaptureFactory();
        var captureCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        try
        {
            capture.Configure(captureMouse, captureKeyboard);
            capture.InputReceived += onInputReceived;
            capture.CaptureError += onError;

            _capture = capture;
            _captureCts = captureCts;

            var captureTask = capture.StartAsync(captureCts.Token) ?? Task.CompletedTask;
            CaptureTask = captureTask;
            return (capture, captureTask, captureCts);
        }
        catch
        {
            capture.InputReceived -= onInputReceived;
            capture.CaptureError -= onError;
            try
            {
                capture.StopCapture();
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                // Cleanup is best effort; preserve the original capture setup failure.
            }

            try
            {
                captureCts.Dispose();
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                // Cleanup is best effort; preserve the original capture setup failure.
            }

            try
            {
                capture.Dispose();
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                // Cleanup is best effort; preserve the original capture setup failure.
            }
            if (ReferenceEquals(_capture, capture))
            {
                _capture = null;
                _captureCts = null;
                CaptureTask = null;
            }

            throw;
        }
    }

    private static void DetachHandlers(
        IInputCapture? capture,
        EventHandler<CapturedInputEventArgs> onInputReceived,
        EventHandler<InputCaptureErrorEventArgs> onError,
        Action<Exception> onStopError)
    {
        if (capture is null)
        {
            return;
        }

        try
        {
            capture.InputReceived -= onInputReceived;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            ReportCleanupError(onStopError, ex);
        }

        try
        {
            capture.CaptureError -= onError;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            ReportCleanupError(onStopError, ex);
        }
    }

    private void ClearOwnership(IInputCapture capture, CancellationTokenSource captureCts, Task captureTask)
    {
        if (ReferenceEquals(_capture, capture) &&
            ReferenceEquals(_captureCts, captureCts) &&
            ReferenceEquals(CaptureTask, captureTask))
        {
            _capture = null;
            _captureCts = null;
            CaptureTask = null;
        }
    }

    private static void StopCapture(IInputCapture? capture, Action<Exception> onStopError)
    {
        if (capture is null)
        {
            return;
        }

        try
        {
            capture.StopCapture();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            ReportCleanupError(onStopError, ex);
        }
    }

    private static void DisposeCapture(IInputCapture? capture, Action<Exception> onStopError)
    {
        if (capture is null)
        {
            return;
        }

        try
        {
            capture.Dispose();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            ReportCleanupError(onStopError, ex);
        }
    }

    private static void ReportCleanupError(Action<Exception> onStopError, Exception exception)
    {
        try
        {
            onStopError(exception);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.Debug(ex, "[InputCaptureLifecycle] Cleanup error reporter failed");
        }
    }

    private async Task ObserveStartupTaskAsync(
        IInputCapture capture,
        Task captureTask,
        Action<IInputCapture> onStarted,
        Action<IInputCapture, Exception> onFault,
        CancellationToken token)
    {
        try
        {
            await captureTask.ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            if (ReferenceEquals(_capture, capture))
            {
                onStarted(capture);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // Normal shutdown path.
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            onFault(capture, ex);
        }
    }
}
