
namespace CrossMacro.Infrastructure.Services.TextExpansion;

public interface ITextExpansionStorageService : ITextExpansionStore
{
    public IList<TextExpansionEntry> Load();
    public Task ReloadAsync(string profileConfigDirectory);
    public string FilePath { get; }
}
