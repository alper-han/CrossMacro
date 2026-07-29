
namespace CrossMacro.UI.Services;

internal sealed class PortalScreenReadingGuidanceService(
    IDialogService dialogService,
    ISettingsService settingsService,
    IScreenReadingDiagnosticProvider? diagnosticProvider = null) : IPortalScreenReadingGuidanceService
{
    private readonly IDialogService _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
    private readonly ISettingsService _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
    private readonly IScreenReadingDiagnosticProvider? _diagnosticProvider = diagnosticProvider;
    private readonly Lock _lock = new();
    private bool _hasShown;

    public async Task ShowBeforePortalWarmupAsync()
    {
        if (!ShouldShowGuidance())
        {
            return;
        }

        await _dialogService.ShowMessageAsync(
            UIStrings.PortalScreenReadingGuidanceTitle,
            UIStrings.PortalScreenReadingGuidanceMessage,
            UIStrings.ContinueButton).ConfigureAwait(false);
    }

    private bool ShouldShowGuidance()
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

        if (!string.IsNullOrWhiteSpace(_settingsService.Current.PortalScreenCastRestoreToken))
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
}
