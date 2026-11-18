
namespace CrossMacro.Infrastructure.Services.Input;

/// <summary>
/// Interactively captures a mouse position or a keyboard key through the platform input capture.
/// </summary>
public sealed class CoordinateCaptureService(
    IMousePositionProvider positionProvider,
    Func<IInputCapture>? inputCaptureFactory = null) : ICoordinateCaptureService
{
    private readonly IMousePositionProvider _positionProvider = positionProvider ?? throw new ArgumentNullException(nameof(positionProvider));
    private readonly Func<IInputCapture>? _inputCaptureFactory = inputCaptureFactory;
    private readonly Lock _lock = new();

    private CancellationTokenSource? _currentCts;

    /// <inheritdoc/>
    public bool IsCapturing
    {
        get
        {
            using (_lock.EnterScope())
            {
                return _currentCts is not null && !_currentCts.IsCancellationRequested;
            }
        }
    }

    /// <inheritdoc/>
    public Task<(int X, int Y)?> CaptureMousePositionAsync(CancellationToken ct = default)
    {
        return CaptureAsync<(int X, int Y)>(
            "Mouse position",
            captureMouse: true,
            HandleMouseInputAsync,
            async () =>
            {
                // No capture backend: the current position is the best answer available.
                Log.Warning("[CoordinateCaptureService] No input capture factory available, using current position");
                return await _positionProvider.GetAbsolutePositionAsync().ConfigureAwait(false);
            },
            ct);
    }

    /// <inheritdoc/>
    public Task<int?> CaptureKeyCodeAsync(CancellationToken ct = default)
    {
        return CaptureAsync<int>(
            "Key code",
            captureMouse: false,
            HandleKeyInputAsync,
            () =>
            {
                Log.Warning("[CoordinateCaptureService] No input capture factory available");
                return Task.FromResult<int?>(null);
            },
            ct);
    }

    /// <inheritdoc/>
    public void CancelCapture()
    {
        CancellationTokenSource? ctsToCancel;
        using (_lock.EnterScope())
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
            Log.Debug("[CoordinateCaptureService] Previous capture cancellation source was already disposed.");
        }
    }

    private async Task<T?> CaptureAsync<T>(
        string operation,
        bool captureMouse,
        Func<CapturedInputEventArgs, TaskCompletionSource<T?>, Task> handleInput,
        Func<Task<T?>> withoutCapture,
        CancellationToken externalToken)
        where T : struct
    {
        var captureCts = await BeginCaptureAsync(externalToken).ConfigureAwait(false);

        try
        {
            captureCts.Token.ThrowIfCancellationRequested();

            if (_inputCaptureFactory is null)
            {
                return await withoutCapture().ConfigureAwait(false);
            }

            using var capture = _inputCaptureFactory();
            capture.Configure(captureMouse, captureKeyboard: true);

            var completion = new TaskCompletionSource<T?>(TaskCreationOptions.RunContinuationsAsynchronously);
            var callbackTasks = new List<Task>();

            capture.InputReceived += (_, e) =>
            {
                var callback = handleInput(e, completion);
                lock (callbackTasks)
                {
                    callbackTasks.Add(callback);
                }
            };

            capture.CaptureError += (sender, error) =>
            {
                if (InputBackendErrorClassifier.IsKnownUnavailableMessage(error.Message))
                {
                    Log.Warning("[CoordinateCaptureService] {Operation} capture unavailable: {Error}", operation, error.Message);
                }
                else
                {
                    Log.LogError("[CoordinateCaptureService] Input capture error while capturing {Operation}: {Error}", operation, error.Message);
                }

                _ = completion.TrySetResult(null);
            };

            using (captureCts.Token.Register(() => completion.TrySetResult(null)))
            {
                await capture.StartAsync(captureCts.Token).ConfigureAwait(false);
                var result = await completion.Task.ConfigureAwait(false);

                Task[] pendingCallbacks;
                lock (callbackTasks)
                {
                    pendingCallbacks = [.. callbackTasks];
                }

                await Task.WhenAll(pendingCallbacks).ConfigureAwait(false);
                return result;
            }
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            if (InputBackendErrorClassifier.IsKnownUnavailable(ex))
            {
                Log.Warning("[CoordinateCaptureService] {Operation} capture skipped: {Error}", operation, ex.Message);
                return null;
            }

            Log.LogError(ex, "[CoordinateCaptureService] Error during {Operation} capture", operation);
            return null;
        }
        finally
        {
            EndCapture(captureCts);
        }
    }

    private async Task HandleMouseInputAsync(
        CapturedInputEventArgs input,
        TaskCompletionSource<(int X, int Y)?> completion)
    {
        try
        {
            if (input.Type is InputEventType.Key && input.Value is 1 && input.Code == InputEventCode.KEY_ESC)
            {
                _ = completion.TrySetResult(null);
                return;
            }

            if ((input.Type is InputEventType.MouseButton && input.Value is 1)
                || (input.Type is InputEventType.Key && input.Value is 1 && input.Code == InputEventCode.KEY_ENTER))
            {
                var position = await _positionProvider.GetAbsolutePositionAsync().ConfigureAwait(false);
                _ = completion.TrySetResult(position);
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.LogError(ex, "[CoordinateCaptureService] Error processing mouse capture callback");
            _ = completion.TrySetResult(null);
        }
    }

    // ESC is a valid key here and is captured like any other, not treated as cancellation.
    private static Task HandleKeyInputAsync(CapturedInputEventArgs input, TaskCompletionSource<int?> completion)
    {
        if (input.Type is InputEventType.Key && input.Value is 1)
        {
            _ = completion.TrySetResult(input.Code);
        }

        return Task.CompletedTask;
    }

    private async Task<CancellationTokenSource> BeginCaptureAsync(CancellationToken externalToken)
    {
        CancellationTokenSource? previousCts;
        var captureCts = CancellationTokenSource.CreateLinkedTokenSource(externalToken);

        using (_lock.EnterScope())
        {
            previousCts = _currentCts;
            _currentCts = captureCts;
        }

        if (previousCts is not null)
        {
            try
            {
                await previousCts.CancelAsync().ConfigureAwait(false);
            }
            catch (ObjectDisposedException)
            {
                // The previous capture may already have disposed its cancellation source.
            }
        }

        return captureCts;
    }

    private void EndCapture(CancellationTokenSource captureCts)
    {
        using (_lock.EnterScope())
        {
            if (ReferenceEquals(_currentCts, captureCts))
            {
                _currentCts = null;
            }
        }

        captureCts.Dispose();
    }
}
