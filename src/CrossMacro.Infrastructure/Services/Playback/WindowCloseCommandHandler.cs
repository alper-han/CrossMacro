
namespace CrossMacro.Infrastructure.Services.Playback;

internal sealed class WindowCloseCommandHandler : IWindowCommandHandler
{
    public string SubCommand => "close";
    public string? Validate(string[] parts) =>
        WindowControlSelector.Parse(parts, SubCommand, allowClass: false, out _);

    public async Task ExecuteAsync(string[] parts, IDictionary<string, string> variables, int stepNumber, IWindowQueryService query, IWindowMutationService mutator, IWorkspaceManagementService workspace, CancellationToken cancellationToken)
    {
        var error = WindowControlSelector.Parse(parts, SubCommand, allowClass: false, out var selector);
        if (error is not null) { throw new InvalidOperationException(error); }
        if (selector.Kind is WindowTargetKind.Active)
        {
            var info = await query.GetActiveWindowAsync(cancellationToken).ConfigureAwait(false);
            if (info != null)
            {
                _ = await mutator.CloseWindowByAddressAsync(info.Address, cancellationToken).ConfigureAwait(false);
            }

            return;
        }
        var term = selector.Value;
        _ = selector.Kind switch
        {
            WindowTargetKind.Title => await mutator.CloseWindowByTitleAsync(term, cancellationToken).ConfigureAwait(false),
            WindowTargetKind.Address => await mutator.CloseWindowByAddressAsync(term, cancellationToken).ConfigureAwait(false),
            WindowTargetKind.Unknown or WindowTargetKind.Active or WindowTargetKind.Class => false,
            _ => throw new InvalidOperationException("Unknown window selector."),
        };
    }

}
