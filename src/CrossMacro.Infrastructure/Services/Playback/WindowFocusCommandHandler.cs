
namespace CrossMacro.Infrastructure.Services.Playback;

internal sealed class WindowFocusCommandHandler : IWindowCommandHandler
{
    public string SubCommand => "focus";
    public string? Validate(string[] parts) =>
        WindowControlSelector.Parse(parts, SubCommand, allowClass: true, out _);

    public async Task ExecuteAsync(string[] parts, IDictionary<string, string> variables, int stepNumber, IWindowManager manager, CancellationToken cancellationToken)
    {
        var error = WindowControlSelector.Parse(parts, SubCommand, allowClass: true, out var selector);
        if (error is not null) { throw new InvalidOperationException(error); }
        if (selector.Kind is WindowTargetKind.Active)
        {
            var info = await manager.GetActiveWindowAsync(cancellationToken).ConfigureAwait(false);
            if (info != null)
            {
                _ = await manager.FocusWindowByAddressAsync(info.Address, cancellationToken).ConfigureAwait(false);
            }

            return;
        }
        var term = selector.Value;
        _ = selector.Kind switch
        {
            WindowTargetKind.Title => await manager.FocusWindowByTitleAsync(term, cancellationToken).ConfigureAwait(false),
            WindowTargetKind.Class => await manager.FocusWindowByClassAsync(term, cancellationToken).ConfigureAwait(false),
            WindowTargetKind.Address => await manager.FocusWindowByAddressAsync(term, cancellationToken).ConfigureAwait(false),
            WindowTargetKind.Unknown or WindowTargetKind.Active => false,
            _ => throw new InvalidOperationException("Unknown window selector."),
        };
    }

}
