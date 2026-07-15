
namespace CrossMacro.Infrastructure.Services;

public interface ITextExpansionStorageService : ITextExpansionStore
{
    List<Core.Models.TextExpansion> Load();
    List<Core.Models.TextExpansion> GetCurrent();
    string FilePath { get; }
}
