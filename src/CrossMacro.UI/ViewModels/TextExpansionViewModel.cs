using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services;
using CrossMacro.Infrastructure.Services;
using CrossMacro.UI.Localization;
using CrossMacro.UI.Services;

namespace CrossMacro.UI.ViewModels;

/// <summary>
/// ViewModel for the Text Expansion tab - handles creating and managing text expansions
/// </summary>
public partial class TextExpansionViewModel : ViewModelBase, IDisposable
{
    private readonly ITextExpansionStorageService _storageService;
    private readonly IDialogService _dialogService;
    private readonly IEnvironmentInfoProvider _environmentInfoProvider;
    private readonly ILocalizationService _localizationService;

    private string _triggerInput = string.Empty;
    private string _replacementInput = string.Empty;
    private ObservableCollection<TextExpansion> _expansions = new();
    
    public TextExpansionViewModel(
        ITextExpansionStorageService storageService, 
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

    public Task InitializationTask { get; private set; } = Task.CompletedTask;
    
    public bool IsPasteMethodVisible => IsLinuxEnvironment(_environmentInfoProvider.CurrentEnvironment);

    public bool IsPasteMethodSelectorVisible =>
        IsPasteMethodVisible && SelectedInsertionMode == TextInsertionMode.Paste;

    private static bool IsLinuxEnvironment(DisplayEnvironment env)
    {
        return env == DisplayEnvironment.LinuxX11 ||
               env == DisplayEnvironment.LinuxWayland ||
               env == DisplayEnvironment.LinuxHyprland ||
               env == DisplayEnvironment.LinuxKDE ||
               env == DisplayEnvironment.LinuxGnome;
    }

    private async Task LoadExpansionsAsync()
    {
        var loadedExpansions = await _storageService.LoadAsync();
        
        // Ensure UI update happens on UI thread (though usually ViewModels are on UI thread anyway)
        foreach (var expansion in loadedExpansions)
        {
            _expansions.Add(expansion);
        }

        OnPropertyChanged(nameof(HasExpansions));
        OnPropertyChanged(nameof(ExpansionCountText));
    }

    private PasteMethod _selectedPasteMethod = PasteMethod.CtrlV;
    private TextInsertionMode _selectedInsertionMode = TextInsertionMode.Paste;
    private IReadOnlyList<TextInsertionMode> _insertionModes = Enum.GetValues<TextInsertionMode>();
    private IReadOnlyList<PasteMethod> _pasteMethods = Enum.GetValues<PasteMethod>();

    public TextInsertionMode SelectedInsertionMode
    {
        get => _selectedInsertionMode;
        set
        {
            if (SetProperty(ref _selectedInsertionMode, value))
            {
                OnPropertyChanged(nameof(IsPasteMethodSelectorVisible));
            }
        }
    }

    public PasteMethod SelectedPasteMethod
    {
        get => _selectedPasteMethod;
        set => SetProperty(ref _selectedPasteMethod, value);
    }

    public IEnumerable<TextInsertionMode> InsertionModes => _insertionModes;
    
    // Expose enum values for UI
    public IEnumerable<PasteMethod> PasteMethods => _pasteMethods;

    private void OnCultureChanged(object? sender, EventArgs e)
    {
        _insertionModes = Enum.GetValues<TextInsertionMode>();
        _pasteMethods = Enum.GetValues<PasteMethod>();
        OnPropertyChanged(nameof(ExpansionCountText));
        OnPropertyChanged(nameof(InsertionModes));
        OnPropertyChanged(nameof(PasteMethods));
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

    public ObservableCollection<TextExpansion> Expansions
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
        var newExpansion = new TextExpansion(
            TriggerInput,
            ReplacementInput,
            true,
            SelectedPasteMethod,
            SelectedInsertionMode);
        
        // Add to UI collection
        Expansions.Insert(0, newExpansion);
        
        // Save to storage
        await _storageService.SaveAsync(Expansions);
        
        // Notify HasExpansions property changed
        OnPropertyChanged(nameof(HasExpansions));
        OnPropertyChanged(nameof(ExpansionCountText));
        
        // Clear inputs
        TriggerInput = string.Empty;
        ReplacementInput = string.Empty;
        SelectedInsertionMode = TextInsertionMode.Paste;
        // Reset method to default
        SelectedPasteMethod = PasteMethod.CtrlV; 
    }


    [RelayCommand]
    private async Task RemoveExpansionAsync(TextExpansion? expansion)
    {
        if (expansion == null) return;
        
        var confirmed = await _dialogService.ShowConfirmationAsync(
            _localizationService["TextExpansion_DeleteTitle"],
            string.Format(
                _localizationService.CurrentCulture,
                _localizationService["TextExpansion_DeleteMessage"],
                expansion.Trigger));
            
        if (!confirmed) return;

        if (Expansions.Remove(expansion))
        {
            await _storageService.SaveAsync(Expansions);
            
            // Notify HasExpansions property changed
            OnPropertyChanged(nameof(HasExpansions));
            OnPropertyChanged(nameof(ExpansionCountText));
        }
    }

    
    [RelayCommand]
    private async Task ToggleExpansionAsync(TextExpansion? expansion)
    {
        if (expansion == null) return;
        
        // The IsEnabled property is bound TwoWay, so it's already updated in the object.
        // We just need to persist the changes.
        await _storageService.SaveAsync(Expansions);
    }

    public void Dispose()
    {
        _localizationService.CultureChanged -= OnCultureChanged;
    }
}
