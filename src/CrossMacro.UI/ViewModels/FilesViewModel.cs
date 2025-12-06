using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services;

namespace CrossMacro.UI.ViewModels;

/// <summary>
/// ViewModel for the Files tab - handles macro save/load operations
/// </summary>
public class FilesViewModel : ViewModelBase
{
    private readonly IMacroFileManager _fileManager;
    
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
    
    public FilesViewModel(IMacroFileManager fileManager)
    {
        _fileManager = fileManager;
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
            var mainWindow = (Avalonia.Application.Current?.ApplicationLifetime 
                as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
            
            if (mainWindow == null)
            {
                Status = "Error: Cannot open file dialog";
                return;
            }

            var dialog = new FilePickerSaveOptions
            {
                Title = "Save Macro",
                SuggestedFileName = $"{MacroName}.macro",
                FileTypeChoices = new[]
                {
                    new FilePickerFileType("Macro Files")
                    {
                        Patterns = new[] { "*.macro" }
                    }
                }
            };

            var result = await mainWindow.StorageProvider.SaveFilePickerAsync(dialog);
            if (result == null)
            {
                Status = "Save cancelled";
                return;
            }

            var filePath = result.Path.LocalPath;
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
            var mainWindow = (Avalonia.Application.Current?.ApplicationLifetime 
                as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
            
            if (mainWindow == null)
            {
                Status = "Error: Cannot open file dialog";
                return;
            }

            var dialog = new FilePickerOpenOptions
            {
                Title = "Load Macro",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("Macro Files")
                    {
                        Patterns = new[] { "*.macro" }
                    }
                }
            };

            var result = await mainWindow.StorageProvider.OpenFilePickerAsync(dialog);
            if (result == null || result.Count == 0)
            {
                Status = "Load cancelled";
                return;
            }

            var filePath = result[0].Path.LocalPath;
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
