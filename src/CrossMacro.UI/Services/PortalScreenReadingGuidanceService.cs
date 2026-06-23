using System;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Core.Logging;
using CrossMacro.Core.Services;
using CrossMacro.Platform.Abstractions.Diagnostics;

namespace CrossMacro.UI.Services;

internal sealed class PortalScreenReadingGuidanceService : IPortalScreenReadingGuidanceService
{
    private readonly IDialogService _dialogService;
    private readonly ISettingsService _settingsService;
    private readonly IScreenReadingDiagnosticProvider? _diagnosticProvider;
    private readonly Lock _lock = new();
    private bool _hasShown;

    public PortalScreenReadingGuidanceService(
        IDialogService dialogService,
        ISettingsService settingsService,
        IScreenReadingDiagnosticProvider? diagnosticProvider = null)
    {
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _diagnosticProvider = diagnosticProvider;
    }

    public async Task ShowBeforePortalWarmupAsync()
    {
        if (!ShouldShowGuidance())
        {
            return;
        }

        await _dialogService.ShowMessageAsync(
            UIStrings.PortalScreenReadingGuidanceTitle,
            UIStrings.PortalScreenReadingGuidanceMessage,
            UIStrings.ContinueButton);
    }

    private bool ShouldShowGuidance()
    {
        if (_diagnosticProvider == null)
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
        catch (Exception ex)
        {
            Log.Warning(ex, "[PortalScreenReadingGuidanceService] Screen-reading diagnostics failed; skipping Portal guidance");
            return null;
        }
    }
}
