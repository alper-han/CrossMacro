namespace CrossMacro.Infrastructure.Services.Scripting;

/// <summary>Separate budgets for in-memory expansion and service-backed execution.</summary>
internal static class ScriptExecutionLimits
{
    public const int StaticExpansionIterations = 10_000_000;
    public const int RuntimeExecutionIterations = 100_000;
}
