
namespace CrossMacro.Cli.Commands;

public sealed class ProfileCommandHandler : CliCommandHandlerBase<ProfileCliOptions>
{
    private readonly IProfileCliService _profileCliService;

    public ProfileCommandHandler(IProfileCliService profileCliService)
    {
        _profileCliService = profileCliService;
    }

    protected override Task<CliCommandExecutionResult> ExecuteAsync(ProfileCliOptions options, CancellationToken cancellationToken)
    {
        return options.Action switch
        {
            ProfileCliAction.List => _profileCliService.ListAsync(cancellationToken),
            ProfileCliAction.Current => _profileCliService.CurrentAsync(cancellationToken),
            ProfileCliAction.Create => _profileCliService.CreateAsync(options.ProfileIdentifier ?? string.Empty, cancellationToken),
            ProfileCliAction.Switch => _profileCliService.SwitchAsync(options.ProfileIdentifier ?? string.Empty, cancellationToken),
            ProfileCliAction.Rename => _profileCliService.RenameAsync(options.ProfileIdentifier ?? string.Empty, options.NewName ?? string.Empty, cancellationToken),
            ProfileCliAction.Delete => _profileCliService.DeleteAsync(options.ProfileIdentifier ?? string.Empty, options.Force, cancellationToken),
            _ => Task.FromResult(CliCommandExecutionResult.Fail(CliExitCode.InvalidArguments, "Unknown profile action.")),
        };
    }
}
