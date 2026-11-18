namespace CrossMacro.Platform.Abstractions.Diagnostics;

/// <summary>
/// The doctor checks only a platform backend can make. The doctor composes the platform-neutral report and
/// appends these checks; a backend without any registers nothing.
/// </summary>
public interface IPlatformDoctorChecks
{
    /// <summary>
    /// Brings up what the capability probes depend on before they run. Returns a warning check when that fails
    /// and the report should continue with possibly incomplete capability results; otherwise <see langword="null"/>.
    /// </summary>
    public Task<DoctorCheck?> PrepareAsync(bool verbose, CancellationToken cancellationToken);

    /// <summary>The platform's checks, evaluated against the results of the platform-neutral capability probes.</summary>
    public Task<IReadOnlyList<DoctorCheck>> CheckAsync(
        IReadOnlyList<DoctorCheck> capabilityChecks,
        bool verbose,
        CancellationToken cancellationToken);
}
