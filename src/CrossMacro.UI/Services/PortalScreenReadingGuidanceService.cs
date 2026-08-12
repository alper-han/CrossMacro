
namespace CrossMacro.UI.Services;

internal sealed class PortalScreenReadingGuidanceService(
    IDialogService dialogService,
    IScreenReadingDiagnosticProvider? diagnosticProvider = null,
    IScreenReadingCapabilityReadiness? capabilityReadiness = null,
    IPortalScreenCastRestoreStateService? portalRestoreStateService = null) : IPortalScreenReadingGuidanceService
{
    private readonly IDialogService _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
    private readonly IScreenReadingDiagnosticProvider? _diagnosticProvider = diagnosticProvider;
    private readonly IScreenReadingCapabilityReadiness? _capabilityReadiness = capabilityReadiness;
    private readonly IPortalScreenCastRestoreStateService? _portalRestoreStateService = portalRestoreStateService;
    private readonly Lock _lock = new();
    private bool _hasShown;

    public async Task ShowBeforePortalWarmupAsync()
    {
        if (_capabilityReadiness is not null)
        {
            await _capabilityReadiness.EnsureReadyAsync().ConfigureAwait(false);
        }

        if (!await ShouldShowGuidanceAsync().ConfigureAwait(false))
        {
            return;
        }

        await _dialogService.ShowMessageAsync(
            UIStrings.PortalScreenReadingGuidanceTitle,
            UIStrings.PortalScreenReadingGuidanceMessage,
            UIStrings.ContinueButton).ConfigureAwait(false);
    }

    private async Task<bool> ShouldShowGuidanceAsync()
    {
        if (_diagnosticProvider is null)
        {
            return false;
        }

        var selectedBackend = GetSelectedBackend();
        if (!string.Equals(selectedBackend, "Portal", StringComparison.Ordinal))
        {
            return false;
        }

        if (await HasPortalRestoreStateAsync().ConfigureAwait(false))
        {
            return false;
        }

        lock (_lock)
        {
            if (_hasShown)
            {
                return false;
            }

            _hasShown = true;
            return true;
        }
    }

    private string? GetSelectedBackend()
    {
        try
        {
            return _diagnosticProvider?.GetSnapshot().SelectedBackend;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.Warning(ex, "[PortalScreenReadingGuidanceService] Screen-reading diagnostics failed; skipping Portal guidance");
            return null;
        }
    }

    private async Task<bool> HasPortalRestoreStateAsync()
    {
        if (_portalRestoreStateService is null)
        {
            return false;
        }

        try
        {
            return await _portalRestoreStateService.HasRestoreStateAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            Log.Warning(ex, "[PortalScreenReadingGuidanceService] Could not read Portal restore state");
            return false;
        }
    }
}
