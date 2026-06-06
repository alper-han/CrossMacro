using System;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Core.Services;

namespace CrossMacro.Infrastructure.Services;

internal sealed class InputCaptureLifecycle
{
    private IInputCapture? _capture;
    private CancellationTokenSource? _captureCts;
    private Task? _captureTask;

    public Task? CaptureTask => _captureTask;

    public bool HasActiveResources => _capture != null || _captureCts != null || _captureTask != null;

    public bool IsCurrent(IInputCapture capture)
    {
        return ReferenceEquals(_capture, capture);
    }

    public void Start(
        Func<IInputCapture> inputCaptureFactory,
        bool captureMouse,
        bool captureKeyboard,
        bool captureGamepad,
        EventHandler<InputCaptureEventArgs> onInputReceived,
        EventHandler<string> onError,
        Action<IInputCapture> onStarted,
        Action<IInputCapture, Exception> onFault)
    {
        var capture = inputCaptureFactory();
        capture.Configure(captureMouse, captureKeyboard, captureGamepad);
        capture.InputReceived += onInputReceived;
        capture.Error += onError;

        var captureCts = new CancellationTokenSource();
        _capture = capture;
        _captureCts = captureCts;
        _captureTask = null;

        var captureTask = capture.StartAsync(captureCts.Token) ?? Task.CompletedTask;
        _captureTask = captureTask;
        _ = ObserveStartupTaskAsync(capture, captureTask, captureCts.Token, onStarted, onFault);
    }

    public void Cleanup(
        EventHandler<InputCaptureEventArgs> onInputReceived,
        EventHandler<string> onError,
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
            if (capture != null)
            {
                capture.InputReceived -= onInputReceived;
                capture.Error -= onError;
                capture.Stop();
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
