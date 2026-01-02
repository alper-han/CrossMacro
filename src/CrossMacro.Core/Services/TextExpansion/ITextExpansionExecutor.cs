using System.Threading.Tasks;
using CrossMacro.Core.Models;

namespace CrossMacro.Core.Services.TextExpansion;

/// <summary>
/// Responsible for executing the text expansion (removing trigger and inserting replacement).
/// </summary>
public interface ITextExpansionExecutor
{
    /// <summary>
    /// Performs the expansion asynchronously.
    /// </summary>
    /// <param name="expansion">The expansion to perform.</param>
    Task ExpandAsync(Models.TextExpansion expansion);
}
