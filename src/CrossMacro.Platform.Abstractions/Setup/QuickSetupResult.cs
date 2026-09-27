namespace CrossMacro.Platform.Abstractions.Setup;

public readonly record struct QuickSetupResult(QuickSetupOutcome Outcome, string Message)
{
    public bool Success => Outcome is QuickSetupOutcome.Succeeded;
}
