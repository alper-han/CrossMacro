
namespace CrossMacro.Application.Automation;

public interface ITextExpansionStore
{
    Task<List<TextExpansion>> LoadAsync();
    Task ReloadAsync(string profileConfigDirectory) => LoadAsync();
    Task SaveAsync(IEnumerable<TextExpansion> expansions);
}
