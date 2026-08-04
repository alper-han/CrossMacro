
namespace CrossMacro.Infrastructure.Services;

public interface ITextExpansionStorageService : ITextExpansionStore, ICachedTextExpansionStore
{
    public IList<Core.Models.TextExpansionEntry> Load();
    public string FilePath { get; }
}
