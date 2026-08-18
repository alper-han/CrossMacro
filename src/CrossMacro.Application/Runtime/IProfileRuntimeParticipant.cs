namespace CrossMacro.Application.Runtime;

/// <summary>
/// Participates in profile activation for state owned outside the runtime adapters.
/// </summary>
public interface IProfileRuntimeParticipant
{
    /// <summary>Persists the state associated with the currently active profile.</summary>
    public Task FlushAsync(CancellationToken cancellationToken = default);

    /// <summary>Replaces in-memory state with the snapshot for the activated profile.</summary>
    public Task ReloadAsync(
        string profileConfigDirectory,
        CancellationToken cancellationToken = default);
}
