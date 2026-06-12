using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CrossMacro.Core.Models;

namespace CrossMacro.Infrastructure.Services;

public interface ITextExpansionStorageService
{
    List<Core.Models.TextExpansion> Load();
    Task<List<Core.Models.TextExpansion>> LoadAsync();
    Task ReloadAsync(string profileConfigDirectory) => LoadAsync();
    Task SaveAsync(IEnumerable<Core.Models.TextExpansion> expansions);
    List<Core.Models.TextExpansion> GetCurrent();
    string FilePath { get; }
}
