using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services;
using CrossMacro.UI.Services;

namespace CrossMacro.UI.ViewModels;

/// <summary>
/// ViewModel for the Macro Editor tab.
/// Provides manual macro creation and editing capabilities.
/// </summary>
public partial class EditorViewModel : ViewModelBase, IDisposable
{
    private const int UndoStackLimit = 50;
    private static readonly TimeSpan PropertyEditUndoCoalesceWindow = TimeSpan.FromMilliseconds(400);
    private const string DefaultMacroName = "Manual Macro";
    private const string InitialStatusText = "Ready";
    private const string StatusRemovedAction = "Removed action";
    private const string StatusMovedActionUp = "Moved action up";
    private const string StatusMovedActionDown = "Moved action down";
    private const string StatusDuplicatedAction = "Duplicated action";
    private const string StatusClearedAllActions = "Cleared all actions";
    private const string StatusUndone = "Undone";
    private const string StatusRedone = "Redone";
    private const string StatusSelectActionFirst = "Select an action first";
    private const string StatusCaptureMousePrompt = "Click anywhere to capture position (Esc to cancel)...";
    private const string StatusCaptureKeyPrompt = "Press a key to capture...";
    private const string StatusCaptureCancelled = "Capture cancelled";
    private const string StatusCaptureSelectionChanged = "Capture ignored: selected action changed";
    private const string StatusSaveCancelled = "Save cancelled";
    private const string StatusLoadCancelled = "Load cancelled";
    private const string StatusLoadFailed = "Failed to load macro";
    private const string SaveDialogTitle = "Save Macro";
    private const string LoadDialogTitle = "Load Macro";
    private const string MacroFileDialogName = "Macro Files";
    private const string MacroFileExtension = ".macro";
    private const string DialogTitleNoActions = "No Actions";
    private const string DialogMessageNoActions = "Please add at least one action before saving.";
    private const string DialogTitleValidationErrors = "Validation Errors";
    private const string ValidationErrorHeader = "Please fix the following errors:";

    private readonly IEditorActionConverter _converter;
    private readonly IEditorActionValidator _validator;
    private readonly ICoordinateCaptureService _captureService;
    private readonly IMacroFileManager _fileManager;
    private readonly IDialogService _dialogService;
    private readonly IKeyCodeMapper _keyCodeMapper;

    private readonly Stack<List<EditorAction>> _undoStack = new(UndoStackLimit);
    private readonly Stack<List<EditorAction>> _redoStack = new(UndoStackLimit);

    private EditorAction? _selectedAction;
    private EditorActionType _newActionType = EditorActionType.MouseClick;
    private string _macroName = DefaultMacroName;
    private string _status = InitialStatusText;
    private bool _isCapturing;
    private bool _skipInitialZeroZero;
    private bool _isRestoringState;
    private bool _disposed;
    private List<EditorAction> _lastKnownState = new();
    private DateTimeOffset _lastPropertyEditUndoAt = DateTimeOffset.MinValue;
    private EditorAction? _lastPropertyEditAction;
    private string? _lastPropertyEditName;

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
        Actions.CollectionChanged += (_, _) => UpdateActionIndices();
        RememberCurrentState();
    }

    #region Properties

    public ObservableCollection<EditorAction> Actions { get; }

    public EditorAction? SelectedAction
    {
        get => _selectedAction;
        set
        {
            if (_selectedAction == value)
            {
                return;
            }

            if (_selectedAction != null)
            {
                _selectedAction.PropertyChanged -= OnSelectedActionPropertyChanged;
            }

            _selectedAction = value;

            if (_selectedAction != null)
            {
                _selectedAction.PropertyChanged += OnSelectedActionPropertyChanged;
            }

            OnPropertyChanged();
            OnPropertyChanged(nameof(HasSelectedAction));
            NotifyVisibilityChanged();
            ResetPropertyEditUndoCoalescing();
        }
    }

    public bool HasSelectedAction => _selectedAction != null;

    public EditorActionType NewActionType
    {
        get => _newActionType;
        set
        {
            if (_newActionType == value)
            {
                return;
            }

            _newActionType = value;
            OnPropertyChanged();
        }
    }

    public string MacroName
    {
        get => _macroName;
        set
        {
            if (_macroName == value)
            {
                return;
            }

            _macroName = value;
            OnPropertyChanged();
        }
    }

    public string Status
    {
        get => _status;
        private set
        {
            if (_status == value)
            {
                return;
            }

            _status = value;
            OnPropertyChanged();
            StatusChanged?.Invoke(this, value);
        }
    }

    public bool IsCapturing
    {
        get => _isCapturing;
        private set
        {
            if (_isCapturing == value)
            {
                return;
            }

            _isCapturing = value;
            OnPropertyChanged();
        }
    }

    public bool CanUndo => _undoStack.Count > 0;
    public bool CanRedo => _redoStack.Count > 0;
    public bool HasActions => Actions.Count > 0;

    public IEnumerable<EditorActionType> ActionTypes => Enum.GetValues<EditorActionType>();
    public IEnumerable<MouseButton> MouseButtons => Enum.GetValues<MouseButton>().Where(button => button != MouseButton.None);

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
    /// Show fixed delay value when random delay is disabled.
    /// </summary>
    public bool ShowFixedDelayInput => ShowDelay && SelectedAction?.UseRandomDelay != true;

    /// <summary>
    /// Show random delay bounds when random delay is enabled.
    /// </summary>
    public bool ShowRandomDelayOptions => ShowDelay && SelectedAction?.UseRandomDelay == true;

    /// <summary>
    /// Show scroll amount for: ScrollVertical, ScrollHorizontal
    /// </summary>
    public bool ShowScrollAmount => SelectedAction?.Type is
        EditorActionType.ScrollVertical or
        EditorActionType.ScrollHorizontal;

    /// <summary>
    /// Show text input field for: TextInput action only
    /// </summary>
    public bool ShowTextInput => SelectedAction?.Type == EditorActionType.TextInput;

    #endregion

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _captureService.CancelCapture();
    }
}
