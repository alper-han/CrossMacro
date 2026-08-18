
namespace CrossMacro.Application.Automation;

public interface ITextExpansionStore
{
    public Task<IList<TextExpansionEntry>> LoadAsync();
    public Task ReloadAsync(string profileConfigDirectory) => LoadAsync();
    public Task SaveAsync(IEnumerable<TextExpansionEntry> expansions);
}
