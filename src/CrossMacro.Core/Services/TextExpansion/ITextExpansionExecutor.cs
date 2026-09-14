
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
    public Task ExpandAsync(global::CrossMacro.Core.Models.Automation.TextExpansion.TextExpansionEntry expansion, CancellationToken cancellationToken = default);
}
