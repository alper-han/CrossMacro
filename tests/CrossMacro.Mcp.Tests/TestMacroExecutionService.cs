namespace CrossMacro.Mcp.Tests;

internal sealed class TestMacroExecutionService : IMacroExecutionService
    {
        public MacroExecutionResult? InfoResult { get; init; }

        public MacroExecutionResult? ValidationResult { get; init; }

        public int GetInfoCallCount { get; private set; }

        public string? LastMacroPath { get; private set; }

        public MacroExecutionResult? ExecutionResult { get; init; }

        public Func<MacroExecutionRequest, CancellationToken, Task<MacroExecutionResult>>? ExecutionHandler { get; init; }

        public MacroExecutionRequest? LastExecutionRequest { get; private set; }

        public Task<MacroExecutionResult> ValidateAsync(string macroFilePath, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastMacroPath = macroFilePath;
            return Task.FromResult(ValidationResult ?? throw new InvalidOperationException("Validation result was not configured."));
        }

        public Task<MacroExecutionResult> GetInfoAsync(string macroFilePath, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            GetInfoCallCount++;
            LastMacroPath = macroFilePath;
            return Task.FromResult(InfoResult ?? throw new InvalidOperationException("Info result was not configured."));
        }

        public Task<MacroExecutionResult> ExecuteAsync(MacroExecutionRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastExecutionRequest = request;
            return ExecutionHandler is { } handler
                ? handler(request, cancellationToken)
                : Task.FromResult(ExecutionResult ?? throw new InvalidOperationException("Execution result was not configured."));
        }
    }
