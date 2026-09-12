namespace CrossMacro.Mcp.Tests;

internal sealed class TestProfileCliService : IProfileCliService
    {
        public CliCommandExecutionResult? ListResult { get; init; }

        public int ListCallCount { get; private set; }

        public Task<CliCommandExecutionResult> ListAsync(CancellationToken cancellationToken)
        {
            ListCallCount++;
            return Task.FromResult(ListResult ?? CliCommandExecutionResult.Ok("0 profile(s)."));
        }

        public Task<CliCommandExecutionResult> CurrentAsync(CancellationToken cancellationToken) => Task.FromResult(CliCommandExecutionResult.Ok("Current profile."));
        public Task<CliCommandExecutionResult> CreateAsync(string name, CancellationToken cancellationToken) => Task.FromResult(CliCommandExecutionResult.Ok("Profile created."));
        public Task<CliCommandExecutionResult> SwitchAsync(string profileIdentifier, CancellationToken cancellationToken) => Task.FromResult(CliCommandExecutionResult.Ok("Profile switched."));
        public Task<CliCommandExecutionResult> RenameAsync(string profileIdentifier, string newName, CancellationToken cancellationToken) => Task.FromResult(CliCommandExecutionResult.Ok("Profile renamed."));
        public Task<CliCommandExecutionResult> DeleteAsync(string profileIdentifier, bool force, CancellationToken cancellationToken) => Task.FromResult(CliCommandExecutionResult.Ok("Profile deleted."));
    }
