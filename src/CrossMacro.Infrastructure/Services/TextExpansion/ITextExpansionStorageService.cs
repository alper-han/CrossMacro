
namespace CrossMacro.Infrastructure.Services.TextExpansion;

public interface ITextExpansionStorageService : ITextExpansionStore, ICachedTextExpansionStore
{
    public IList<Core.Models.Automation.TextExpansion.TextExpansionEntry> Load();
    public string FilePath { get; }
}
