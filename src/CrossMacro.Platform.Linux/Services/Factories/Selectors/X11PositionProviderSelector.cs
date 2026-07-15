using CrossMacro.Core.Services;
using CrossMacro.Platform.Linux.DisplayServer;
using CrossMacro.Platform.Linux.DisplayServer.X11;

namespace CrossMacro.Platform.Linux.Services.Factories.Selectors;

public class X11PositionProviderSelector : IPositionProviderSelector
{
    public int Priority => 10;

    public bool CanHandle(CompositorType compositor)
    {
        return compositor is CompositorType.X11;
    }

    public IMousePositionProvider Create()
    {
        return new X11PositionProvider();
    }
}
