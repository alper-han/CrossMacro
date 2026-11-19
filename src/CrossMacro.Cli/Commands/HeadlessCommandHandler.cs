using CrossMacro.Cli.Headless;
using CrossMacro.Commands.Runtime;

namespace CrossMacro.Cli.Commands;

public sealed class HeadlessCommandHandler(IHeadlessRuntimeService headlessRuntimeService, IPreflightService preflightService) : CliCommandHandlerBase<HeadlessOptions>
{
    private readonly IHeadlessRuntimeService _headlessRuntimeService = headlessRuntimeService;
    private readonly IPreflightService _preflightService = preflightService;

    protected override async Task<OperationResult> ExecuteAsync(HeadlessOptions options, CancellationToken cancellationToken)
    {
        var preflight = await _preflightService.CheckAsync(PreflightTarget.Headless, cancellationToken).ConfigureAwait(false);
        if (!preflight.Success)
        {
            return preflight;
        }

        return await _headlessRuntimeService.RunAsync(cancellationToken).ConfigureAwait(false);
    }
}
