namespace CrossMacro.Platform.Linux.Native.UInput;

/// <summary>Owns the bounded, best-effort event-node readiness policy independently of native setup.</summary>
internal sealed class UInputDeviceReadiness(
    Func<string?> getSysname,
    Func<bool> canInspectRoot,
    Func<string, (string? Node, bool CanInspect)> probe,
    TimeProvider? timeProvider = null)
{
    private static readonly TimeSpan Timeout = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(5);
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    internal async Task WaitAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var sysname = getSysname();
        if (sysname is null || !canInspectRoot())
        {
            return;
        }

        var startedAt = _timeProvider.GetTimestamp();
        while (_timeProvider.GetElapsedTime(startedAt) < Timeout)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = probe(sysname);
            if (result.Node is not null)
            {
                Log.Debug("[UInputDevice] Virtual device {Node} is ready", result.Node);
                return;
            }

            if (!result.CanInspect)
            {
                return;
            }

            var remaining = Timeout - _timeProvider.GetElapsedTime(startedAt);
            if (remaining <= TimeSpan.Zero)
            {
                break;
            }

            await Task.Delay(remaining < PollInterval ? remaining : PollInterval, _timeProvider, cancellationToken).ConfigureAwait(false);
        }

        Log.Debug("[UInputDevice] Virtual device event node was not visible before readiness timeout");
    }
}
