using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Core.Models;

namespace CrossMacro.Application.Automation;

public interface IManageTextExpansion
{
    Task<IReadOnlyList<TextExpansion>> ListAsync(string? profileIdentifier = null, CancellationToken cancellationToken = default);
    Task<TextExpansion> AddAsync(TextExpansion expansion, string? profileIdentifier = null, CancellationToken cancellationToken = default);
    Task<TextExpansion> RemoveAsync(string trigger, string? profileIdentifier = null, CancellationToken cancellationToken = default);
    Task<TextExpansion> SetEnabledAsync(string trigger, bool enabled, string? profileIdentifier = null, CancellationToken cancellationToken = default);
    Task<TextExpansion?> FindAsync(string trigger, string? profileIdentifier = null, CancellationToken cancellationToken = default);
}
