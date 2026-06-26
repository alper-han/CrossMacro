using CrossMacro.Core.Services;
using CrossMacro.Platform.Linux.DisplayServer;
using CrossMacro.Platform.Linux.DisplayServer.Wayland;

namespace CrossMacro.Platform.Linux.Services.Factories.Selectors;

public class GnomePositionProviderSelector : IPositionProviderSelector
{
    private readonly GnomePositionProvider _provider;

    public GnomePositionProviderSelector(GnomePositionProvider provider)
    {
        _provider = provider;
    }

    public int Priority => 10;

    public bool CanHandle(CompositorType compositor)
    {
        return compositor == CompositorType.GNOME;
    }

    public IMousePositionProvider Create()
    {
        return _provider;
    }
}
