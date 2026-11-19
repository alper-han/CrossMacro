
namespace CrossMacro.UI.ViewModels.Automation;

/// <summary>
/// ViewModel for the Text Expansion tab - handles creating and managing text expansions
/// </summary>
public partial class TextExpansionViewModel : AutomationTabViewModelBase
{
    private readonly IDialogService _dialogService;
    private readonly IEnvironmentInfoProvider _environmentInfoProvider;
    private readonly IManageTextExpansion _manageTextExpansion;
    private readonly Dictionary<TextExpansionEntry, bool> _managedEnabledState = new();

    public TextExpansionViewModel(
        IManageTextExpansion manageTextExpansion,
        IDialogService dialogService,
        IEnvironmentInfoProvider environmentInfoProvider,
        ILocalizationService localizationService,
        IUiDispatcher? uiDispatcher = null)
        : base(localizationService, "TextExpansion", uiDispatcher)
    {
        _manageTextExpansion = manageTextExpansion ?? throw new ArgumentNullException(nameof(manageTextExpansion));
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
        _environmentInfoProvider = environmentInfoProvider ?? throw new ArgumentNullException(nameof(environmentInfoProvider));
        LocalizationService.CultureChanged += OnCultureChanged;
    }

    public bool IsPasteMethodVisible => MainWindowPresentationPolicy.IsLinux(_environmentInfoProvider.CurrentEnvironment);

    public bool IsPasteMethodSelectorVisible =>
        IsPasteMethodVisible && SelectedInsertionMode is TextInsertionMode.Paste;

    public bool IsDirectTypingMethodSelectorVisible =>
        SelectedInsertionMode is TextInsertionMode.DirectTyping;

    protected override Task InitializeCoreAsync() => LoadExpansionsAsync();

    private async Task LoadExpansionsAsync()
    {
        var loadedExpansions = await _manageTextExpansion.ListAsync(cancellationToken: default).ConfigureAwait(false);

        await RunOnUiThreadAsync(() =>
        {
            Expansions.Clear();
            _managedEnabledState.Clear();
            foreach (var expansion in loadedExpansions)
            {
                Expansions.Add(expansion);
                _managedEnabledState[expansion] = expansion.IsEnabled;
            }

            OnPropertyChanged(nameof(HasExpansions));
            OnPropertyChanged(nameof(ExpansionCountText));
        }).ConfigureAwait(false);
    }

    public async Task RefreshProfileDataAsync()
    {
        try
        {
            await LoadExpansionsAsync().ConfigureAwait(false);
            await RunOnUiThreadAsync(ResetInputs).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            ReportFailure(ex, "StatusInitFailed", "Failed to load text expansions for the active profile");
        }
    }

    private IReadOnlyList<TextInsertionMode> _insertionModes = Enum.GetValues<TextInsertionMode>();
    private IReadOnlyList<PasteMethod> _pasteMethods = Enum.GetValues<PasteMethod>();
    private IReadOnlyList<DirectTypingMethod> _directTypingMethods = Enum.GetValues<DirectTypingMethod>();

