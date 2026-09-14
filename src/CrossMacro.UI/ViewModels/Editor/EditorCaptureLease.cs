namespace CrossMacro.UI.ViewModels.Editor;

internal sealed class EditorCaptureLease(CancellationTokenSource cancellation, Action<EditorCaptureLease> release) : IDisposable
{
    private readonly CancellationTokenSource _cancellation = cancellation;
    internal CancellationToken Token => _cancellation.Token;
    internal void Cancel() => _cancellation.Cancel();
    public void Dispose() { release(this); _cancellation.Dispose(); }
}
