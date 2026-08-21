namespace CrossMacro.Application.Runtime;

public sealed record ApprovalRequest(
    string Operation,
    string Description,
    TimeSpan Timeout,
    string? TargetSummary = null,
    IReadOnlyList<string>? CapabilityNames = null)
{
    public IReadOnlyList<string> CapabilityNames { get; init; } = CapabilityNames ?? [];
}
