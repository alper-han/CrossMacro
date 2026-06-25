using System;
using System.Collections.Specialized;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services;
using CrossMacro.Platform.Abstractions;
using CrossMacro.UI.Localization;
using CrossMacro.UI.Services;

namespace CrossMacro.UI.ViewModels;

/// <summary>
/// ViewModel for the Macro Editor tab.
/// Provides manual macro creation and editing capabilities.
/// </summary>
public partial class EditorViewModel : ViewModelBase, IDisposable
{
    private enum EditorStatusKind
    {
        Ready,
        Other
    }

    private enum ScriptVariableKind
    {
        Unknown,
        Number,
        Text,
        Boolean,
        Color
    }

    private const int UndoStackLimit = 50;
    private static readonly TimeSpan PropertyEditUndoCoalesceWindow = TimeSpan.FromMilliseconds(400);
    private const string MacroFileExtension = ".macro";

    private readonly IEditorActionConverter _converter;
    private readonly IEditorActionValidator _validator;
    private readonly ICoordinateCaptureService _captureService;
    private readonly IMacroFileManager _fileManager;
    private readonly IDialogService _dialogService;
    private readonly IKeyCodeMapper _keyCodeMapper;
    private readonly ILocalizationService _localizationService;
    private readonly EditorActionDisplayFormatter _actionDisplayFormatter;
    private readonly IScreenPixelReader? _screenPixelReader;

    private readonly Stack<List<EditorAction>> _undoStack = new(UndoStackLimit);
    private readonly Stack<List<EditorAction>> _redoStack = new(UndoStackLimit);

    private EditorAction? _selectedAction;
    private EditorActionListItem? _selectedActionListItem;
    private EditorActionType _newActionType = EditorActionType.MouseClick;
    private string _macroName;
    private string _status;
    private bool _isCapturing;
    private bool _skipInitialZeroZero;
    private bool _skipInitialZeroZeroForcedByCurrentPosition;
    private bool _skipInitialZeroZeroBeforeCurrentPositionForce;
    private bool _isRestoringState;
    private bool _isSynchronizingActionProperties;
    private bool _isApplyingVariableSuggestion;
    private bool _isSelectingFromActionList;
    private bool _isSynchronizingSelectedUnderlyingIndices;
    private bool _disposed;
    private bool _usesDefaultMacroName = true;
    private bool _hideMouseMoves;
    private bool _hideShortWaits;
    private bool _simplifyMovement;
    private bool _isApplyingStatusKind;
    private EditorStatusKind _statusKind = EditorStatusKind.Ready;
    private List<EditorAction> _lastKnownState = new();
    private IReadOnlyList<string> _availableVariableNames = Array.Empty<string>();
    private IReadOnlyList<string> _availableColorVariableNames = Array.Empty<string>();
    private string? _selectedSetVariableSuggestion;
    private string? _selectedIncDecVariableSuggestion;
    private string? _selectedConditionLeftVariableSuggestion;
    private string? _selectedConditionRightVariableSuggestion;
    private string? _selectedForVariableSuggestion;
    private string? _selectedScreenTargetColorVariableSuggestion;
    private Guid? _linkedLoadedMacroSessionId;
    private DateTimeOffset _lastPropertyEditUndoAt = DateTimeOffset.MinValue;
    private EditorAction? _lastPropertyEditAction;
    private string? _lastPropertyEditName;
    private readonly HashSet<EditorAction> _subscribedActions = new();
    private static readonly IReadOnlyList<EditorActionType> EditorAddableActionTypes = Enum
        .GetValues<EditorActionType>()
        .Where(IsUserAddableActionType)
        .ToArray();
    private static readonly IReadOnlyList<EditorActionScreenTargetColorSource> EditorScreenTargetColorSources =
        new[]
        {
            EditorActionScreenTargetColorSource.ManualHex,
            EditorActionScreenTargetColorSource.Variable
        };

