
namespace CrossMacro.Cli.Commands;

public sealed class ProfileCommandHandler(IProfileCommandService profileCommandService) : CliCommandHandlerBase<ProfileOptions>
{
    private readonly IProfileCommandService _profileCommandService = profileCommandService;

    protected override Task<OperationResult> ExecuteAsync(ProfileOptions options, CancellationToken cancellationToken)
    {
        return options.Action switch
        {
            ProfileAction.List => _profileCommandService.ListAsync(cancellationToken),
            ProfileAction.Current => _profileCommandService.CurrentAsync(cancellationToken),
            ProfileAction.Create => _profileCommandService.CreateAsync(options.ProfileIdentifier ?? string.Empty, cancellationToken),
            ProfileAction.Switch => _profileCommandService.SwitchAsync(options.ProfileIdentifier ?? string.Empty, cancellationToken),
            ProfileAction.Rename => _profileCommandService.RenameAsync(options.ProfileIdentifier ?? string.Empty, options.NewName ?? string.Empty, cancellationToken),
            ProfileAction.Delete => _profileCommandService.DeleteAsync(options.ProfileIdentifier ?? string.Empty, options.Force, cancellationToken),
            _ => Task.FromResult(OperationResult.Fail(ExecutionOutcomeCode.InvalidArguments, "Unknown profile action.")),
        };
    }
}
