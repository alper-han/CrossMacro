namespace CrossMacro.Application.Profiles;

/// <summary>
/// Identifies a profile use case independently from a presentation transport.
/// </summary>
public enum ProfileOperationKind
{
    List,
    Current,
    Create,
    Switch,
    Rename,
    Delete,
}
