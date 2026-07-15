
namespace CrossMacro.Infrastructure.Services.Playback;

internal interface IWindowCommandHandler
{
    string SubCommand { get; }
    string? Validate(string[] parts);
    Task ExecuteAsync(string[] parts, IDictionary<string, string> variables, int stepNumber, IWindowQueryService queryService, IWindowMutationService mutationService, IWorkspaceManagementService workspaceService, CancellationToken cancellationToken);
}
