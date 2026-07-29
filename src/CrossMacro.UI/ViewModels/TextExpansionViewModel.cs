
namespace CrossMacro.UI.ViewModels;

/// <summary>
/// ViewModel for the Text Expansion tab - handles creating and managing text expansions
/// </summary>
public partial class TextExpansionViewModel : ViewModelBase, IDisposable
{
    private bool _disposed;

    private readonly ITextExpansionStore? _storageService;
    private readonly IDialogService _dialogService;
    private readonly IEnvironmentInfoProvider _environmentInfoProvider;
    private readonly ILocalizationService _localizationService;
    private readonly IManageTextExpansion? _manageTextExpansion;
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

    public Task InitializationTask { get; private set; }

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
        IReadOnlyList<TextExpansionEntry> loadedExpansions;
        if (_storageService is not null)
        {
            loadedExpansions = (IReadOnlyList<TextExpansionEntry>)await _storageService.LoadAsync().ConfigureAwait(false);
        }
        else if (_manageTextExpansion is not null)
        {
            loadedExpansions = await _manageTextExpansion.ListAsync(cancellationToken: default).ConfigureAwait(false);
        }
        else
        {
            loadedExpansions = [];
        }

        await RunOnUiThreadAsync(() =>
        {
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
        }).ConfigureAwait(false);
    }

    public async Task RefreshProfileDataAsync()
    {
        await LoadExpansionsAsync().ConfigureAwait(false);
        await RunOnUiThreadAsync(ResetInputs).ConfigureAwait(false);
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

    public string TriggerInput
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                // Re-evaluate CanExecute for Add command
                (AddExpansionCommand as AsyncRelayCommand)?.NotifyCanExecuteChanged();
            }
        }
    } = string.Empty;

    public string ReplacementInput
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                // Re-evaluate CanExecute for Add command
                (AddExpansionCommand as AsyncRelayCommand)?.NotifyCanExecuteChanged();
            }
        }
    } = string.Empty;

    public ObservableCollection<TextExpansionEntry> Expansions
    {
        get;
        internal set => SetProperty(ref field, value);
    } = new();

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
            var addedExpansion = await _manageTextExpansion.AddAsync(newExpansion, profileIdentifier: null, default).ConfigureAwait(false);
            await RunOnUiThreadAsync(() => AddExpansionToUi(addedExpansion)).ConfigureAwait(false);
        }
        else if (_storageService is not null)
        {
            var expansionsToSave = new[] { newExpansion }.Concat(Expansions).ToArray();
            await _storageService.SaveAsync(expansionsToSave).ConfigureAwait(false);
            await RunOnUiThreadAsync(() => AddExpansionToUi(newExpansion)).ConfigureAwait(false);
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
                _ = await _manageTextExpansion.RemoveAsync(expansion.Trigger, cancellationToken: default).ConfigureAwait(false);
                await RunOnUiThreadAsync(() => RemoveExpansionFromUi(expansion)).ConfigureAwait(false);
            }
            else if (_storageService is not null)
            {
                var expansionsToSave = Expansions.Where(candidate => candidate != expansion).ToArray();
                await _storageService.SaveAsync(expansionsToSave).ConfigureAwait(false);
                await RunOnUiThreadAsync(() => RemoveExpansionFromUi(expansion)).ConfigureAwait(false);
            }
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
                throw;
            }
        }
        else if (_storageService is not null)
        {
            await _storageService.SaveAsync(Expansions).ConfigureAwait(false);
        }
    }

    private void AddExpansionToUi(TextExpansionEntry expansion)
    {
        Expansions.Insert(0, expansion);
        if (_manageTextExpansion is not null)
        {
            _managedEnabledState[expansion] = expansion.IsEnabled;
        }

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

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _localizationService.CultureChanged -= OnCultureChanged;
    }
}
