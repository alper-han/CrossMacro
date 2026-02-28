using System.Threading.Tasks;
using CrossMacro.Core.Services;

namespace CrossMacro.Cli.Services;

internal sealed class NullMousePositionProvider : IMousePositionProvider
{
    public NullMousePositionProvider(string providerName)
    {
        ProviderName = providerName;
    }

    public string ProviderName { get; }

    public bool IsSupported => true;

    public Task<(int X, int Y)?> GetAbsolutePositionAsync()
    {
        return Task.FromResult<(int X, int Y)?>(null);
    }

    public Task<(int Width, int Height)?> GetScreenResolutionAsync()
    {
        return Task.FromResult<(int Width, int Height)?>(null);
    }

    public void Dispose()
    {
    }
}
