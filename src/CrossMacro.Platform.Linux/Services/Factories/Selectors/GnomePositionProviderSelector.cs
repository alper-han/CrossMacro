
namespace CrossMacro.Platform.Linux.Services.Factories.Selectors;

public class GnomePositionProviderSelector(GnomePositionProvider provider) : IPositionProviderSelector
{
    private readonly GnomePositionProvider _provider = provider;

    public int Priority => 10;

    public bool CanHandle(CompositorType compositor)
    {
        return compositor is CompositorType.GNOME;
    }

    public IMousePositionProvider Create()
    {
        return _provider;
    }
}