    /// <summary>
    /// Event fired when a macro is created/saved.
    /// Includes the persisted source path chosen during save.
    /// </summary>
    public event EventHandler<EditorMacroCreatedEventArgs>? MacroCreated;

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
        IKeyCodeMapper keyCodeMapper,
        ILocalizationService? localizationService = null,
        EditorActionDisplayFormatter? actionDisplayFormatter = null,
        IScreenPixelReader? screenPixelReader = null)
    {
        _converter = converter ?? throw new ArgumentNullException(nameof(converter));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _captureService = captureService ?? throw new ArgumentNullException(nameof(captureService));
        _fileManager = fileManager ?? throw new ArgumentNullException(nameof(fileManager));
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
        _keyCodeMapper = keyCodeMapper ?? throw new ArgumentNullException(nameof(keyCodeMapper));
        _localizationService = localizationService ?? new LocalizationService();
        _actionDisplayFormatter = actionDisplayFormatter ?? new EditorActionDisplayFormatter(_localizationService);
        _screenPixelReader = screenPixelReader;
        _macroName = _localizationService["Editor_DefaultMacroName"];
        _status = BuildStatus(EditorStatusKind.Ready);

        Actions = new ObservableCollection<EditorAction>();
        ActionListItems = new ObservableCollection<EditorActionListItem>();
        SelectedActionUnderlyingIndices = new ObservableCollection<int>();
        LoadWarnings = new ObservableCollection<string>();
        Actions.CollectionChanged += OnActionsCollectionChanged;
        SelectedActionUnderlyingIndices.CollectionChanged += OnSelectedActionUnderlyingIndicesChanged;
        LoadWarnings.CollectionChanged += OnLoadWarningsCollectionChanged;
        _localizationService.CultureChanged += OnCultureChanged;
        RefreshAvailableVariableNames();
        RememberCurrentState();
    }

    #region Properties

    public ObservableCollection<EditorAction> Actions { get; }

    public ObservableCollection<EditorActionListItem> ActionListItems { get; }

    public ObservableCollection<int> SelectedActionUnderlyingIndices { get; }

    public ObservableCollection<string> LoadWarnings { get; }

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
                NormalizeSelectedActionState(_selectedAction);
            }

            OnPropertyChanged();
            OnPropertyChanged(nameof(HasSelectedAction));
            OnPropertyChanged(nameof(SelectedActionIsAbsolute));
            OnPropertyChanged(nameof(SelectedActionIsRelative));
            NotifyVisibilityChanged();
            OnPropertyChanged(nameof(SelectedActionDisplayText));
            ResetPropertyEditUndoCoalescing();
            SyncSelectedActionListItem();
            if (!_isSelectingFromActionList)
            {
                SyncSelectedUnderlyingIndicesToPrimarySelection();
            }
        }
    }

    public EditorActionListItem? SelectedActionListItem
    {
        get => _selectedActionListItem;
        set
        {
            if (ReferenceEquals(_selectedActionListItem, value))
            {
                return;
            }

            _selectedActionListItem = value;
            OnPropertyChanged();

            if (_isSelectingFromActionList)
            {
                return;
            }

            _isSelectingFromActionList = true;
            try
            {
                if (!ReferenceEquals(SelectedAction, value?.Action))
                {
                    SelectedAction = value?.Action;
                }
            }
            finally
            {
                _isSelectingFromActionList = false;
            }
        }
    }

    public bool SelectedActionIsAbsolute
    {
        get => SelectedAction?.IsAbsolute ?? false;
        set
        {
            if (value)
            {
                SetSelectedActionCoordinateMode(isAbsolute: true);
            }
        }
    }

    public bool SelectedActionIsRelative
    {
        get => SelectedAction is { IsAbsolute: false };
        set
        {
            if (value)
            {
                SetSelectedActionCoordinateMode(isAbsolute: false);
            }
        }
    }

    public bool HasSelectedAction => _selectedAction != null;
    public bool HasSelectedActions => SelectedActionUnderlyingIndices.Count > 0;
    public int SelectedActionCount => SelectedActionUnderlyingIndices.Count;
    public bool ShowSingleSelectedActionProperties => HasSelectedAction && SelectedActionCount <= 1;
    public bool ShowBatchDelayProperties => SelectedActionCount > 1 && GetSelectedActions().All(action => action.Type == EditorActionType.Delay);
    public bool ShowMultiSelectionPropertiesHint => SelectedActionCount > 1 && !ShowBatchDelayProperties;
    public bool ShowBatchFixedDelayInput => ShowBatchDelayProperties && !BatchDelayUseRandomDelay;
    public bool ShowBatchRandomDelayOptions => ShowBatchDelayProperties && BatchDelayUseRandomDelay;
    public bool CanRemoveSelectedActions => HasSelectedActions;
    public bool CanDeleteHiddenEvents => Actions
        .Select((action, index) => new { action, index })
        .Any(item => IsHiddenByActiveFilters(item.action, IsInsideMouseDrag(item.index)));
    public bool ShowDeleteHiddenEvents => (HideMouseMoves || HideShortWaits) && CanDeleteHiddenEvents;
    public bool CanHideMouseMoves => Actions.Any(action => action.Type == EditorActionType.MouseMove);
    public bool ShowHideMouseMovesToggle => HideMouseMoves || CanHideMouseMoves;
    public bool CanHideShortWaits => Actions.Any(IsShortWaitAction);
    public bool ShowHideShortWaitsToggle => HideShortWaits || CanHideShortWaits;
    public bool CanSimplifyMovement => Actions
        .Select((_, index) => TryGetCondensibleRun(index))
        .Any(run => run != null);
    public bool ShowSimplifyMovementToggle => SimplifyMovement || CanSimplifyMovement;
    public bool CanDuplicateSelectedActions => HasSelectedActions;
    public bool CanMoveSelectedActionsUp => HasSelectedActions && SelectedActionUnderlyingIndices.Min() > 0;
    public bool CanMoveSelectedActionsDown => HasSelectedActions && SelectedActionUnderlyingIndices.Max() < Actions.Count - 1;

    public bool BatchDelayUseRandomDelay
    {
        get => GetSelectedDelayActions().FirstOrDefault()?.UseRandomDelay ?? false;
        set => ApplyToSelectedDelayActions(
            nameof(EditorAction.UseRandomDelay),
            action => action.UseRandomDelay != value,
            action => action.UseRandomDelay = value);
    }

    public int BatchDelayMs
    {
        get => GetSelectedDelayActions().FirstOrDefault()?.DelayMs ?? 0;
        set => ApplyToSelectedDelayActions(
            nameof(EditorAction.DelayMs),
            action => action.DelayMs != value,
            action => action.DelayMs = value);
    }

    public int BatchRandomDelayMinMs
    {
        get => GetSelectedDelayActions().FirstOrDefault()?.RandomDelayMinMs ?? 0;
        set => ApplyToSelectedDelayActions(
            nameof(EditorAction.RandomDelayMinMs),
            action => action.RandomDelayMinMs != value,
            action => action.RandomDelayMinMs = value);
    }

    public int BatchRandomDelayMaxMs
    {
        get => GetSelectedDelayActions().FirstOrDefault()?.RandomDelayMaxMs ?? 0;
        set => ApplyToSelectedDelayActions(
            nameof(EditorAction.RandomDelayMaxMs),
            action => action.RandomDelayMaxMs != value,
            action => action.RandomDelayMaxMs = value);
    }

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
            _usesDefaultMacroName = false;
            OnPropertyChanged();
        }
    }

    public Guid? LinkedLoadedMacroSessionId => _linkedLoadedMacroSessionId;

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
            if (!_isApplyingStatusKind)
            {
                _statusKind = EditorStatusKind.Other;
            }
            OnPropertyChanged();
            StatusChanged?.Invoke(this, value);
        }
    }

    public void TrackLoadedMacroSession(Guid sessionId)
    {
        _linkedLoadedMacroSessionId = sessionId;
    }

    public void ClearLoadedMacroSessionLink()
    {
        _linkedLoadedMacroSessionId = null;
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
            OnPropertyChanged(nameof(ShowConditionLeftColorPicker));
            OnPropertyChanged(nameof(ShowConditionRightColorPicker));
        }
    }

    public bool CanUndo => _undoStack.Count > 0;
    public bool CanRedo => _redoStack.Count > 0;
    public bool HasActions => Actions.Count > 0;
    public bool HasLoadWarnings => LoadWarnings.Count > 0;
    public int HiddenEventCount { get; private set; }
    public bool HasHiddenEvents => HiddenEventCount > 0;

    public bool HideMouseMoves
    {
        get => _hideMouseMoves;
        set
        {
            if (_hideMouseMoves == value)
            {
                return;
            }

            _hideMouseMoves = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ShowHideMouseMovesToggle));
            OnPropertyChanged(nameof(ShowDeleteHiddenEvents));
            UpdateActionListPresentation();
        }
    }

    public bool HideShortWaits
    {
        get => _hideShortWaits;
        set
        {
            if (_hideShortWaits == value)
            {
                return;
            }

            _hideShortWaits = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ShowHideShortWaitsToggle));
            OnPropertyChanged(nameof(ShowDeleteHiddenEvents));
            UpdateActionListPresentation();
        }
    }

    public bool SimplifyMovement
    {
        get => _simplifyMovement;
        set
        {
            if (_simplifyMovement == value)
            {
                return;
            }

            _simplifyMovement = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ShowSimplifyMovementToggle));
            UpdateActionListPresentation();
        }
    }

    public bool SkipInitialZeroZero
    {
        get => _skipInitialZeroZero;
        set
        {
            var normalized = RequiresSkipInitialZeroZero ? true : value;
            if (_skipInitialZeroZero == normalized)
            {
                return;
            }

            _skipInitialZeroZero = normalized;
            if (!RequiresSkipInitialZeroZero && !_skipInitialZeroZeroForcedByCurrentPosition)
            {
                _skipInitialZeroZeroBeforeCurrentPositionForce = normalized;
            }
            OnPropertyChanged();
        }
    }

    public bool RequiresSkipInitialZeroZero => Actions.Any(IsCurrentPositionMouseButtonAction);
    public bool CanEditSkipInitialZeroZero => !RequiresSkipInitialZeroZero;

    public IEnumerable<EditorActionType> ActionTypes => Enum.GetValues<EditorActionType>();
    public IReadOnlyList<EditorActionType> AddableActionTypes => EditorAddableActionTypes;

    public string FormatActionType(EditorActionType actionType) => _actionDisplayFormatter.FormatActionType(actionType);
    public IEnumerable<MouseButton> MouseButtons => Enum.GetValues<MouseButton>().Where(button => button != MouseButton.None);
    public IEnumerable<ScriptValueType> ScriptValueTypes => Enum.GetValues<ScriptValueType>();
    public IEnumerable<ScriptNumericSourceType> ScriptNumericSourceTypes => Enum.GetValues<ScriptNumericSourceType>();
    public IEnumerable<ScriptOperandType> ScriptOperandTypes => Enum.GetValues<ScriptOperandType>();
    #endregion

    #region Visibility Properties

    /// <summary>
    /// Show coordinates for: MouseMove, MouseClick, MouseDown, MouseUp.
    /// </summary>
    public bool ShowCoordinates => SelectedAction != null
        && UsesCoordinateFields(SelectedAction.Type)
        && !IsCurrentPositionMouseButtonAction(SelectedAction);

    /// <summary>
    /// Show Absolute/Relative toggle for all coordinate-bearing mouse actions.
    /// </summary>
    public bool ShowCoordModeToggle => SelectedAction != null
        && UsesCoordinateFields(SelectedAction.Type)
        && !IsCurrentPositionMouseButtonAction(SelectedAction);

    /// <summary>
    /// Show current-position toggle for mouse button actions.
    /// </summary>
    public bool ShowCurrentPositionToggle => SelectedAction?.Type is
        EditorActionType.MouseClick or
        EditorActionType.MouseDown or
        EditorActionType.MouseUp;
    public string CurrentPositionToggleLabel => SelectedAction?.Type switch
    {
        EditorActionType.MouseClick => Localize("Editor_CurrentPositionClick"),
        EditorActionType.MouseDown => Localize("Editor_CurrentPositionHold"),
        EditorActionType.MouseUp => Localize("Editor_CurrentPositionRelease"),
        _ => Localize("Editor_CurrentPositionUse")
    };

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
    /// Show text payload field for TextInput and RawScriptStep.
    /// </summary>
    public bool ShowTextInput => SelectedAction?.Type is EditorActionType.TextInput or EditorActionType.RawScriptStep;
    public string SelectedActionDisplayText
    {
        get => SelectedAction?.Text ?? string.Empty;
        set
        {
            if (SelectedAction == null)
            {
                return;
            }

            var text = value;
            if (SelectedAction.Text == text)
            {
                return;
            }

            SelectedAction.Text = text;
        }
    }
    public bool ShowSetVariableFields => SelectedAction?.Type == EditorActionType.SetVariable;
    public bool ShowIncDecFields => SelectedAction?.Type is EditorActionType.IncrementVariable or EditorActionType.DecrementVariable;
    public bool ShowRepeatFields => SelectedAction?.Type == EditorActionType.RepeatBlockStart;
    public bool ShowConditionFields => SelectedAction?.Type is EditorActionType.IfBlockStart or EditorActionType.WhileBlockStart;
    public bool ShowForFields => SelectedAction?.Type == EditorActionType.ForBlockStart;
    public bool ShowForStepFields => ShowForFields && SelectedAction?.ForHasStep == true;
    public bool CanInsertElseBlock => CanInsertElseForSelection();
    public bool CanRemoveBlock => CanRemoveSelectedBlock();

    public string TextInputLabel => SelectedAction?.Type == EditorActionType.RawScriptStep
        ? Localize("Editor_RawScriptStep")
        : Localize("Editor_TextToType");
    public string TextInputWatermark => SelectedAction?.Type == EditorActionType.RawScriptStep
        ? Localize("Editor_OriginalScriptLine")
        : Localize("Editor_EnterTextToType");
    public string TextInputHint => SelectedAction?.Type == EditorActionType.RawScriptStep
        ? Localize("Editor_RawScriptHint")
        : Localize("Editor_TextToTypeHint");

    public bool TextInputAcceptsReturn => SelectedAction?.Type is EditorActionType.TextInput or EditorActionType.RawScriptStep;

    #endregion

    private static bool UsesCoordinateFields(EditorActionType actionType)
    {
        return actionType is
            EditorActionType.MouseMove or
            EditorActionType.MouseClick or
            EditorActionType.MouseDown or
            EditorActionType.MouseUp;
    }

    private static bool IsUserAddableActionType(EditorActionType actionType)
    {
        return actionType is not (EditorActionType.BlockEnd or EditorActionType.ElseBlockStart or EditorActionType.RawScriptStep);
    }

    private static bool IsAutoManagedBlockStartAction(EditorActionType actionType)
    {
        return actionType is
            EditorActionType.RepeatBlockStart
            or EditorActionType.IfBlockStart
            or EditorActionType.WhileBlockStart
            or EditorActionType.ForBlockStart;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        foreach (var action in _subscribedActions)
        {
            action.PropertyChanged -= OnAnyActionPropertyChanged;
        }

        _subscribedActions.Clear();
        Actions.CollectionChanged -= OnActionsCollectionChanged;
        SelectedActionUnderlyingIndices.CollectionChanged -= OnSelectedActionUnderlyingIndicesChanged;
        LoadWarnings.CollectionChanged -= OnLoadWarningsCollectionChanged;
        _localizationService.CultureChanged -= OnCultureChanged;
        _captureService.CancelCapture();
    }

    private void OnCultureChanged(object? sender, EventArgs e)
    {
        if (_usesDefaultMacroName)
        {
            _macroName = Localize("Editor_DefaultMacroName");
            OnPropertyChanged(nameof(MacroName));
        }

        if (_statusKind == EditorStatusKind.Ready)
        {
            SetStatusKind(EditorStatusKind.Ready);
        }

        UpdateActionListPresentation();
        OnPropertyChanged(nameof(CurrentPositionToggleLabel));
        OnPropertyChanged(nameof(TextInputLabel));
        OnPropertyChanged(nameof(TextInputWatermark));
        OnPropertyChanged(nameof(TextInputHint));
        OnPropertyChanged(nameof(AddableActionTypes));
        OnPropertyChanged(nameof(ActionTypes));
        OnPropertyChanged(nameof(SelectedAction));
        OnPropertyChanged(nameof(SelectedActionListItem));
        OnPropertyChanged(nameof(CanInsertElseBlock));
        OnPropertyChanged(nameof(CanRemoveBlock));
        OnPropertyChanged(nameof(ConditionRightOperandHint));
        NotifyScreenReadingComputedPropertiesChanged();
    }

    private void SetStatusKind(EditorStatusKind statusKind)
    {
        _statusKind = statusKind;
        _isApplyingStatusKind = true;
        try
        {
            Status = BuildStatus(statusKind);
        }
        finally
        {
            _isApplyingStatusKind = false;
        }
    }

    private string BuildStatus(EditorStatusKind statusKind)
    {
        return statusKind switch
        {
            EditorStatusKind.Ready => Localize("Editor_StatusReady"),
            _ => _status
        };
    }

    private static bool IsCurrentPositionMouseButtonAction(EditorAction? action)
    {
        return action?.Type is EditorActionType.MouseClick or EditorActionType.MouseDown or EditorActionType.MouseUp
            && action.UseCurrentPosition;
    }

    private bool CanRemoveSelectedBlock()
    {
        return SelectedAction != null
            && Actions.IndexOf(SelectedAction) >= 0
            && (IsScriptBlockStartAction(SelectedAction.Type) || SelectedAction.Type == EditorActionType.BlockEnd);
    }

    private bool CanInsertElseForSelection()
    {
        if (SelectedAction?.Type != EditorActionType.IfBlockStart)
        {
            return false;
        }

        var ifIndex = Actions.IndexOf(SelectedAction);
        if (ifIndex < 0 || !TryFindMatchingBlockEnd(ifIndex, out var blockEndIndex))
        {
            return false;
        }

        return blockEndIndex + 1 >= Actions.Count
            || Actions[blockEndIndex + 1].Type != EditorActionType.ElseBlockStart;
    }

    private void OnActionsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            foreach (var action in _subscribedActions)
            {
                action.PropertyChanged -= OnAnyActionPropertyChanged;
            }

            _subscribedActions.Clear();

            foreach (var action in Actions)
            {
                action.PropertyChanged += OnAnyActionPropertyChanged;
                _subscribedActions.Add(action);
            }
        }
        else
        {
            if (e.OldItems != null)
            {
                foreach (var item in e.OldItems.OfType<EditorAction>())
                {
                    if (_subscribedActions.Remove(item))
                    {
                        item.PropertyChanged -= OnAnyActionPropertyChanged;
                    }
                }
            }

            if (e.NewItems != null)
            {
                foreach (var item in e.NewItems.OfType<EditorAction>())
                {
                    if (_subscribedActions.Add(item))
                    {
                        item.PropertyChanged += OnAnyActionPropertyChanged;
                    }
                }
            }
        }

        UpdateActionIndices();
        UpdateActionListPresentation();
        RefreshCurrentPositionConfiguration();
        RefreshAvailableVariableNames();
        OnPropertyChanged(nameof(CanRemoveBlock));
        OnPropertyChanged(nameof(CanDeleteHiddenEvents));
        OnPropertyChanged(nameof(ShowDeleteHiddenEvents));
        NotifyFilterToggleAvailabilityChanged();
        NormalizeSelectedUnderlyingIndices();
        NotifySelectedActionsChanged();
    }

    private void OnSelectedActionUnderlyingIndicesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_isSynchronizingSelectedUnderlyingIndices)
        {
            return;
        }

        NormalizeSelectedUnderlyingIndices();
        SelectPrimaryActionFromUnderlyingSelection();
        NotifySelectedActionsChanged();
    }

    public void ReplaceSelectedActionUnderlyingIndices(IEnumerable<int> underlyingIndices)
    {
        ArgumentNullException.ThrowIfNull(underlyingIndices);

        var normalized = underlyingIndices
            .Where(index => index >= 0 && index < Actions.Count)
            .Distinct()
            .OrderBy(index => index)
            .ToArray();

        if (SelectedActionUnderlyingIndices.SequenceEqual(normalized))
        {
            return;
        }

        _isSynchronizingSelectedUnderlyingIndices = true;
        try
        {
            SelectedActionUnderlyingIndices.Clear();
            foreach (var index in normalized)
            {
                SelectedActionUnderlyingIndices.Add(index);
            }
        }
        finally
        {
            _isSynchronizingSelectedUnderlyingIndices = false;
        }

        SelectPrimaryActionFromUnderlyingSelection();
        NotifySelectedActionsChanged();
    }

    private void OnAnyActionPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(EditorAction.Index))
        {
            UpdateActionListPresentation();
        }

        if (e.PropertyName == nameof(EditorAction.Type))
        {
            OnPropertyChanged(nameof(CanRemoveBlock));
            OnPropertyChanged(nameof(CanDeleteHiddenEvents));
            OnPropertyChanged(nameof(ShowDeleteHiddenEvents));
            NotifyFilterToggleAvailabilityChanged();
        }

        if (e.PropertyName == nameof(EditorAction.DelayMs) || e.PropertyName == nameof(EditorAction.UseRandomDelay))
        {
            OnPropertyChanged(nameof(CanDeleteHiddenEvents));
            OnPropertyChanged(nameof(ShowDeleteHiddenEvents));
            NotifyFilterToggleAvailabilityChanged();
        }

        if (e.PropertyName is not (
            nameof(EditorAction.Type)
            or nameof(EditorAction.Text)
            or nameof(EditorAction.ScriptVariableName)
            or nameof(EditorAction.ForVariableName)
            or nameof(EditorAction.ScriptValue)
            or nameof(EditorAction.ScriptNumericValue)
            or nameof(EditorAction.ScriptLeftOperand)
            or nameof(EditorAction.ScriptRightOperand)
            or nameof(EditorAction.ForStartValue)
            or nameof(EditorAction.ForEndValue)
            or nameof(EditorAction.ForStepValue)
            or nameof(EditorAction.ScreenColorVariableName)
            or nameof(EditorAction.ScreenFoundVariableName)
            or nameof(EditorAction.ScreenFoundXVariableName)
            or nameof(EditorAction.ScreenFoundYVariableName)))
        {
            NotifyScreenReadingComputedPropertiesChanged();
            return;
        }

        RefreshAvailableVariableNames();
        NotifyScreenReadingComputedPropertiesChanged();
    }

    private void OnLoadWarningsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(HasLoadWarnings));
    }

    private void SetLoadWarnings(IEnumerable<EditorActionRestoreWarning> warnings)
    {
        LoadWarnings.Clear();
        foreach (var warning in warnings)
        {
            var stepPreview = warning.Step.Length > 120
                ? warning.Step[..120] + "..."
                : warning.Step;
            LoadWarnings.Add($"Step {warning.StepIndex}: {warning.Message} ({stepPreview})");
        }
    }

    private string Localize(string key)
    {
        return _localizationService[key];
    }

    private void UpdateActionListPresentation()
    {
        var previousSelectionSyncFlag = _isSelectingFromActionList;
        _isSelectingFromActionList = true;
        try
        {
            ActionListItems.Clear();

            var depth = 0;
            var blockStack = new Stack<EditorActionType>();

            var hiddenEventCount = 0;
            for (var index = 0; index < Actions.Count; index++)
            {
                var action = Actions[index];
                var isInsideDrag = IsInsideMouseDrag(index);
                var isLowImportance = IsLowImportanceEditorEvent(action, isInsideDrag);
                if (IsHiddenByActiveFilters(action, isInsideDrag))
                {
                    hiddenEventCount++;
                    continue;
                }

                var condensedRun = SimplifyMovement
                    ? TryGetCondensibleRun(index)
                    : null;
                if (condensedRun != null)
                {
                    var representativeAction = Actions[condensedRun.RepresentativeIndex];
                    var representativeIsLowImportance = IsLowImportanceEditorEvent(representativeAction, isInsideDrag: false);
                    var representativeDisplayName = _actionDisplayFormatter.Format(representativeAction);

                    ActionListItems.Add(CreateActionListItem(
                        representativeAction,
                        condensedRun.RepresentativeIndex,
                        depth,
                        representativeDisplayName,
                        representativeIsLowImportance,
                        condensedRun.HiddenCount));
                    index = condensedRun.EndIndex;
                    continue;
                }

                if (action.Type == EditorActionType.BlockEnd)
                {
                    if (depth > 0)
                    {
                        depth--;
                    }

                    var displayName = blockStack.Count > 0
                        ? $"End {_actionDisplayFormatter.FormatBlockName(blockStack.Pop())}"
                        : Localize("Editor_Action_EndBlockShort");

                    ActionListItems.Add(CreateActionListItem(action, index, depth, displayName, isLowImportance, condensedHiddenCount: 0));
                    continue;
                }

                var rowDisplayName = _actionDisplayFormatter.Format(action);

                ActionListItems.Add(CreateActionListItem(action, index, depth, rowDisplayName, isLowImportance, condensedHiddenCount: 0));

                if (IsScriptBlockStartAction(action.Type))
                {
                    blockStack.Push(action.Type);
                    depth++;
                }
            }

            if (HiddenEventCount != hiddenEventCount)
            {
                HiddenEventCount = hiddenEventCount;
                OnPropertyChanged(nameof(HiddenEventCount));
                OnPropertyChanged(nameof(HasHiddenEvents));
            }

            NormalizeSelectedUnderlyingIndices();
            if (SelectedActionUnderlyingIndices.Count > 0)
            {
                SelectPrimaryActionFromUnderlyingSelection();
            }
            else
            {
                SyncSelectedActionListItem();
            }

            NotifySelectedActionsChanged();
        }
        finally
        {
            _isSelectingFromActionList = previousSelectionSyncFlag;
        }
    }

    private sealed record CondensibleRun(int EndIndex, int RepresentativeIndex, int HiddenCount);

    private CondensibleRun? TryGetCondensibleRun(int startIndex)
    {
        if (IsInsideMouseDrag(startIndex) || !IsMovementSimplificationCandidate(Actions[startIndex]))
        {
            return null;
        }

        var endIndex = startIndex;
        var representativeIndex = startIndex;
        var lastMouseMoveIndex = -1;

        for (var index = startIndex; index < Actions.Count; index++)
        {
            var action = Actions[index];
            if (IsInsideMouseDrag(index) || !IsMovementSimplificationCandidate(action))
            {
                break;
            }

            endIndex = index;
            representativeIndex = index;
            if (action.Type == EditorActionType.MouseMove)
            {
                lastMouseMoveIndex = index;
            }
        }

        var runLength = endIndex - startIndex + 1;
        if (runLength < 6)
        {
            return null;
        }

        if (lastMouseMoveIndex >= startIndex)
        {
            representativeIndex = lastMouseMoveIndex;
        }

        return new CondensibleRun(
            endIndex,
            representativeIndex,
            runLength - 1);
    }

    private EditorActionListItem CreateActionListItem(
        EditorAction action,
        int underlyingIndex,
        int indentLevel,
        string displayName,
        bool isNoise,
        int condensedHiddenCount)
    {
        var visualKind = GetActionVisualKind(action, isNoise);

        var condensedHint = condensedHiddenCount > 0
            ? string.Format(
                _localizationService.CurrentCulture,
                Localize("Editor_SimplifiedMovementHint"),
                condensedHiddenCount)
            : string.Empty;

        return new EditorActionListItem(
            action,
            action.Index,
            underlyingIndex,
            indentLevel,
            displayName,
            condensedHint,
            visualKind,
            IsImportantAction(action, isNoise),
            IsCleanupEligibleAction(action, isNoise),
            condensedHiddenCount,
            representsSourceAction: true,
            isNoise);
    }

    private static EditorActionVisualKind GetActionVisualKind(EditorAction action, bool isNoise)
    {
        return action.Type switch
        {
            EditorActionType.Delay when isNoise => EditorActionVisualKind.Noise,
            EditorActionType.MouseMove => EditorActionVisualKind.Movement,
            EditorActionType.MouseClick
                or EditorActionType.MouseDown
                or EditorActionType.MouseUp
                or EditorActionType.ScrollVertical
                or EditorActionType.ScrollHorizontal => EditorActionVisualKind.Pointer,
            EditorActionType.KeyPress
                or EditorActionType.KeyDown
                or EditorActionType.KeyUp => EditorActionVisualKind.Keyboard,
            EditorActionType.TextInput => EditorActionVisualKind.Text,
            EditorActionType.Delay => EditorActionVisualKind.Timing,
            EditorActionType.SetVariable
                or EditorActionType.IncrementVariable
                or EditorActionType.DecrementVariable => EditorActionVisualKind.Variable,
            EditorActionType.PixelColor
                or EditorActionType.WaitColor
                or EditorActionType.PixelSearch => EditorActionVisualKind.Raw,
            EditorActionType.RepeatBlockStart
                or EditorActionType.IfBlockStart
                or EditorActionType.ElseBlockStart
                or EditorActionType.WhileBlockStart
                or EditorActionType.ForBlockStart
                or EditorActionType.BlockEnd
                or EditorActionType.Break
                or EditorActionType.Continue => EditorActionVisualKind.ControlFlow,
            EditorActionType.RawScriptStep => EditorActionVisualKind.Raw,
            _ => EditorActionVisualKind.Raw
        };
    }

    private static bool IsImportantAction(EditorAction action, bool isNoise)
    {
        if (isNoise)
        {
            return false;
        }

        return action.Type switch
        {
            EditorActionType.MouseMove => false,
            EditorActionType.Delay when !action.UseRandomDelay && action.DelayMs == 0 => false,
            EditorActionType.Delay => true,
            _ => true
        };
    }

    private static bool IsCleanupEligibleAction(EditorAction action, bool isNoise)
    {
        return isNoise && (action.Type == EditorActionType.MouseMove || action.Type == EditorActionType.Delay);
    }

    private void SyncSelectedActionListItem()
    {
        var selectedRow = _selectedAction == null
            ? null
            : ActionListItems.FirstOrDefault(item => ReferenceEquals(item.Action, _selectedAction));
        if (ReferenceEquals(_selectedActionListItem, selectedRow))
        {
            return;
        }

        _selectedActionListItem = selectedRow;
        OnPropertyChanged(nameof(SelectedActionListItem));
    }

    private void SyncSelectedUnderlyingIndicesToPrimarySelection()
    {
        _isSynchronizingSelectedUnderlyingIndices = true;
        try
        {
            SelectedActionUnderlyingIndices.Clear();

            var selectedIndex = SelectedAction == null
                ? -1
                : Actions.IndexOf(SelectedAction);
            if (selectedIndex >= 0)
            {
                SelectedActionUnderlyingIndices.Add(selectedIndex);
            }
        }
        finally
        {
            _isSynchronizingSelectedUnderlyingIndices = false;
        }

        NotifySelectedActionsChanged();
    }

    private void NormalizeSelectedUnderlyingIndices()
    {
        if (_isSynchronizingSelectedUnderlyingIndices)
        {
            return;
        }

        var normalized = SelectedActionUnderlyingIndices
            .Where(index => index >= 0 && index < Actions.Count)
            .Distinct()
            .OrderBy(index => index)
            .ToArray();

        if (SelectedActionUnderlyingIndices.SequenceEqual(normalized))
        {
            return;
        }

        _isSynchronizingSelectedUnderlyingIndices = true;
        try
        {
            SelectedActionUnderlyingIndices.Clear();
            foreach (var index in normalized)
            {
                SelectedActionUnderlyingIndices.Add(index);
            }
        }
        finally
        {
            _isSynchronizingSelectedUnderlyingIndices = false;
        }
    }

    private void SelectPrimaryActionFromUnderlyingSelection()
    {
        var selectedIndexSet = SelectedActionUnderlyingIndices.ToHashSet();
        var selectedRow = ActionListItems
            .Where(item => item.RepresentsSourceAction && selectedIndexSet.Contains(item.UnderlyingIndex))
            .OrderBy(item => item.UnderlyingIndex)
            .FirstOrDefault();

        _isSelectingFromActionList = true;
        try
        {
            SelectedActionListItem = selectedRow;
            if (!ReferenceEquals(SelectedAction, selectedRow?.Action))
            {
                SelectedAction = selectedRow?.Action;
            }
        }
        finally
        {
            _isSelectingFromActionList = false;
        }
    }

    private void NotifySelectedActionsChanged()
    {
        OnPropertyChanged(nameof(HasSelectedActions));
        OnPropertyChanged(nameof(SelectedActionCount));
        OnPropertyChanged(nameof(ShowSingleSelectedActionProperties));
        OnPropertyChanged(nameof(ShowBatchDelayProperties));
        OnPropertyChanged(nameof(ShowMultiSelectionPropertiesHint));
        OnPropertyChanged(nameof(ShowBatchFixedDelayInput));
        OnPropertyChanged(nameof(ShowBatchRandomDelayOptions));
        OnPropertyChanged(nameof(BatchDelayUseRandomDelay));
        OnPropertyChanged(nameof(BatchDelayMs));
        OnPropertyChanged(nameof(BatchRandomDelayMinMs));
        OnPropertyChanged(nameof(BatchRandomDelayMaxMs));
        OnPropertyChanged(nameof(CanRemoveSelectedActions));
        OnPropertyChanged(nameof(CanDeleteHiddenEvents));
        OnPropertyChanged(nameof(ShowDeleteHiddenEvents));
        OnPropertyChanged(nameof(CanDuplicateSelectedActions));
        OnPropertyChanged(nameof(CanMoveSelectedActionsUp));
        OnPropertyChanged(nameof(CanMoveSelectedActionsDown));
    }

    private void NotifyFilterToggleAvailabilityChanged()
    {
        OnPropertyChanged(nameof(CanHideMouseMoves));
        OnPropertyChanged(nameof(ShowHideMouseMovesToggle));
        OnPropertyChanged(nameof(CanHideShortWaits));
        OnPropertyChanged(nameof(ShowHideShortWaitsToggle));
        OnPropertyChanged(nameof(CanSimplifyMovement));
        OnPropertyChanged(nameof(ShowSimplifyMovementToggle));
    }

    private IReadOnlyList<EditorAction> GetSelectedActions()
    {
        return SelectedActionUnderlyingIndices
            .Where(index => index >= 0 && index < Actions.Count)
            .Distinct()
            .OrderBy(index => index)
            .Select(index => Actions[index])
            .ToArray();
    }

    private IReadOnlyList<EditorAction> GetSelectedDelayActions()
    {
        return GetSelectedActions()
            .Where(action => action.Type == EditorActionType.Delay)
            .ToArray();
    }

    private void ApplyToSelectedDelayActions(string propertyName, Func<EditorAction, bool> shouldUpdate, Action<EditorAction> update)
    {
        var actions = GetSelectedDelayActions();
        if (actions.Count == 0 || !actions.Any(shouldUpdate))
        {
            return;
        }

        SaveUndoState();
        _isSynchronizingActionProperties = true;
        try
        {
            foreach (var action in actions)
            {
                update(action);
            }
        }
        finally
        {
            _isSynchronizingActionProperties = false;
        }

        UpdateActionListPresentation();
        NotifyVisibilityChanged();
        NotifySelectedActionsChanged();
        ResetPropertyEditUndoCoalescing();
        RememberCurrentState();

        OnPropertyChanged(propertyName switch
        {
            nameof(EditorAction.UseRandomDelay) => nameof(BatchDelayUseRandomDelay),
            nameof(EditorAction.DelayMs) => nameof(BatchDelayMs),
            nameof(EditorAction.RandomDelayMinMs) => nameof(BatchRandomDelayMinMs),
            nameof(EditorAction.RandomDelayMaxMs) => nameof(BatchRandomDelayMaxMs),
            _ => string.Empty
        });
    }

    private bool IsHiddenByActiveFilters(EditorAction action, bool isInsideDrag)
    {
        return (HideMouseMoves && action.Type == EditorActionType.MouseMove)
            || (HideShortWaits && IsShortWaitAction(action));
    }

    private static bool IsLowImportanceEditorEvent(EditorAction action, bool isInsideDrag)
    {
        return (!isInsideDrag && action.Type == EditorActionType.MouseMove)
            || IsShortWaitAction(action);
    }

    private static bool IsMovementSimplificationCandidate(EditorAction action)
    {
        return action.Type == EditorActionType.MouseMove
            || IsShortWaitAction(action);
    }

    private static bool IsShortWaitAction(EditorAction action)
    {
        return action is { Type: EditorActionType.Delay, UseRandomDelay: false, DelayMs: > 0 and < 10 };
    }

    private bool IsInsideMouseDrag(int actionIndex)
    {
        var isDragging = false;
        for (var index = 0; index < actionIndex && index < Actions.Count; index++)
        {
            switch (Actions[index].Type)
            {
                case EditorActionType.MouseDown:
                    isDragging = true;
                    break;
                case EditorActionType.MouseUp:
                case EditorActionType.MouseClick:
                    isDragging = false;
                    break;
            }
        }

        return isDragging;
    }

}
