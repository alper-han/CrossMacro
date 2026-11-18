namespace CrossMacro.Mcp.Tests;

internal sealed class TestQuickSetupCommandService : IQuickSetupCommandService
{
    public QuickSetupStatus Status { get; init; } = new(Applicable: true, Provider: "flatpak", ShouldPrompt: false);
    public QuickSetupResult Result { get; init; } = new(Outcome: QuickSetupOutcome.Succeeded, Message: "Quick setup completed.");

    public QuickSetupStatus GetStatus() => Status;
    public Task<QuickSetupCommandResult> RunAsync(CancellationToken cancellationToken) => Task.FromResult(new QuickSetupCommandResult(Status.Applicable, Status.Provider, Result));
}
