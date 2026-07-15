
namespace CrossMacro.UI.ViewModels;

/// <summary>
/// ViewModel for the Text Expansion tab - handles creating and managing text expansions
/// </summary>
public partial class TextExpansionViewModel : ViewModelBase, IDisposable
{
    private readonly ITextExpansionStore _storageService = null!;
    private readonly IDialogService _dialogService;
    private readonly IEnvironmentInfoProvider _environmentInfoProvider;
    private readonly ILocalizationService _localizationService;
    private readonly IManageTextExpansion? _manageTextExpansion;

    private string _triggerInput = string.Empty;
    private string _replacementInput = string.Empty;
    private ObservableCollection<TextExpansionEntry> _expansions = new();
    private readonly Dictionary<TextExpansionEntry, bool> _managedEnabledState = new();

    public TextExpansionViewModel(
        ITextExpansionStore storageService,
        IDialogService dialogService,
        IEnvironmentInfoProvider environmentInfoProvider,
        ILocalizationService localizationService)
    {
        _storageService = storageService;
        _dialogService = dialogService;
        _environmentInfoProvider = environmentInfoProvider;
        _localizationService = localizationService;
        _localizationService.CultureChanged += OnCultureChanged;

        // Load existing expansions asynchronously
        InitializationTask = LoadExpansionsAsync();
    }

    public TextExpansionViewModel(
        IManageTextExpansion manageTextExpansion,
        IDialogService dialogService,
        IEnvironmentInfoProvider environmentInfoProvider,
        ILocalizationService localizationService)
    {
        _manageTextExpansion = manageTextExpansion;
        _dialogService = dialogService;
        _environmentInfoProvider = environmentInfoProvider;
        _localizationService = localizationService;
        _localizationService.CultureChanged += OnCultureChanged;
        InitializationTask = LoadExpansionsAsync();
    }

    public Task InitializationTask { get; private set; } = Task.CompletedTask;

    public bool IsPasteMethodVisible => IsLinuxEnvironment(_environmentInfoProvider.CurrentEnvironment);

    public bool IsPasteMethodSelectorVisible =>
        IsPasteMethodVisible && SelectedInsertionMode is TextInsertionMode.Paste;

    public bool IsDirectTypingMethodSelectorVisible =>
        SelectedInsertionMode is TextInsertionMode.DirectTyping;

    private static bool IsLinuxEnvironment(DisplayEnvironment env)
    {
        return env is DisplayEnvironment.LinuxX11 or DisplayEnvironment.LinuxWayland or DisplayEnvironment.LinuxHyprland or DisplayEnvironment.LinuxWayfire or DisplayEnvironment.LinuxKDE or DisplayEnvironment.LinuxGnome;
    }

    private async Task LoadExpansionsAsync()
    {
        var loadedExpansions = _manageTextExpansion is not null
            ? await _manageTextExpansion.ListAsync().ConfigureAwait(false)
            : (IReadOnlyList<TextExpansionEntry>)await _storageService.LoadAsync().ConfigureAwait(false);
        Expansions.Clear();
        _managedEnabledState.Clear();
        foreach (var expansion in loadedExpansions)
        {
            Expansions.Add(expansion);
            if (_manageTextExpansion is not null)
            {
                _managedEnabledState[expansion] = expansion.IsEnabled;
            }
        }

        OnPropertyChanged(nameof(HasExpansions));
        OnPropertyChanged(nameof(ExpansionCountText));
    }

    public async Task RefreshProfileDataAsync()
    {
        TriggerInput = string.Empty;
        ReplacementInput = string.Empty;
        SelectedInsertionMode = TextInsertionMode.Paste;
        SelectedPasteMethod = PasteMethod.CtrlV;
        SelectedDirectTypingMethod = DirectTypingMethod.FastBatch;
        await LoadExpansionsAsync().ConfigureAwait(false);
    }

    private PasteMethod _selectedPasteMethod = PasteMethod.CtrlV;
    private TextInsertionMode _selectedInsertionMode = TextInsertionMode.Paste;
    private DirectTypingMethod _selectedDirectTypingMethod = DirectTypingMethod.FastBatch;
    private IReadOnlyList<TextInsertionMode> _insertionModes = Enum.GetValues<TextInsertionMode>();
    private IReadOnlyList<PasteMethod> _pasteMethods = Enum.GetValues<PasteMethod>();
    private IReadOnlyList<DirectTypingMethod> _directTypingMethods = Enum.GetValues<DirectTypingMethod>();

    public TextInsertionMode SelectedInsertionMode
    {
        get => _selectedInsertionMode;
        set
        {
            if (SetProperty(ref _selectedInsertionMode, value))
            {
                OnPropertyChanged(nameof(IsPasteMethodSelectorVisible));
                OnPropertyChanged(nameof(IsDirectTypingMethodSelectorVisible));
            }
        }
    }

    public PasteMethod SelectedPasteMethod
    {
        get => _selectedPasteMethod;
        set => SetProperty(ref _selectedPasteMethod, value);
    }