    public TextInsertionMode SelectedInsertionMode
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                OnPropertyChanged(nameof(IsPasteMethodSelectorVisible));
                OnPropertyChanged(nameof(IsDirectTypingMethodSelectorVisible));
            }
        }
    } = TextInsertionMode.Paste;

    public PasteMethod SelectedPasteMethod
    {
        get;
        set => SetProperty(ref field, value);
    } = PasteMethod.CtrlV;

    public DirectTypingMethod SelectedDirectTypingMethod
    {
        get;
        set => SetProperty(ref field, value);
    } = DirectTypingMethod.FastBatch;

    public IEnumerable<TextInsertionMode> InsertionModes => _insertionModes;

    // Expose enum values for UI
    public IEnumerable<PasteMethod> PasteMethods => _pasteMethods;

    public IEnumerable<DirectTypingMethod> DirectTypingMethods => _directTypingMethods;

    private void OnCultureChanged(object? sender, EventArgs e)
    {
        PostToUiThread(() =>
        {
            _insertionModes = Enum.GetValues<TextInsertionMode>();
            _pasteMethods = Enum.GetValues<PasteMethod>();
            _directTypingMethods = Enum.GetValues<DirectTypingMethod>();
            OnPropertyChanged(nameof(ExpansionCountText));
            OnPropertyChanged(nameof(InsertionModes));
            OnPropertyChanged(nameof(PasteMethods));
            OnPropertyChanged(nameof(DirectTypingMethods));
        });
    }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddExpansionCommand))]
    private string _triggerInput = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddExpansionCommand))]
    private string _replacementInput = string.Empty;

    public ObservableCollection<TextExpansionEntry> Expansions { get; } = new();

    public bool HasExpansions => Expansions.Count > 0;

    public string ExpansionCountText => FormatLocalized("Items", Expansions.Count);

    private bool CanAddExpansion()
    {
        return !string.IsNullOrWhiteSpace(TriggerInput) &&
               !string.IsNullOrWhiteSpace(ReplacementInput);
    }

    [RelayCommand(CanExecute = nameof(CanAddExpansion))]
    private async Task AddExpansionAsync()
    {
        var newExpansion = new TextExpansionEntry(
            TriggerInput,
            ReplacementInput,
            isEnabled: true,
            SelectedPasteMethod,
            SelectedInsertionMode,
            SelectedDirectTypingMethod);

        try
        {
            var addedExpansion = await _manageTextExpansion.AddAsync(newExpansion, profileIdentifier: null, default).ConfigureAwait(false);
            await RunOnUiThreadAsync(() => AddExpansionToUi(addedExpansion)).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            ReportFailure(ex, "StatusAddFailed", "Failed to add text expansion");
        }
    }

    [RelayCommand]
    private async Task RemoveExpansionAsync(TextExpansionEntry? expansion)
    {
        if (expansion is null)
        {
            return;
        }

        var confirmed = await _dialogService.ShowConfirmationAsync(
            Localize("DeleteTitle"),
            FormatLocalized("DeleteMessage", expansion.Trigger)).ConfigureAwait(false);

        if (!confirmed || !Expansions.Contains(expansion))
        {
            return;
        }

        try
        {
            _ = await _manageTextExpansion.RemoveAsync(expansion.Trigger, cancellationToken: default).ConfigureAwait(false);
            await RunOnUiThreadAsync(() => RemoveExpansionFromUi(expansion)).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            ReportFailure(ex, "StatusRemoveFailed", "Failed to remove text expansion");
        }
    }

    [RelayCommand]
    private async Task ToggleExpansionAsync(TextExpansionEntry? expansion)
    {
        if (expansion is null)
        {
            return;
        }

        var requestedEnabled = expansion.IsEnabled;
        var previousEnabled = _managedEnabledState.TryGetValue(expansion, out var knownEnabled)
            ? knownEnabled
            : requestedEnabled;
        try
        {
            var updatedExpansion = await _manageTextExpansion.SetEnabledAsync(expansion.Trigger, requestedEnabled, cancellationToken: default).ConfigureAwait(false);
            await RunOnUiThreadAsync(() =>
            {
                expansion.IsEnabled = updatedExpansion.IsEnabled;
                _managedEnabledState[expansion] = updatedExpansion.IsEnabled;
            }).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            await RunOnUiThreadAsync(() => expansion.IsEnabled = previousEnabled).ConfigureAwait(false);
            ReportFailure(ex, "StatusToggleFailed", "Failed to change text expansion state");
        }
    }

    private void AddExpansionToUi(TextExpansionEntry expansion)
    {
        Expansions.Insert(0, expansion);
        _managedEnabledState[expansion] = expansion.IsEnabled;

        OnPropertyChanged(nameof(HasExpansions));
        OnPropertyChanged(nameof(ExpansionCountText));
        ResetInputs();
    }

    private void RemoveExpansionFromUi(TextExpansionEntry expansion)
    {
        _ = Expansions.Remove(expansion);
        _ = _managedEnabledState.Remove(expansion);
        OnPropertyChanged(nameof(HasExpansions));
        OnPropertyChanged(nameof(ExpansionCountText));
    }

    private void ResetInputs()
    {
        TriggerInput = string.Empty;
        ReplacementInput = string.Empty;
        SelectedInsertionMode = TextInsertionMode.Paste;
        SelectedPasteMethod = PasteMethod.CtrlV;
        SelectedDirectTypingMethod = DirectTypingMethod.FastBatch;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !IsDisposed)
        {
            LocalizationService.CultureChanged -= OnCultureChanged;
        }

        base.Dispose(disposing);
    }
}
