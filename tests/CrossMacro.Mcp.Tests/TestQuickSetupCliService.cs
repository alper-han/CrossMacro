namespace CrossMacro.Mcp.Tests;

internal sealed class TestQuickSetupCliService : IQuickSetupCliService
    {
        public QuickSetupStatus Status { get; init; } = new(Applicable: true, Provider: "flatpak", ShouldPrompt: false);
        public QuickSetupResult Result { get; init; } = new(Success: true, Message: "Quick setup completed.");

        public QuickSetupStatus GetStatus() => Status;
        public Task<QuickSetupCliResult> RunAsync(CancellationToken cancellationToken) => Task.FromResult(new QuickSetupCliResult(Status.Applicable, Status.Provider, Result));
    }
