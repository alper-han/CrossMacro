using CrossMacro.Platform.Abstractions.Setup;
using CrossMacro.UI.Views.Dialogs;

namespace CrossMacro.UI.Services.Runtime;

internal sealed class DesktopQuickSetupGateService(
    BootstrapDialogHost dialogHost,
    ILocalizationService localizationService,
    Func<IFlatpakQuickSetupService?> getFlatpakQuickSetupService,
    Func<IAppImageQuickSetupService?> getAppImageQuickSetupService,
    Func<IDisplaySessionService?>? getDisplaySessionService = null,
    Func<ILinuxDirectInputQuickSetupService?>? getLinuxDirectInputQuickSetupService = null)
{
    private const string ParagraphSeparator = "\n\n";

    private readonly BootstrapDialogHost _dialogHost = dialogHost ?? throw new ArgumentNullException(nameof(dialogHost));
    private readonly ILocalizationService _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
    private readonly Func<IFlatpakQuickSetupService?> _getFlatpakQuickSetupService = getFlatpakQuickSetupService ?? throw new ArgumentNullException(nameof(getFlatpakQuickSetupService));
    private readonly Func<IAppImageQuickSetupService?> _getAppImageQuickSetupService = getAppImageQuickSetupService ?? throw new ArgumentNullException(nameof(getAppImageQuickSetupService));
    private readonly Func<IDisplaySessionService?> _getDisplaySessionService = getDisplaySessionService ?? (static () => null);
    private readonly Func<ILinuxDirectInputQuickSetupService?> _getLinuxDirectInputQuickSetupService = getLinuxDirectInputQuickSetupService ?? (static () => null);

    public async Task<bool> TryHandleAsync(
        IClassicDesktopStyleApplicationLifetime desktop,
        DesktopStartupPreferences startupPreferences,
        string? unsupportedSessionReason,
        Func<IClassicDesktopStyleApplicationLifetime, DesktopStartupPreferences, bool, Task> startDesktopRuntimeAsync,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(desktop);
        ArgumentNullException.ThrowIfNull(startDesktopRuntimeAsync);
        cancellationToken.ThrowIfCancellationRequested();

        if (!string.IsNullOrWhiteSpace(unsupportedSessionReason))
        {
            var flatpak = _getFlatpakQuickSetupService();
            if (flatpak is not null && flatpak.IsApplicable())
            {
                await HandleQuickSetupAsync(desktop, startupPreferences,
                    Localize("QuickSetup_FlatpakTitle"),
                    Paragraphs(
                        Localize("QuickSetup_FlatpakIntro"),
                        Localize("QuickSetup_RunPrompt"),
                        Localize("QuickSetup_FlatpakDetail"),
                        string.Format(_localizationService.CurrentCulture, Localize("QuickSetup_ReasonDetails"), unsupportedSessionReason)),
                    flatpak.RunAsync, allowWithoutAutomation: false, startDesktopRuntimeAsync, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                _dialogHost.ShowTerminalDialog(
                    desktop,
                    Localize("QuickSetup_UnsupportedSessionTitle"),
                    unsupportedSessionReason,
                    Localize("QuickSetup_Exit"),
                    cancellationToken);
            }
            return true;
        }

        var appImage = _getAppImageQuickSetupService();
        if (appImage?.ShouldPrompt() is true)
        {
            await HandleQuickSetupAsync(desktop, startupPreferences,
                Localize("QuickSetup_LinuxInputTitle"),
                Paragraphs(
                    Localize("QuickSetup_AppImageIntro"),
                    Localize("QuickSetup_RunPrompt"),
                    Localize("QuickSetup_AppImageDetail"),
                    Localize("QuickSetup_AppImageTemporary")),
                appImage.RunAsync, allowWithoutAutomation: true, startDesktopRuntimeAsync, cancellationToken).ConfigureAwait(false);
            return true;
        }

        var directInput = _getLinuxDirectInputQuickSetupService();
        if (directInput is not null && await directInput.ShouldPromptAsync(cancellationToken).ConfigureAwait(false))
        {
            await HandleQuickSetupAsync(desktop, startupPreferences,
                Localize("QuickSetup_LinuxInputTitle"),
                Paragraphs(
                    Localize("QuickSetup_DirectInputIntro"),
                    Localize("QuickSetup_RunPrompt"),
                    Localize("QuickSetup_DirectInputDetail")),
                directInput.RunAsync, allowWithoutAutomation: true, startDesktopRuntimeAsync, cancellationToken).ConfigureAwait(false);
            return true;
        }

        cancellationToken.ThrowIfCancellationRequested();
        return false;
    }

    private async Task HandleQuickSetupAsync(
        IClassicDesktopStyleApplicationLifetime desktop,
        DesktopStartupPreferences startupPreferences,
        string title,
        string message,
        Func<CancellationToken, Task<QuickSetupResult>> runSetupAsync,
        bool allowWithoutAutomation,
        Func<IClassicDesktopStyleApplicationLifetime, DesktopStartupPreferences, bool, Task> startDesktopRuntimeAsync,
        CancellationToken cancellationToken)
    {
        await _dialogHost.RunWithOwnerAsync(desktop, async owner =>
        {
            var actionText = Localize("QuickSetup_RunAction");
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                // Nullable results distinguish closing the window from an explicit Continue button.
                var choice = await _dialogHost.ShowDialogAsync<bool?>(owner,
                    () =>
                    {
                        var dialog = BootstrapDialogHost.CreateCenteredConfirmationDialog(
                            title,
                            allowWithoutAutomation
                                ? Paragraphs(message, Localize("QuickSetup_ContinueWithoutAutomationNote"))
                                : message,
                            actionText,
                            allowWithoutAutomation ? Localize("QuickSetup_ContinueWithoutAutomation") : Localize("QuickSetup_Exit"),
                            dangerYes: false,
                            dangerNo: !allowWithoutAutomation);
                        dialog.Width = 450;
                        return dialog;
                    }).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();

                if (choice is not true)
                {
                    if (choice is false && allowWithoutAutomation)
                    {
                        await startDesktopRuntimeAsync(desktop, startupPreferences, false).ConfigureAwait(false);
                        cancellationToken.ThrowIfCancellationRequested();
                    }
                    else
                    {
                        await _dialogHost.ShutdownAsync(desktop).ConfigureAwait(false);
                    }
                    return;
                }

                QuickSetupResult result;
                try
                {
                    result = await runSetupAsync(cancellationToken).ConfigureAwait(false);
                    cancellationToken.ThrowIfCancellationRequested();
                    if (result.Success && !allowWithoutAutomation && _getDisplaySessionService() is { } displaySession)
                    {
                        var support = await displaySession.IsSessionSupportedAsync(cancellationToken).ConfigureAwait(false);
                        cancellationToken.ThrowIfCancellationRequested();
                        if (!support.Supported)
                        {
                            result = new QuickSetupResult(QuickSetupOutcome.DeviceAccessUnavailable, support.Reason);
                        }
                    }
                }
                catch (Exception ex) when (ex is not OutOfMemoryException and not OperationCanceledException)
                {
                    Log.LogError(ex, "[DesktopQuickSetupGateService] Quick setup failed");
                    result = new QuickSetupResult(QuickSetupOutcome.Failed, Localize("QuickSetup_UnexpectedError"));
                }

                if (result.Success)
                {
                    await startDesktopRuntimeAsync(desktop, startupPreferences, true).ConfigureAwait(false);
                    cancellationToken.ThrowIfCancellationRequested();
                    return;
                }

                title = Localize(result.Outcome is QuickSetupOutcome.Cancelled ? "QuickSetup_CancelledTitle" : "QuickSetup_FailedTitle");
                message = result.Message;
                actionText = Localize("QuickSetup_RetryAction");
            }
        }, cancellationToken).ConfigureAwait(false);
    }

    private string Localize(string key) => _localizationService[key];

    private static string Paragraphs(params ReadOnlySpan<string> paragraphs) => string.Join(ParagraphSeparator, paragraphs);
}
