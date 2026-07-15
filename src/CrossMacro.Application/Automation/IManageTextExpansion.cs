
namespace CrossMacro.Application.Automation;

public interface IManageTextExpansion
{
    public Task<IReadOnlyList<TextExpansionEntry>> ListAsync(string? profileIdentifier = null, CancellationToken cancellationToken = default);
    public Task<TextExpansionEntry> AddAsync(TextExpansionEntry expansion, string? profileIdentifier = null, CancellationToken cancellationToken = default);
    public Task<TextExpansionEntry> RemoveAsync(string trigger, string? profileIdentifier = null, CancellationToken cancellationToken = default);
    public Task<TextExpansionEntry> SetEnabledAsync(string trigger, bool enabled, string? profileIdentifier = null, CancellationToken cancellationToken = default);
    public Task<TextExpansionEntry?> FindAsync(string trigger, string? profileIdentifier = null, CancellationToken cancellationToken = default);
}
