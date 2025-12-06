using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services;
using CrossMacro.Infrastructure.Services;

namespace CrossMacro.UI.ViewModels;

/// <summary>
/// ViewModel for the Text Expansion tab - handles creating and managing text expansions
/// </summary>
public partial class TextExpansionViewModel : ViewModelBase
{
    private readonly TextExpansionStorageService _storageService;

    private string _triggerInput = string.Empty;
    private string _replacementInput = string.Empty;
    private ObservableCollection<TextExpansion> _expansions = new();
    
    public TextExpansionViewModel(TextExpansionStorageService storageService)
    {
        _storageService = storageService;
        
        // Load existing expansions asynchronously
        _ = LoadExpansionsAsync();
    }

    private async Task LoadExpansionsAsync()
    {
        var loadedExpansions = await _storageService.LoadAsync();
        
        // Ensure UI update happens on UI thread (though usually ViewModels are on UI thread anyway)
        foreach (var expansion in loadedExpansions)
        {
            _expansions.Add(expansion);
        }
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

    private bool CanAddExpansion()
    {
        return !string.IsNullOrWhiteSpace(TriggerInput) && 
               !string.IsNullOrWhiteSpace(ReplacementInput);
    }

    [RelayCommand(CanExecute = nameof(CanAddExpansion))]
    private async Task AddExpansionAsync()
    {
        var newExpansion = new TextExpansion(TriggerInput, ReplacementInput);
        
        // Add to UI collection
        Expansions.Insert(0, newExpansion);
        
        // Save to storage
        await _storageService.SaveAsync(Expansions);
        
        // Notify HasExpansions property changed
        OnPropertyChanged(nameof(HasExpansions));
        
        // Clear inputs
        TriggerInput = string.Empty;
        ReplacementInput = string.Empty;
    }


    [RelayCommand]
    private async Task RemoveExpansionAsync(TextExpansion? expansion)
    {
        if (expansion == null) return;
        
        if (Expansions.Remove(expansion))
        {
            await _storageService.SaveAsync(Expansions);
            
            // Notify HasExpansions property changed
            OnPropertyChanged(nameof(HasExpansions));
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
}

