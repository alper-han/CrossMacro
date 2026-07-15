using CrossMacro.Core.Services;
using CrossMacro.Platform.Linux.DisplayServer;
using CrossMacro.Platform.Linux.DisplayServer.Wayland;

namespace CrossMacro.Platform.Linux.Services.Factories.Selectors;

public class KdePositionProviderSelector : IPositionProviderSelector
{
    private readonly KdePositionProvider _provider;

    public KdePositionProviderSelector(KdePositionProvider provider)
    {
        _provider = provider;
    }

    public int Priority => 10;

    public bool CanHandle(CompositorType compositor)
    {
        return compositor is CompositorType.KDE;
    }

    public IMousePositionProvider Create()
    {
        return _provider;
    }
}
