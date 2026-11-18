namespace CrossMacro.UI.ViewModels.Automation;

/// <summary>
/// Lifecycle, one-time initialization and global status reporting shared by the automation tabs.
/// Localized messages use the resource key convention <c>{resourcePrefix}_{suffix}</c>.
/// </summary>
public abstract class AutomationTabViewModelBase(
    ILocalizationService localizationService,
    string resourcePrefix,
    IUiDispatcher? uiDispatcher) : ViewModelBase(uiDispatcher), IDisposable
{
    private readonly Lock _initializationGate = new();
    private bool _initializationStarted;

    public ILocalizationService LocalizationService { get; } =
        localizationService ?? throw new ArgumentNullException(nameof(localizationService));

    public Task InitializationTask { get; private set; } = Task.CompletedTask;

    /// <summary>Raised with a localized message for the global status bar.</summary>
    public event EventHandler<string>? StatusChanged;

    protected bool IsDisposed { get; private set; }

    public Task InitializeAsync()
    {
        lock (_initializationGate)
        {
            if (!_initializationStarted)
            {
                _initializationStarted = true;
                InitializationTask = InitializeSafeAsync();
            }

            return InitializationTask;
        }
    }

    protected abstract Task InitializeCoreAsync();

    protected string Localize(string keySuffix) => LocalizationService[$"{resourcePrefix}_{keySuffix}"];

    protected string FormatLocalized(string keySuffix, params object?[] arguments) =>
        string.Format(LocalizationService.CurrentCulture, Localize(keySuffix), arguments);

    protected void ReportStatus(string message) => PostToUiThread(() => StatusChanged?.Invoke(this, message));

    /// <summary>Logs the failure and reports it as a localized status message.</summary>
    protected void ReportFailure(Exception exception, string statusKeySuffix, string logMessage)
    {
        ArgumentNullException.ThrowIfNull(exception);
        Log.LogError(exception, "[{ViewModel}] {Message}", GetType().Name, logMessage);
        ReportStatus(FormatLocalized(statusKeySuffix, exception.Message));
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            IsDisposed = true;
        }
    }

    private async Task InitializeSafeAsync()
    {
        try
        {
            await InitializeCoreAsync().ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            ReportFailure(ex, "StatusInitFailed", "Failed to initialize");
        }
    }
}
