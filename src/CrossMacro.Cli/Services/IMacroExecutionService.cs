
namespace CrossMacro.Cli.Services;

public interface IMacroExecutionService
{
    public Task<MacroExecutionResult> ValidateAsync(string macroFilePath, CancellationToken cancellationToken);

    public Task<MacroExecutionResult> GetInfoAsync(string macroFilePath, CancellationToken cancellationToken);

    public Task<MacroExecutionResult> ExecuteAsync(MacroExecutionRequest request, CancellationToken cancellationToken);
}
