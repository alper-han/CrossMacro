namespace CrossMacro.Platform.Windows.Services.Input;

/// <summary>
/// The native message-loop boundary of the low-level hook thread. Hook callbacks and
/// session-window state belong to <see cref="WindowsCaptureSession"/>.
/// </summary>
internal static class WindowsMessagePump
{
    internal static void Run(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            int result = User32.GetMessage(out var message, IntPtr.Zero, 0, 0);
            if (ShouldDispatchMessage(result))
            {
                _ = User32.TranslateMessage(ref message);
                _ = User32.DispatchMessage(ref message);
            }
            else
            {
                break;
            }
        }
    }

    internal static bool ShouldDispatchMessage(int getMessageResult) => getMessageResult > 0;

    internal static void RequestStop(uint threadId)
    {
        if (threadId != 0)
        {
            _ = User32.PostThreadMessage(threadId, User32.WM_QUIT, IntPtr.Zero, IntPtr.Zero);
        }
    }
}
