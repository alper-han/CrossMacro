namespace CrossMacro.Infrastructure.Services.Scripting;

/// <summary>
/// Represents a single script step with optional source metadata.
/// </summary>
public sealed record RunScriptStep(string Step, int? SourceLineNumber = null, int SourceIndex = 0);
