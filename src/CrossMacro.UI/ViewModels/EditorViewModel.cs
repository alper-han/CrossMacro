using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services;
using CrossMacro.UI.Services;

namespace CrossMacro.UI.ViewModels;

/// <summary>
/// ViewModel for the Macro Editor tab.
/// Provides manual macro creation and editing capabilities.
/// </summary>
public class EditorViewModel : ViewModelBase, IDisposable
{
    private readonly IEditorActionConverter _converter;
    private readonly IEditorActionValidator _validator;
    private readonly ICoordinateCaptureService _captureService;
    private readonly IMacroFileManager _fileManager;
    private readonly IDialogService _dialogService;
    private readonly IKeyCodeMapper _keyCodeMapper;
    
    private readonly Stack<List<EditorAction>> _undoStack = new(50);
    private readonly Stack<List<EditorAction>> _redoStack = new(50);
    
    private EditorAction? _selectedAction;
    private EditorActionType _newActionType = EditorActionType.MouseClick;
    private string _macroName = "Manual Macro";
    private string _status = "Ready";
    private bool _isCapturing;
    private bool _skipInitialZeroZero;
    private bool _disposed;
    
    /// <summary>
    /// Event fired when a macro is created/saved.
    /// </summary>
    public event EventHandler<MacroSequence>? MacroCreated;
    
    /// <summary>
    /// Event fired when status changes.
    /// </summary>
    public event EventHandler<string>? StatusChanged;
    
    public EditorViewModel(
        IEditorActionConverter converter,
        IEditorActionValidator validator,
        ICoordinateCaptureService captureService,
        IMacroFileManager fileManager,
        IDialogService dialogService,
        IKeyCodeMapper keyCodeMapper)
    {
        _converter = converter ?? throw new ArgumentNullException(nameof(converter));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _captureService = captureService ?? throw new ArgumentNullException(nameof(captureService));
        _fileManager = fileManager ?? throw new ArgumentNullException(nameof(fileManager));
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
        _keyCodeMapper = keyCodeMapper ?? throw new ArgumentNullException(nameof(keyCodeMapper));
        
        Actions = new ObservableCollection<EditorAction>();
        Actions.CollectionChanged += (s, e) => UpdateActionIndices();
    }
    
    private void UpdateActionIndices()
    {
        for (int i = 0; i < Actions.Count; i++)
        {
            Actions[i].Index = i + 1; // 1-based index
        }
    }
    
    #region Properties
    
    public ObservableCollection<EditorAction> Actions { get; }
    