    public DirectTypingMethod SelectedDirectTypingMethod
    {
        get => _selectedDirectTypingMethod;
        set => SetProperty(ref _selectedDirectTypingMethod, value);
    }

    public IEnumerable<TextInsertionMode> InsertionModes => _insertionModes;

    // Expose enum values for UI
    public IEnumerable<PasteMethod> PasteMethods => _pasteMethods;

    public IEnumerable<DirectTypingMethod> DirectTypingMethods => _directTypingMethods;

    private void OnCultureChanged(object? sender, EventArgs e)
    {
        _insertionModes = Enum.GetValues<TextInsertionMode>();
        _pasteMethods = Enum.GetValues<PasteMethod>();
        _directTypingMethods = Enum.GetValues<DirectTypingMethod>();
        OnPropertyChanged(nameof(ExpansionCountText));
        OnPropertyChanged(nameof(InsertionModes));
        OnPropertyChanged(nameof(PasteMethods));
        OnPropertyChanged(nameof(DirectTypingMethods));
    }

    public string TriggerInput
    {
        get => _triggerInput;
        set
        {
            if (SetProperty(ref _triggerInput, value))
            {
                // Re-evaluate CanExecute for Add command
                (AddExpansionCommand as AsyncRelayCommand)?.NotifyCanExecuteChanged();
            }
        }
    }

    public string ReplacementInput
    {
        get => _replacementInput;
        set
        {
            if (SetProperty(ref _replacementInput, value))
            {
                // Re-evaluate CanExecute for Add command
                (AddExpansionCommand as AsyncRelayCommand)?.NotifyCanExecuteChanged();
            }
        }
    }

    public ObservableCollection<TextExpansionEntry> Expansions
    {
        get => _expansions;
        set => SetProperty(ref _expansions, value);
    }

    public bool HasExpansions => Expansions.Count > 0;

    public string ExpansionCountText => string.Format(
        _localizationService.CurrentCulture,
        _localizationService["TextExpansion_Items"],
        Expansions.Count);

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

        if (_manageTextExpansion is not null)
        {
            var addedExpansion = await _manageTextExpansion.AddAsync(newExpansion).ConfigureAwait(false);
            Expansions.Insert(0, addedExpansion);
            _managedEnabledState[addedExpansion] = addedExpansion.IsEnabled;
        }
        else
        {
            Expansions.Insert(0, newExpansion);
            await _storageService.SaveAsync(Expansions).ConfigureAwait(false);
        }

        // Notify HasExpansions property changed
        OnPropertyChanged(nameof(HasExpansions));
        OnPropertyChanged(nameof(ExpansionCountText));

        // Clear inputs
        TriggerInput = string.Empty;
        ReplacementInput = string.Empty;
        SelectedInsertionMode = TextInsertionMode.Paste;
        // Reset method to default
        SelectedPasteMethod = PasteMethod.CtrlV;
        SelectedDirectTypingMethod = DirectTypingMethod.FastBatch;
    }


    [RelayCommand]
    private async Task RemoveExpansionAsync(TextExpansionEntry? expansion)
    {
        if (expansion is null)
        {
            return;
        }

        var confirmed = await _dialogService.ShowConfirmationAsync(
            _localizationService["TextExpansion_DeleteTitle"],
            string.Format(
                _localizationService.CurrentCulture,
                _localizationService["TextExpansion_DeleteMessage"],
                expansion.Trigger)).ConfigureAwait(false);

        if (!confirmed)
        {
            return;
        }

        if (Expansions.Contains(expansion))
        {
            if (_manageTextExpansion is not null)
            {
                await _manageTextExpansion.RemoveAsync(expansion.Trigger).ConfigureAwait(false);
                Expansions.Remove(expansion);
                _managedEnabledState.Remove(expansion);
            }
            else
            {
                Expansions.Remove(expansion);
                await _storageService.SaveAsync(Expansions).ConfigureAwait(false);
            }

            // Notify HasExpansions property changed
            OnPropertyChanged(nameof(HasExpansions));
            OnPropertyChanged(nameof(ExpansionCountText));
        }
    }

    [RelayCommand]
    private async Task ToggleExpansionAsync(TextExpansionEntry? expansion)
    {
        if (expansion is null)
        {
            return;
        }

        if (_manageTextExpansion is not null)
        {
            var requestedEnabled = expansion.IsEnabled;
            var previousEnabled = _managedEnabledState.TryGetValue(expansion, out var knownEnabled)
                ? knownEnabled
                : requestedEnabled;
            try
            {
                var updatedExpansion = await _manageTextExpansion.SetEnabledAsync(expansion.Trigger, requestedEnabled).ConfigureAwait(false);
                expansion.IsEnabled = updatedExpansion.IsEnabled;
                _managedEnabledState[expansion] = updatedExpansion.IsEnabled;
            }
            catch
            {
                expansion.IsEnabled = previousEnabled;
                throw;
            }
        }
        else
        {
            await _storageService.SaveAsync(Expansions).ConfigureAwait(false);
        }
    }

    public void Dispose()
    {
        _localizationService.CultureChanged -= OnCultureChanged;
    }
}
