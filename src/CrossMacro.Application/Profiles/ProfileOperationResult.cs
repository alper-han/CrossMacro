namespace CrossMacro.Application.Profiles;

/// <summary>
/// The semantic outcome shared by CLI and MCP profile adapters.
/// </summary>
public sealed record ProfileOperationResult(
    ProfileResult? Snapshot,
    ProfileInfo? AffectedProfile,
    string Message,
    ProfileOperationFailureKind Failure = ProfileOperationFailureKind.None,
    string? ErrorDetail = null)
{
    public bool Success => Failure is ProfileOperationFailureKind.None;
}
