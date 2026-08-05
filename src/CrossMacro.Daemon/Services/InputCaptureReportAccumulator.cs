namespace CrossMacro.Daemon.Services;

/// <summary>
/// Buffers one evdev report and releases it atomically at SYN_REPORT.
/// </summary>
internal sealed class InputCaptureReportAccumulator
{
    private readonly bool _captureMouse;
    private readonly bool _captureKeyboard;
    private readonly List<UInputNative.input_event> _pending = new(capacity: 8);

    internal InputCaptureReportAccumulator(bool captureMouse, bool captureKeyboard)
    {
        _captureMouse = captureMouse;
        _captureKeyboard = captureKeyboard;
    }

    internal bool TryAppend(
        UInputNative.input_event inputEvent,
        out List<UInputNative.input_event>? completedReport)
    {
        completedReport = null;

        if (DaemonInputCapturePolicy.IsReportBoundary(inputEvent))
        {
            if (_pending.Count is 0)
            {
                return false;
            }

            _pending.Add(inputEvent);
            completedReport = _pending;
            return true;
        }

        if (DaemonInputCapturePolicy.ShouldForwardEvent(inputEvent, _captureMouse, _captureKeyboard))
        {
            _pending.Add(inputEvent);
        }

        return false;
    }

    /// <summary>
    /// Releases the completed report for reuse. The caller must invoke this after
    /// forwarding the list returned by <see cref="TryAppend"/>.
    /// </summary>
    internal void ResetCompletedReport() => _pending.Clear();
}
