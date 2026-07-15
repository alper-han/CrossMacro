
namespace CrossMacro.Infrastructure.Services.Playback;

internal interface IWindowCommandHandler
{
    public string SubCommand { get; }
    public string? Validate(string[] parts);
    public Task ExecuteAsync(string[] parts, IDictionary<string, string> variables, int stepNumber, IWindowQueryService queryService, IWindowMutationService mutationService, IWorkspaceManagementService workspaceService, CancellationToken cancellationToken);
}
