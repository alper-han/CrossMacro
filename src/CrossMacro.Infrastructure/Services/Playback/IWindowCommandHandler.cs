
namespace CrossMacro.Infrastructure.Services.Playback;

internal interface IWindowCommandHandler
{
    public string SubCommand { get; }
    public string? Validate(string[] parts);

    /// <summary>
    /// Index of the token naming the output variable, or -1 when the sub-command stores nothing. That token is
    /// a variable name, not a reference, so it is never resolved to the variable's value.
    /// </summary>
    public int OutputVariableIndex(string[] parts) => -1;
    public Task ExecuteAsync(string[] parts, IDictionary<string, string> variables, int stepNumber, IWindowManager manager, CancellationToken cancellationToken);
}
