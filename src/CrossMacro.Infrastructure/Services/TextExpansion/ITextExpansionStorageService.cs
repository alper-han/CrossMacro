
namespace CrossMacro.Infrastructure.Services.TextExpansion;

public interface ITextExpansionStorageService : ITextExpansionStore, ICachedTextExpansionStore
{
    public IList<TextExpansionEntry> Load();
    public string FilePath { get; }
}
