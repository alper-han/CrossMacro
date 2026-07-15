namespace CrossMacro.Core.Services;

/// <summary>
/// Warning produced while restoring script steps into editor actions.
/// </summary>
public sealed record EditorActionRestoreWarning(int StepIndex, string Step, string Message);
