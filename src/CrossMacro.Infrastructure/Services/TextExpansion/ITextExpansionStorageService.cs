
namespace CrossMacro.Infrastructure.Services.TextExpansion;

public interface ITextExpansionStorageService : ITextExpansionStore
{
    public Task ReloadAsync(string profileConfigDirectory);
    public string FilePath { get; }
}
