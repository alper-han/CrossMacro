namespace CrossMacro.UI.Services.Presentation;

internal sealed class PresentationSubscriptions : IDisposable
{
    private readonly List<Action> _unsubscribe = [];
    public void Add(Action unsubscribe) => _unsubscribe.Add(unsubscribe);
    public void Dispose()
    {
        foreach (var unsubscribe in _unsubscribe) { unsubscribe(); }
        _unsubscribe.Clear();
    }
}
