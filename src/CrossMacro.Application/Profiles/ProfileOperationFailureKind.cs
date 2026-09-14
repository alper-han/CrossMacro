namespace CrossMacro.Application.Profiles;

/// <summary>
/// Describes an expected, user-facing failure from a profile operation.
/// </summary>
public enum ProfileOperationFailureKind
{
    None,
    ProfileNotFound,
    ForceRequired,
    CreateFailed,
    SwitchFailed,
    RenameFailed,
    DeleteFailed,
}
