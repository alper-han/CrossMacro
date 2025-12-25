using System.Collections.Generic;
using System.Threading.Tasks;
using CrossMacro.Core.Models;

namespace CrossMacro.Infrastructure.Services;

public interface ITextExpansionStorageService
{
    List<TextExpansion> Load();
    Task<List<TextExpansion>> LoadAsync();
    Task SaveAsync(IEnumerable<TextExpansion> expansions);
    List<TextExpansion> GetCurrent();
    string FilePath { get; }
}
