using System;
using System.Collections.Specialized;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text.RegularExpressions;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services;
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
    private bool _disposed;
    private bool _usesDefaultMacroName = true;
    private bool _isApplyingStatusKind;
    private EditorStatusKind _statusKind = EditorStatusKind.Ready;
    private List<EditorAction> _lastKnownState = new();
    private IReadOnlyList<string> _availableVariableNames = Array.Empty<string>();
    private string? _selectedSetVariableSuggestion;
    private string? _selectedIncDecVariableSuggestion;
    private string? _selectedConditionLeftVariableSuggestion;
    private string? _selectedConditionRightVariableSuggestion;
    private string? _selectedForVariableSuggestion;
    private Guid? _linkedLoadedMacroSessionId;
    private DateTimeOffset _lastPropertyEditUndoAt = DateTimeOffset.MinValue;
    private EditorAction? _lastPropertyEditAction;
    private string? _lastPropertyEditName;
    private readonly HashSet<EditorAction> _subscribedActions = new();
    private static readonly IReadOnlyList<EditorActionType> EditorAddableActionTypes = Enum
        .GetValues<EditorActionType>()
        .Where(IsUserAddableActionType)
        .ToArray();

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
        EditorActionDisplayFormatter? actionDisplayFormatter = null)
    {
        _converter = converter ?? throw new ArgumentNullException(nameof(converter));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _captureService = captureService ?? throw new ArgumentNullException(nameof(captureService));
        _fileManager = fileManager ?? throw new ArgumentNullException(nameof(fileManager));
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
        _keyCodeMapper = keyCodeMapper ?? throw new ArgumentNullException(nameof(keyCodeMapper));
        _localizationService = localizationService ?? new LocalizationService();
        _actionDisplayFormatter = actionDisplayFormatter ?? new EditorActionDisplayFormatter(_localizationService);
        _macroName = _localizationService["Editor_DefaultMacroName"];
        _status = BuildStatus(EditorStatusKind.Ready);

        Actions = new ObservableCollection<EditorAction>();
        ActionListItems = new ObservableCollection<EditorActionListItem>();
        LoadWarnings = new ObservableCollection<string>();
        Actions.CollectionChanged += OnActionsCollectionChanged;
        LoadWarnings.CollectionChanged += OnLoadWarningsCollectionChanged;
        _localizationService.CultureChanged += OnCultureChanged;
        RefreshAvailableVariableNames();
        RememberCurrentState();
    }

    #region Properties

    public ObservableCollection<EditorAction> Actions { get; }

    public ObservableCollection<EditorActionListItem> ActionListItems { get; }

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
            }

            OnPropertyChanged();
            OnPropertyChanged(nameof(HasSelectedAction));
            NotifyVisibilityChanged();
            ResetPropertyEditUndoCoalescing();
            SyncSelectedActionListItem();
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
        }
    }

    public bool CanUndo => _undoStack.Count > 0;
    public bool CanRedo => _redoStack.Count > 0;
    public bool HasActions => Actions.Count > 0;
    public bool HasLoadWarnings => LoadWarnings.Count > 0;
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
    public IEnumerable<ScriptConditionOperator> ScriptConditionOperators => Enum.GetValues<ScriptConditionOperator>();
    public IReadOnlyList<string> AvailableVariableNames => _availableVariableNames;
    public bool HasAvailableVariableNames => AvailableVariableNames.Count > 0;

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
    public bool ShowSetVariableFields => SelectedAction?.Type == EditorActionType.SetVariable;
    public bool ShowIncDecFields => SelectedAction?.Type is EditorActionType.IncrementVariable or EditorActionType.DecrementVariable;
    public bool ShowRepeatFields => SelectedAction?.Type == EditorActionType.RepeatBlockStart;
    public bool ShowConditionFields => SelectedAction?.Type is EditorActionType.IfBlockStart or EditorActionType.WhileBlockStart;
    public bool ShowForFields => SelectedAction?.Type == EditorActionType.ForBlockStart;
    public bool ShowForStepFields => ShowForFields && SelectedAction?.ForHasStep == true;
    public bool ShowSetVariablePicker => ShowSetVariableFields && HasAvailableVariableNames;
    public bool ShowIncDecVariablePicker => ShowIncDecFields && HasAvailableVariableNames;
    public bool ShowConditionLeftVariablePicker =>
        ShowConditionFields
        && HasAvailableVariableNames
        && SelectedAction?.ScriptLeftOperandType == ScriptOperandType.VariableReference;
    public bool ShowConditionRightVariablePicker =>
        ShowConditionFields
        && HasAvailableVariableNames
        && SelectedAction?.ScriptRightOperandType == ScriptOperandType.VariableReference;
    public bool ShowForVariablePicker => ShowForFields && HasAvailableVariableNames;
    public bool CanInsertElseBlock => CanInsertElseForSelection();
    public bool CanRemoveBlock => CanRemoveSelectedBlock();

    public string? SelectedSetVariableSuggestion
    {
        get => _selectedSetVariableSuggestion;
        set => ApplyVariableSuggestion(ref _selectedSetVariableSuggestion, value, nameof(SelectedSetVariableSuggestion), suggestion =>
        {
            if (SelectedAction?.Type == EditorActionType.SetVariable)
            {
                SelectedAction.ScriptVariableName = suggestion;
            }
        });
    }

    public string? SelectedIncDecVariableSuggestion
    {
        get => _selectedIncDecVariableSuggestion;
        set => ApplyVariableSuggestion(ref _selectedIncDecVariableSuggestion, value, nameof(SelectedIncDecVariableSuggestion), suggestion =>
        {
            if (SelectedAction?.Type is EditorActionType.IncrementVariable or EditorActionType.DecrementVariable)
            {
                SelectedAction.ScriptVariableName = suggestion;
            }
        });
    }

    public string? SelectedConditionLeftVariableSuggestion
    {
        get => _selectedConditionLeftVariableSuggestion;
        set => ApplyVariableSuggestion(ref _selectedConditionLeftVariableSuggestion, value, nameof(SelectedConditionLeftVariableSuggestion), suggestion =>
        {
            if (SelectedAction?.Type is EditorActionType.IfBlockStart or EditorActionType.WhileBlockStart
                && SelectedAction.ScriptLeftOperandType == ScriptOperandType.VariableReference)
            {
                SelectedAction.ScriptLeftOperand = suggestion;
            }
        });
    }

    public string? SelectedConditionRightVariableSuggestion
    {
        get => _selectedConditionRightVariableSuggestion;
        set => ApplyVariableSuggestion(ref _selectedConditionRightVariableSuggestion, value, nameof(SelectedConditionRightVariableSuggestion), suggestion =>
        {
            if (SelectedAction?.Type is EditorActionType.IfBlockStart or EditorActionType.WhileBlockStart
                && SelectedAction.ScriptRightOperandType == ScriptOperandType.VariableReference)
            {
                SelectedAction.ScriptRightOperand = suggestion;
            }
        });
    }

    public string? SelectedForVariableSuggestion
    {
        get => _selectedForVariableSuggestion;
        set => ApplyVariableSuggestion(ref _selectedForVariableSuggestion, value, nameof(SelectedForVariableSuggestion), suggestion =>
        {
            if (SelectedAction?.Type == EditorActionType.ForBlockStart)
            {
                SelectedAction.ForVariableName = suggestion;
            }
        });
    }

    public string TextInputLabel => SelectedAction?.Type == EditorActionType.RawScriptStep
        ? Localize("Editor_RawScriptStep")
        : Localize("Editor_TextToType");
    public string TextInputWatermark => SelectedAction?.Type == EditorActionType.RawScriptStep
        ? Localize("Editor_OriginalScriptLine")
        : Localize("Editor_EnterTextToType");
    public string TextInputHint => SelectedAction?.Type == EditorActionType.RawScriptStep
        ? Localize("Editor_RawScriptHint")
        : string.Format(
            _localizationService.CurrentCulture,
            Localize("Editor_TextToTypeHint"),
            EditorActionValidationLimits.MaxTextInputLength);

    public bool TextInputAcceptsReturn => SelectedAction?.Type == EditorActionType.RawScriptStep;

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
            or nameof(EditorAction.ForStepValue)))
        {
            return;
        }

        RefreshAvailableVariableNames();
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

            for (var index = 0; index < Actions.Count; index++)
            {
                var action = Actions[index];
                if (action.Type == EditorActionType.BlockEnd)
                {
                    if (depth > 0)
                    {
                        depth--;
                    }

                    var displayName = blockStack.Count > 0
                        ? $"End {_actionDisplayFormatter.FormatBlockName(blockStack.Pop())}"
                        : Localize("Editor_Action_EndBlockShort");

                    ActionListItems.Add(new EditorActionListItem(
                        action,
                        action.Index,
                        depth,
                        displayName));
                    continue;
                }

                var rowDisplayName = _actionDisplayFormatter.Format(action);

                ActionListItems.Add(new EditorActionListItem(
                    action,
                    action.Index,
                    depth,
                    rowDisplayName));

                if (IsScriptBlockStartAction(action.Type))
                {
                    blockStack.Push(action.Type);
                    depth++;
                }
            }

            SyncSelectedActionListItem();
        }
        finally
        {
            _isSelectingFromActionList = previousSelectionSyncFlag;
        }
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

    private IReadOnlyList<string> BuildAvailableVariableNames()
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < Actions.Count; index++)
        {
            var action = Actions[index];
            switch (action.Type)
            {
                case EditorActionType.SetVariable:
                    AddIfValidVariableName(names, action.ScriptVariableName);
                    if (action.PreferLegacyScriptText)
                    {
                        TryAddLegacySetVariableName(names, action.Text);
                    }
                    break;
                case EditorActionType.ForBlockStart:
                    AddIfValidVariableName(names, action.ForVariableName);
                    break;
            }
        }

        if (SelectedAction != null)
        {
            AddIfValidVariableName(names, SelectedAction.ScriptVariableName);
            AddIfValidVariableName(names, SelectedAction.ForVariableName);
            AddIfValidVariableName(names, SelectedAction.ScriptValue);
            AddIfValidVariableName(names, SelectedAction.ScriptNumericValue);
            AddIfValidVariableName(names, SelectedAction.ScriptLeftOperand);
            AddIfValidVariableName(names, SelectedAction.ScriptRightOperand);
            AddIfValidVariableName(names, SelectedAction.ForStartValue);
            AddIfValidVariableName(names, SelectedAction.ForEndValue);
            AddIfValidVariableName(names, SelectedAction.ForStepValue);
        }

        return names.OrderBy(name => name, StringComparer.Ordinal).ToArray();
    }

    private void RefreshAvailableVariableNames()
    {
        var next = BuildAvailableVariableNames();
        if (_availableVariableNames.SequenceEqual(next, StringComparer.Ordinal))
        {
            OnPropertyChanged(nameof(CanInsertElseBlock));
            OnPropertyChanged(nameof(CanRemoveBlock));
            OnPropertyChanged(nameof(ShowSetVariablePicker));
            OnPropertyChanged(nameof(ShowIncDecVariablePicker));
            OnPropertyChanged(nameof(ShowConditionLeftVariablePicker));
            OnPropertyChanged(nameof(ShowConditionRightVariablePicker));
            OnPropertyChanged(nameof(ShowForVariablePicker));
            ClearVariableSuggestionSelections();
            return;
        }

        _availableVariableNames = next;
        OnPropertyChanged(nameof(AvailableVariableNames));
        OnPropertyChanged(nameof(HasAvailableVariableNames));
        OnPropertyChanged(nameof(CanInsertElseBlock));
        OnPropertyChanged(nameof(CanRemoveBlock));
        OnPropertyChanged(nameof(ShowSetVariablePicker));
        OnPropertyChanged(nameof(ShowIncDecVariablePicker));
        OnPropertyChanged(nameof(ShowConditionLeftVariablePicker));
        OnPropertyChanged(nameof(ShowConditionRightVariablePicker));
        OnPropertyChanged(nameof(ShowForVariablePicker));
        ClearVariableSuggestionSelections();
    }

    private void ClearVariableSuggestionSelections()
    {
        SetSuggestionValue(ref _selectedSetVariableSuggestion, nameof(SelectedSetVariableSuggestion), null);
        SetSuggestionValue(ref _selectedIncDecVariableSuggestion, nameof(SelectedIncDecVariableSuggestion), null);
        SetSuggestionValue(ref _selectedConditionLeftVariableSuggestion, nameof(SelectedConditionLeftVariableSuggestion), null);
        SetSuggestionValue(ref _selectedConditionRightVariableSuggestion, nameof(SelectedConditionRightVariableSuggestion), null);
        SetSuggestionValue(ref _selectedForVariableSuggestion, nameof(SelectedForVariableSuggestion), null);
    }

    private void SetSuggestionValue(ref string? targetField, string propertyName, string? value)
    {
        if (string.Equals(targetField, value, StringComparison.Ordinal))
        {
            return;
        }

        targetField = value;
        OnPropertyChanged(propertyName);
    }

    private void ApplyVariableSuggestion(
        ref string? field,
        string? value,
        string propertyName,
        Action<string> applyAction)
    {
        if (string.Equals(field, value, StringComparison.Ordinal))
        {
            return;
        }

        field = value;
        OnPropertyChanged(propertyName);

        if (_isApplyingVariableSuggestion || string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        _isApplyingVariableSuggestion = true;
        try
        {
            applyAction(value);
            field = null;
            OnPropertyChanged(propertyName);
        }
        finally
        {
            _isApplyingVariableSuggestion = false;
        }
    }

    private static void TryAddLegacySetVariableName(ISet<string> target, string legacyText)
    {
        if (string.IsNullOrWhiteSpace(legacyText))
        {
            return;
        }

        var text = legacyText.Trim();
        var equalIndex = text.IndexOf('=');
        if (equalIndex > 0)
        {
            AddIfValidVariableName(target, text[..equalIndex]);
            return;
        }

        var firstPart = text.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();
        AddIfValidVariableName(target, firstPart ?? string.Empty);
    }

    private static void AddIfValidVariableName(ISet<string> target, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        var token = value.Trim();
        if (token.StartsWith("$", StringComparison.Ordinal))
        {
            token = token[1..];
        }

        if (Regex.IsMatch(token, @"^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.CultureInvariant))
        {
            target.Add(token);
        }
    }
}
