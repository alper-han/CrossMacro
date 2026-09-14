namespace CrossMacro.Application.Profiles;

/// <summary>
/// A transport-neutral request for one profile operation.
/// </summary>
public sealed record ProfileOperationRequest(
    ProfileOperationKind Operation,
    string? Identifier = null,
    string? DisplayName = null,
    bool Force = false);
