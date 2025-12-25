using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services;
using CrossMacro.UI.Services;

namespace CrossMacro.UI.ViewModels;

/// <summary>
/// ViewModel for the Files tab - handles macro save/load operations
/// </summary>
public class FilesViewModel : ViewModelBase
{
    private readonly IMacroFileManager _fileManager;
    private readonly IDialogService _dialogService;
    
    private string _macroName = "New Macro";
    private bool _hasRecordedMacro;
    private string _status = "Ready";
    
    private MacroSequence? _currentMacro;
    
    /// <summary>
    /// Event fired when a macro is loaded
    /// </summary>
    public event EventHandler<MacroSequence>? MacroLoaded;
    
    /// <summary>
    /// Event fired when status changes
    /// </summary>
    public event EventHandler<string>? StatusChanged;
    
    public FilesViewModel(IMacroFileManager fileManager, IDialogService dialogService)
    {
        _fileManager = fileManager;
        _dialogService = dialogService;
    }
    
    public string MacroName
    {
        get => _macroName;
        set
        {
            if (_macroName != value)
            {
                _macroName = value;
                OnPropertyChanged();
                
                // Update macro name if we have one
                if (_currentMacro != null)
                {
                    _currentMacro.Name = value;
                }
            }
        }
    }
    
    public bool HasRecordedMacro
    {
        get => _hasRecordedMacro;
        private set
        {
            if (_hasRecordedMacro != value)
            {
                _hasRecordedMacro = value;
                OnPropertyChanged();
            }
        }
    }
    
    public string Status
    {
        get => _status;
        private set
        {
            if (_status != value)
            {
                _status = value;
                OnPropertyChanged();
                StatusChanged?.Invoke(this, value);
            }
        }
    }
    
    /// <summary>
    /// Set the current macro (called after recording)
    /// </summary>
    public void SetMacro(MacroSequence? macro)
    {
        _currentMacro = macro;
        HasRecordedMacro = macro != null && macro.EventCount > 0;
        
        if (_currentMacro != null)
        {
            _currentMacro.Name = MacroName;
        }
    }
    
    public async Task SaveMacroAsync()
    {
        if (_currentMacro == null)
            return;
        
        try
        {
            var filters = new[]
            {
                new FileDialogFilter { Name = "Macro Files", Extensions = new[] { "*.macro" } }
            };

            var filePath = await _dialogService.ShowSaveFileDialogAsync("Save Macro", $"{MacroName}.macro", filters);
            
            if (string.IsNullOrEmpty(filePath))
            {
                Status = "Save cancelled";
                return;
            }

            _currentMacro.Name = MacroName;
            await _fileManager.SaveAsync(_currentMacro, filePath);
            
            Status = $"Saved to {Path.GetFileName(filePath)}";
        }
        catch (Exception ex)
        {
            Status = $"Save error: {ex.Message}";
        }
    }
    
    public async Task LoadMacroAsync()
    {
        try
        {
            var filters = new[]
            {
                new FileDialogFilter { Name = "Macro Files", Extensions = new[] { "*.macro" } }
            };

            var filePath = await _dialogService.ShowOpenFileDialogAsync("Load Macro", filters);
            
            if (string.IsNullOrEmpty(filePath))
            {
                Status = "Load cancelled";
                return;
            }

            _currentMacro = await _fileManager.LoadAsync(filePath);
            
            if (_currentMacro != null)
            {
                HasRecordedMacro = true;
                MacroName = _currentMacro.Name;
                Status = $"Loaded {Path.GetFileName(filePath)}";
                MacroLoaded?.Invoke(this, _currentMacro);
            }
        }
        catch (Exception ex)
        {
            Status = $"Load error: {ex.Message}";
        }
    }
    
    /// <summary>
    /// Get current macro
    /// </summary>
    public MacroSequence? GetCurrentMacro() => _currentMacro;
}
