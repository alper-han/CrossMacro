
namespace CrossMacro.Infrastructure.Services;

public interface ITextExpansionStorageService : ITextExpansionStore
{
    public IList<Core.Models.TextExpansionEntry> Load();
    public IList<Core.Models.TextExpansionEntry> GetCurrent();
    public string FilePath { get; }
}
