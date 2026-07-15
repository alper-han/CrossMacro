using System.Collections.Generic;
using System.Threading.Tasks;
using CrossMacro.Core.Models;

namespace CrossMacro.Application.Automation;

public interface ITextExpansionStore
{
    Task<List<TextExpansion>> LoadAsync();
    Task ReloadAsync(string profileConfigDirectory) => LoadAsync();
    Task SaveAsync(IEnumerable<TextExpansion> expansions);
}