    public EditorAction? SelectedAction
    {
        get => _selectedAction;
        set
        {
            if (_selectedAction != value)
            {
                // Unsubscribe from old action
                if (_selectedAction != null)
                    _selectedAction.PropertyChanged -= OnSelectedActionPropertyChanged;
                
                _selectedAction = value;
                
                // Subscribe to new action
                if (_selectedAction != null)
                    _selectedAction.PropertyChanged += OnSelectedActionPropertyChanged;
                
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasSelectedAction));
                
                // Notify all visibility properties
                NotifyVisibilityChanged();
            }
        }
    }
    
    private void OnSelectedActionPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        // Skip derived/computed properties that shouldn't trigger undo
        var skipProperties = new[] { nameof(EditorAction.DisplayName), nameof(EditorAction.Index), nameof(EditorAction.KeyName) };
        
        // Save undo state for editable property changes
        if (e.PropertyName != null && !skipProperties.Contains(e.PropertyName))
        {
            // Debounce: don't save if we just saved within 500ms (prevents multiple undos for same edit)
            if (!_isRestoringState)
            {
                SaveUndoState();
            }
        }
        
        // When action's Type changes, update visibility
        if (e.PropertyName == nameof(EditorAction.Type))
        {
            NotifyVisibilityChanged();
        }
        
        // When KeyCode changes manually, update KeyName
        if (e.PropertyName == nameof(EditorAction.KeyCode) && sender is EditorAction action)
        {
            if (action.KeyCode > 0)
            {
                var newKeyName = _keyCodeMapper.GetKeyName(action.KeyCode);
                if (action.KeyName != newKeyName)
                {
                    action.KeyName = newKeyName;
                }
            }
            else
            {
                action.KeyName = null;
            }
        }
    }
    
    private bool _isRestoringState = false;
    
    public bool HasSelectedAction => _selectedAction != null;
    
    public EditorActionType NewActionType
    {
        get => _newActionType;
        set
        {
            if (_newActionType != value)
            {
                _newActionType = value;
                OnPropertyChanged();
            }
        }
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
    
    public bool IsCapturing
    {
        get => _isCapturing;
        private set
        {
            if (_isCapturing != value)
            {
                _isCapturing = value;
                OnPropertyChanged();
            }
        }
    }
    
    public bool CanUndo => _undoStack.Count > 0;
    public bool CanRedo => _redoStack.Count > 0;
    public bool HasActions => Actions.Count > 0;
    
    public IEnumerable<EditorActionType> ActionTypes => Enum.GetValues<EditorActionType>();
    public IEnumerable<MouseButton> MouseButtons => Enum.GetValues<MouseButton>().Where(b => b != MouseButton.None);
    
    #endregion
    
    #region Visibility Properties
    
    /// <summary>
    /// Show coordinates for: MouseMove only (other mouse events don't use coordinates)
    /// </summary>
    public bool ShowCoordinates => SelectedAction?.Type == EditorActionType.MouseMove;
    
    /// <summary>
    /// Show Absolute/Relative toggle for: MouseMove only
    /// </summary>
    public bool ShowCoordModeToggle => SelectedAction?.Type == EditorActionType.MouseMove;
    
    /// <summary>
    /// Show mouse button for: MouseClick, MouseDown, MouseUp
    /// </summary>
    public bool ShowMouseButton => SelectedAction?.Type is 
        EditorActionType.MouseClick or 
        EditorActionType.MouseDown or 
        EditorActionType.MouseUp;
    
    /// <summary>
    /// Show key code for: KeyPress, KeyDown, KeyUp
    /// </summary>
    public bool ShowKeyCode => SelectedAction?.Type is 
        EditorActionType.KeyPress or 
        EditorActionType.KeyDown or 
        EditorActionType.KeyUp;
    
    /// <summary>
    /// Show delay for: Delay action only (other actions have timing handled differently)
    /// </summary>
    public bool ShowDelay => SelectedAction?.Type == EditorActionType.Delay;
    
    /// <summary>
    /// Show scroll amount for: ScrollVertical, ScrollHorizontal
    /// </summary>
    public bool ShowScrollAmount => SelectedAction?.Type is 
        EditorActionType.ScrollVertical or 
        EditorActionType.ScrollHorizontal;
    
    private void NotifyVisibilityChanged()
    {
        OnPropertyChanged(nameof(ShowCoordinates));
        OnPropertyChanged(nameof(ShowCoordModeToggle));
        OnPropertyChanged(nameof(ShowMouseButton));
        OnPropertyChanged(nameof(ShowKeyCode));
        OnPropertyChanged(nameof(ShowDelay));
        OnPropertyChanged(nameof(ShowScrollAmount));
    }
    
    #endregion
    
    #region Action Methods
    
    private void SaveUndoState()
    {
        var state = Actions.Select(a => a.Clone()).ToList();
        _undoStack.Push(state);
        _redoStack.Clear();
        
        // Limit stack size
        while (_undoStack.Count > 50)
        {
            var items = _undoStack.ToArray();
            _undoStack.Clear();
            for (int i = 0; i < 49; i++)
                _undoStack.Push(items[items.Length - 1 - i]);
        }
        
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
    }
    
    public void AddAction()
    {
        SaveUndoState();
        
        var action = new EditorAction
        {
            Type = NewActionType,
            IsAbsolute = true, // Default to absolute
            DelayMs = NewActionType == EditorActionType.Delay ? 100 : 0,
            ScrollAmount = NewActionType is EditorActionType.ScrollVertical or EditorActionType.ScrollHorizontal ? 1 : 0
        };
        
        Actions.Add(action);
        SelectedAction = action;
        Status = $"Added {action.DisplayName}";
        OnPropertyChanged(nameof(HasActions));
    }
    
    public void RemoveAction()
    {
        if (SelectedAction == null) return;
        
        SaveUndoState();
        var index = Actions.IndexOf(SelectedAction);
        Actions.Remove(SelectedAction);
        
        // Select adjacent action
        if (Actions.Count > 0)
        {
            SelectedAction = Actions[Math.Min(index, Actions.Count - 1)];
        }
        else
        {
            SelectedAction = null;
        }
        
        Status = "Removed action";
        OnPropertyChanged(nameof(HasActions));
    }
    
    public void MoveUp()
    {
        if (SelectedAction == null) return;
        
        var index = Actions.IndexOf(SelectedAction);
        if (index <= 0) return;
        
        SaveUndoState();
        var action = SelectedAction;
        Actions.Move(index, index - 1);
        SelectedAction = action; // Re-select after move
        Status = "Moved action up";
    }
    
    public void MoveDown()
    {
        if (SelectedAction == null) return;
        
        var index = Actions.IndexOf(SelectedAction);
        if (index < 0 || index >= Actions.Count - 1) return;
        
        SaveUndoState();
        var action = SelectedAction;
        Actions.Move(index, index + 1);
        SelectedAction = action; // Re-select after move
        Status = "Moved action down";
    }
    
    public void DuplicateAction()
    {
        if (SelectedAction == null) return;
        
        SaveUndoState();
        var clone = SelectedAction.Clone();
        var index = Actions.IndexOf(SelectedAction);
        Actions.Insert(index + 1, clone);
        SelectedAction = clone;
        Status = "Duplicated action";
        OnPropertyChanged(nameof(HasActions));
    }
    
    public void ClearAll()
    {
        if (Actions.Count == 0) return;
        
        SaveUndoState();
        Actions.Clear();
        SelectedAction = null;
        Status = "Cleared all actions";
        OnPropertyChanged(nameof(HasActions));
    }
    
    public void Undo()
    {
        if (_undoStack.Count == 0) return;
        
        _isRestoringState = true;
        try
        {
            // Save current state to redo
            var currentState = Actions.Select(a => a.Clone()).ToList();
            _redoStack.Push(currentState);
            
            // Restore previous state
            var previousState = _undoStack.Pop();
            Actions.Clear();
            foreach (var action in previousState)
            {
                Actions.Add(action);
            }
            
            SelectedAction = Actions.FirstOrDefault();
            Status = "Undone";
            
            OnPropertyChanged(nameof(CanUndo));
            OnPropertyChanged(nameof(CanRedo));
            OnPropertyChanged(nameof(HasActions));
        }
        finally
        {
            _isRestoringState = false;
        }
    }
    
    public void Redo()
    {
        if (_redoStack.Count == 0) return;
        
        _isRestoringState = true;
        try
        {
            // Save current state to undo
            var currentState = Actions.Select(a => a.Clone()).ToList();
            _undoStack.Push(currentState);
            
            // Restore next state
            var nextState = _redoStack.Pop();
            Actions.Clear();
            foreach (var action in nextState)
            {
                Actions.Add(action);
            }
            
            SelectedAction = Actions.FirstOrDefault();
            Status = "Redone";
            
            OnPropertyChanged(nameof(CanUndo));
            OnPropertyChanged(nameof(CanRedo));
            OnPropertyChanged(nameof(HasActions));
        }
        finally
        {
            _isRestoringState = false;
        }
    }
    
    #endregion
    
    #region Capture Methods
    
    public async Task CaptureMouseAsync()
    {
        if (SelectedAction == null)
        {
            Status = "Select an action first";
            return;
        }
        
        IsCapturing = true;
        Status = "Click anywhere to capture position (Esc to cancel)...";
        
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var result = await _captureService.CaptureMousePositionAsync(cts.Token);
            
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (result.HasValue)
                {
                    SelectedAction.X = result.Value.X;
                    SelectedAction.Y = result.Value.Y;
                    Status = $"Captured position: ({result.Value.X}, {result.Value.Y})";
                }
                else
                {
                    Status = "Capture cancelled";
                }
            });
        }
        catch (Exception ex)
        {
            Status = $"Capture error: {ex.Message}";
        }
        finally
        {
            IsCapturing = false;
        }
    }
    
    public async Task CaptureKeyAsync()
    {
        if (SelectedAction == null)
        {
            Status = "Select an action first";
            return;
        }
        
        IsCapturing = true;
        Status = "Press a key to capture...";
        
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var result = await _captureService.CaptureKeyCodeAsync(cts.Token);
            
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (result.HasValue)
                {
                    SelectedAction.KeyCode = result.Value;
                    SelectedAction.KeyName = _keyCodeMapper.GetKeyName(result.Value);
                    Status = $"Captured: {SelectedAction.KeyName} (code: {result.Value})";
                }
                else
                {
                    Status = "Capture cancelled";
                }
            });
        }
        catch (Exception ex)
        {
            Status = $"Capture error: {ex.Message}";
        }
        finally
        {
            IsCapturing = false;
        }
    }
    
    public void CancelCapture()
    {
        _captureService.CancelCapture();
        IsCapturing = false;
        Status = "Capture cancelled";
    }
    
    #endregion
    
    #region File Operations
    
    public async Task SaveMacroAsync()
    {
        if (Actions.Count == 0)
        {
            await _dialogService.ShowMessageAsync("No Actions", "Please add at least one action before saving.");
            return;
        }
        
        // Validate all actions
        var (isValid, errors) = _validator.ValidateAll(Actions);
        if (!isValid)
        {
            var errorMessage = "Please fix the following errors:\n\n" + string.Join("\n", errors.Select(e => $"• {e}"));
            await _dialogService.ShowMessageAsync("Validation Errors", errorMessage);
            Status = $"Validation failed: {errors.Count} error(s)";
            return;
        }
        
        try
        {
            var filters = new[]
            {
                new FileDialogFilter { Name = "Macro Files", Extensions = new[] { "*.macro" } }
            };
            
            var baseName = MacroName.EndsWith(".macro", StringComparison.OrdinalIgnoreCase)
                ? MacroName[..^6]
                : MacroName;
            var filePath = await _dialogService.ShowSaveFileDialogAsync("Save Macro", $"{baseName}.macro", filters);
            
            if (string.IsNullOrEmpty(filePath))
            {
                Status = "Save cancelled";
                return;
            }
            
            // Determine if sequence is absolute based on any MouseMove action
            var isAbsolute = Actions.Any(a => a.Type == EditorActionType.MouseMove && a.IsAbsolute);
            
            var sequence = _converter.ToMacroSequence(Actions, MacroName, isAbsolute, _skipInitialZeroZero);
            await _fileManager.SaveAsync(sequence, filePath);
            
            Status = $"Saved: {System.IO.Path.GetFileName(filePath)}";
            MacroCreated?.Invoke(this, sequence);
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
            
            var sequence = await _fileManager.LoadAsync(filePath);
            
            if (sequence == null)
            {
                Status = "Failed to load macro";
                return;
            }
            
            LoadMacroSequence(sequence);
            Status = $"Loaded: {System.IO.Path.GetFileName(filePath)}";
        }
        catch (Exception ex)
        {
            Status = $"Load error: {ex.Message}";
        }
    }
    
    /// <summary>
    /// Loads a MacroSequence for editing.
    /// </summary>
    public void LoadMacroSequence(MacroSequence sequence)
    {
        SaveUndoState();
        
        Actions.Clear();
        MacroName = sequence.Name;
        _skipInitialZeroZero = sequence.SkipInitialZeroZero;
        
        var editorActions = _converter.FromMacroSequence(sequence);
        foreach (var action in editorActions)
        {
            Actions.Add(action);
        }
        
        SelectedAction = Actions.FirstOrDefault();
        OnPropertyChanged(nameof(HasActions));
    }
    
    #endregion
    
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        
        _captureService.CancelCapture();
    }
}
