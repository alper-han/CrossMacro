
namespace CrossMacro.Platform.Linux.Services.Factories.Selectors;

public class KdePositionProviderSelector(KdePositionProvider provider) : IPositionProviderSelector
{
    private readonly KdePositionProvider _provider = provider;

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
