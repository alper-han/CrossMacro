using System.Collections.Specialized;
using CrossMacro.Platform.Abstractions.Input.Capture;
using CrossMacro.UI.Icons;

namespace CrossMacro.UI.ViewModels.Editor;

/// <summary>
/// ViewModel for the Macro Editor tab.
/// Provides manual macro creation and editing capabilities.
/// </summary>
public partial class EditorViewModel : ViewModelBase, IDisposable
{
    // External snapshots update presentation without re-running user-change effects.
    private bool _isRefreshingPresentation;

    private enum EditorStatusKind
    {
        Ready,
        Other,
    }

    private readonly EditorHistory _history;
    private readonly EditorCaptureSession _captureSession;
    private readonly EditorTestPlayback _testPlayback;
    private readonly EditorDocumentSession _document = new();
    private readonly EditorSelection _selection = new();
    private readonly IEditorActionConverter _converter;
    private readonly IEditorActionValidator _validator;
    private readonly ICoordinateCaptureService _captureService;
    private readonly IMacroFileManager _fileManager;
    private readonly IDialogService _dialogService;
    private readonly IKeyCodeMapper _keyCodeMapper;
    private readonly ILocalizationService _localizationService;
    private readonly EditorActionDisplayFormatter _actionDisplayFormatter;
    private readonly IScreenPixelReader? _screenPixelReader;
    private readonly IImageAssetCodec? _imageAssetCodec;
    private readonly IImageAssetPreviewDecoder? _imageAssetPreviewDecoder;
    private readonly IMacroPlayer _macroPlayer;

    [ObservableProperty]
    private string _macroName;
    private string _status;
    private readonly EditorSkipInitialZeroZero _skip;
    private readonly EditorSelectedActionReactions _reactions;
    private readonly EditorActionSubscriptions _subscriptions;
    internal bool SkipInitialZeroZeroFlag => _skip.Value;
    internal bool IsRestoringState { get; set; }
    internal bool IsSynchronizingActionProperties { get; set; }
    private readonly VariableSuggestionScope _suggestionScope = new();
    private readonly EditorCaptureController _capture;
    private readonly EditorMacroBuilder _macroBuilder;
    private readonly EditorTestRunner _testRunner;
    private readonly EditorFileOperations _files;
    private readonly EditorUndoRedo _undo;
    private readonly EditorActionListEditor _listEditor;
    private readonly EditorActionListBuilder _listBuilder;
    private readonly EditorSelectionController _selectionController;
    internal bool IsBatchUpdatingActions { get; set; }
    internal bool IsDisposed { get; private set; }
    // Lifetime CTS for operations unrelated to test playback (image import/preview).
    private readonly CancellationTokenSource _viewModelCts = new();
    private bool AddToPlaybackOnSaveWhenUnlinked { get; set; } = true;
    private bool _isApplyingStatusKind;
    private EditorStatusKind _statusKind = EditorStatusKind.Ready;
    /// <summary>
    /// Event fired when a macro is created/saved.
    /// Includes the persisted source path chosen during save.
    /// </summary>
    public event EventHandler<EditorMacroCreatedEventArgs>? MacroCreated;

    /// <summary>
    /// Event fired when a loaded editor document is explicitly added to the playback session.
    /// </summary>
    public event EventHandler<EditorMacroPlaybackRequestedEventArgs>? PlaybackAddRequested;

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
        IMacroPlayer macroPlayer,
        ILocalizationService localizationService,
        EditorActionDisplayFormatter actionDisplayFormatter,
        IScreenPixelReader? screenPixelReader = null,
        IImageAssetCodec? imageAssetCodec = null,
        IImageAssetPreviewDecoder? imageAssetPreviewDecoder = null,
        IUiDispatcher? uiDispatcher = null,
        TimeProvider? timeProvider = null)
        : base(uiDispatcher)
    {
        _history = new EditorHistory(timeProvider ?? TimeProvider.System);
        _captureSession = new EditorCaptureSession(timeProvider ?? TimeProvider.System);
        _testPlayback = new EditorTestPlayback(macroPlayer);
        _converter = converter ?? throw new ArgumentNullException(nameof(converter));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _captureService = captureService ?? throw new ArgumentNullException(nameof(captureService));
        _fileManager = fileManager ?? throw new ArgumentNullException(nameof(fileManager));
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
        _keyCodeMapper = keyCodeMapper ?? throw new ArgumentNullException(nameof(keyCodeMapper));
        _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
        _actionDisplayFormatter = actionDisplayFormatter ?? throw new ArgumentNullException(nameof(actionDisplayFormatter));
        _screenPixelReader = screenPixelReader;
        _imageAssetCodec = imageAssetCodec;
        _imageAssetPreviewDecoder = imageAssetPreviewDecoder;
        _macroPlayer = macroPlayer ?? throw new ArgumentNullException(nameof(macroPlayer));
        _macroName = _localizationService["Editor_DefaultMacroName"];
        _status = BuildStatus(EditorStatusKind.Ready);

        ActionListItems = new ObservableCollection<EditorActionListItem>();
        LoadWarnings = new ObservableCollection<string>();
        ImageAssetNames = new ObservableCollection<string>();
        Arithmetic = new ScriptArithmeticEditor(this, _suggestionScope);
        Tab = new EditorTabState(this, string.Format(_localizationService.CurrentCulture, _localizationService["Editor_UntitledTab"], 1));
        Movement = new EditorMovementCondensation(this);
        Filters = new EditorListFilters(this);
        Batch = new EditorBatchDelayEditor(this);
        Picker = new EditorNewActionPicker(this, _actionDisplayFormatter);
        Fields = new EditorFieldVisibility(this, new EditorRawScriptHints(_localizationService));
        ImagePreview = new EditorImageAssetPreview(this, _document.ImageAssets, _imageAssetPreviewDecoder, UiDispatcher, _viewModelCts.Token);
        Variables = new EditorVariableSuggestions(this, _suggestionScope);
        ConditionRules = new ConditionOperatorRules(Actions);
        _capture = new EditorCaptureController(this, _captureSession, _captureService, _keyCodeMapper, _screenPixelReader, UiDispatcher);
        _macroBuilder = new EditorMacroBuilder(this, _document, _converter, _validator, _dialogService, _actionDisplayFormatter, UiDispatcher);
        _testRunner = new EditorTestRunner(this, _testPlayback, _macroBuilder, _macroPlayer, UiDispatcher);
        _undo = new EditorUndoRedo(this, _history);
        _skip = new EditorSkipInitialZeroZero(this);
        _reactions = new EditorSelectedActionReactions(this, _undo, _skip, _keyCodeMapper);
        _subscriptions = new EditorActionSubscriptions(this, _reactions);
        _listEditor = new EditorActionListEditor(this, _actionDisplayFormatter);
        _selectionController = new EditorSelectionController(this, _selection);
        _listBuilder = new EditorActionListBuilder(_localizationService, _actionDisplayFormatter);
        _files = new EditorFileOperations(this, _document, _macroBuilder, _fileManager, _dialogService, _imageAssetCodec, UiDispatcher, _viewModelCts.Token);
        Actions.CollectionChanged += OnActionsCollectionChanged;
        SelectedActionUnderlyingIndices.CollectionChanged += OnSelectedActionUnderlyingIndicesChanged;
        LoadWarnings.CollectionChanged += OnLoadWarningsCollectionChanged;
        _localizationService.CultureChanged += OnCultureChanged;
        RefreshAvailableVariableNames();
        RememberCurrentState();
        MarkDocumentClean();
    }

    #region Properties

    public ObservableCollection<EditorAction> Actions => _document.Actions;

    public ObservableCollection<EditorActionListItem> ActionListItems { get; }

    public ObservableCollection<int> SelectedActionUnderlyingIndices => _selection.UnderlyingIndices;

    public ObservableCollection<string> LoadWarnings { get; }

    public ObservableCollection<string> ImageAssetNames { get; }

    public ScriptArithmeticEditor Arithmetic { get; }

    public EditorTabState Tab { get; }

    public EditorMovementCondensation Movement { get; }

    public EditorListFilters Filters { get; }

    public EditorBatchDelayEditor Batch { get; }

    public EditorNewActionPicker Picker { get; }

    public EditorFieldVisibility Fields { get; }

    public EditorImageAssetPreview ImagePreview { get; }

    public bool ShowMovementCleanupControls => Actions.Count > 0;

    public EditorVariableSuggestions Variables { get; }

    public bool HasImageAssets => ImageAssetNames.Count > 0;

    public EditorAction? SelectedAction
    {
        get => _selection.PrimaryAction;
        set
        {
            if (_selection.PrimaryAction == value)
            {
                return;
            }

            _selection.PrimaryAction?.PropertyChanged -= _reactions.OnPropertyChanged;

            _selection.PrimaryAction = value;

            if (_selection.PrimaryAction is not null)
            {
                _selection.PrimaryAction.PropertyChanged += _reactions.OnPropertyChanged;
                _reactions.NormalizeState(_selection.PrimaryAction);
            }

            Arithmetic.SyncFromModel(_selection.PrimaryAction);

            OnPropertyChanged();
            OnPropertyChanged(nameof(HasSelectedAction));
            OnPropertyChanged(nameof(SelectedImageSearchMatchMode));
            OnPropertyChanged(nameof(SelectedActionIsAbsolute));
            OnPropertyChanged(nameof(SelectedActionIsRelative));
            OnPropertyChanged(nameof(SelectedActionIsRawRelative));
            OnPropertyChanged(nameof(SelectedActionIsLogicalRelative));
            NotifyVisibilityChanged();
            OnPropertyChanged(nameof(SelectedActionDisplayText));
            _ = ImagePreview.RefreshAsync();
            ResetPropertyEditUndoCoalescing();
            _selectionController.SyncSelectedRow();
            if (!_selection.IsSelectingFromList)
            {
                _selectionController.SyncIndicesToPrimary();
            }
        }
    }

    public EditorActionListItem? SelectedActionListItem
    {
        get => _selection.SelectedRow;
        set
        {
            if (ReferenceEquals(_selection.SelectedRow, value))
            {
                return;
            }

            _selection.SelectedRow = value;
            OnPropertyChanged();

            if (_selection.IsSelectingFromList)
            {
                return;
            }

            _selection.IsSelectingFromList = true;
            try
            {
                if (!ReferenceEquals(SelectedAction, value?.Action))
                {
                    SelectedAction = value?.Action;
                }
            }
            finally
            {
                _selection.IsSelectingFromList = false;
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
                _reactions.SetCoordinateMode(isAbsolute: true);
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
                _reactions.SetCoordinateMode(isAbsolute: false, MouseCoordinateSpace.RawDevice);
            }
        }
    }

    public bool SelectedActionIsRawRelative
    {
        get => SelectedAction is { IsAbsolute: false, CoordinateSpace: MouseCoordinateSpace.RawDevice };
        set
        {
            if (value)
            {
                _reactions.SetCoordinateMode(isAbsolute: false, MouseCoordinateSpace.RawDevice);
            }
        }
    }

    public bool SelectedActionIsLogicalRelative
    {
        get => SelectedAction is { IsAbsolute: false, CoordinateSpace: MouseCoordinateSpace.LogicalDesktop };
        set
        {
            if (value)
            {
                _reactions.SetCoordinateMode(isAbsolute: false, MouseCoordinateSpace.LogicalDesktop);
            }
        }
    }

    public bool HasSelectedAction => SelectedAction is not null;

    public EditorImageMatchMode SelectedImageSearchMatchMode
    {
        get => SelectedAction?.ImageSearchMatchMode ?? EditorImageMatchMode.Automatic;
        set => _reactions.SetImageMatchMode(value);
    }

    public bool HasSelectedActions => SelectedActionUnderlyingIndices.Count > 0;
    public int SelectedActionCount => SelectedActionUnderlyingIndices.Count;
    public bool ShowSingleSelectedActionProperties => HasSelectedAction && SelectedActionCount <= 1;
    public bool ShowMultiSelectionPropertiesHint => SelectedActionCount > 1 && !Batch.IsAvailable;
    public bool CanRemoveSelectedActions => HasSelectedActions;
    public bool CanDuplicateSelectedActions => HasSelectedActions;
    public bool CanMoveSelectedActionsUp => HasSelectedActions && SelectedActionUnderlyingIndices.Min() > 0;
    public bool CanMoveSelectedActionsDown => HasSelectedActions && SelectedActionUnderlyingIndices.Max() < Actions.Count - 1;

    partial void OnMacroNameChanged(string value)
    {
        if (_isRefreshingPresentation) { return; }
        Tab.OnMacroNameChanged(value);
        UpdateDirtyState();
    }

    [ObservableProperty]
    private bool _isDirty;

    partial void OnIsDirtyChanged(bool value) => Tab.NotifyTitleChanged();

    [ObservableProperty]
    private bool _isActive;

    public string? SourcePath
    {
        get;
        private set
        {
            if (!SetProperty(ref field, value))
            {
                return;
            }

            Tab.NotifyTitleChanged();
            OnPropertyChanged(nameof(CanAddToPlayback));
        }
    }

    public bool IsEmptyAndClean => !IsDirty
        && string.IsNullOrWhiteSpace(SourcePath)
        && Actions.Count is 0
        && _document.ImageAssets.Count is 0;

    public bool CanAddToPlayback => !string.IsNullOrWhiteSpace(SourcePath)
        && LinkedLoadedMacroSessionId is null
        && Actions.Count > 0;

    public Guid? LinkedLoadedMacroSessionId { get; private set; }

    public bool ShouldAddToPlaybackOnSave => LinkedLoadedMacroSessionId is null
        && AddToPlaybackOnSaveWhenUnlinked;

    // Kept manual: StatusChanged must fire after the PropertyChanged notification and the setter tracks status-kind bookkeeping.
    public string Status
    {
        get => _status;
        internal set
        {
            if (string.Equals(_status, value, StringComparison.Ordinal))
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
        LinkedLoadedMacroSessionId = sessionId;
        AddToPlaybackOnSaveWhenUnlinked = false;
        OnPropertyChanged(nameof(CanAddToPlayback));
        OnPropertyChanged(nameof(ShouldAddToPlaybackOnSave));
    }

    public void ClearLoadedMacroSessionLink()
    {
        LinkedLoadedMacroSessionId = null;
        AddToPlaybackOnSaveWhenUnlinked = false;
        OnPropertyChanged(nameof(CanAddToPlayback));
        OnPropertyChanged(nameof(ShouldAddToPlaybackOnSave));
    }


    public event EventHandler? LoadRequested;

    public void RequestLoad() => LoadRequested?.Invoke(this, EventArgs.Empty);

    public async Task AddToPlaybackAsync()
    {
        if (!CanAddToPlayback)
        {
            return;
        }

        var macro = await _macroBuilder.BuildValidAsync().ConfigureAwait(false);
        if (macro is null)
        {
            return;
        }

        await RunOnUiThreadAsync(() =>
        {
            if (CanAddToPlayback && SourcePath is { } sourcePath)
            {
                PlaybackAddRequested?.Invoke(this, new EditorMacroPlaybackRequestedEventArgs(macro, sourcePath));
            }
        }).ConfigureAwait(false);
    }

    internal EditorDocumentSnapshot CaptureDocumentState() => _document.Capture(MacroName, SkipInitialZeroZeroFlag);

    internal void UpdateDirtyState()
    {
        if (!IsRestoringState && !IsBatchUpdatingActions)
        {
            IsDirty = _document.IsDirty(MacroName, SkipInitialZeroZeroFlag);
        }
    }

    private void MarkDocumentClean(EditorDocumentSnapshot? savedState = null)
    {
        _document.MarkClean(savedState ?? CaptureDocumentState());
        UpdateDirtyState();
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCapturing))]
    [NotifyPropertyChangedFor(nameof(IsCapturingPosition))]
    [NotifyPropertyChangedFor(nameof(IsCapturingKey))]
    [NotifyPropertyChangedFor(nameof(IsCapturingTargetColor))]
    [NotifyPropertyChangedFor(nameof(IsCapturingConditionLeftColor))]
    [NotifyPropertyChangedFor(nameof(IsCapturingConditionRightColor))]
    [NotifyPropertyChangedFor(nameof(IsCapturingPixelSearchTopLeft))]
    [NotifyPropertyChangedFor(nameof(IsCapturingPixelSearchBottomRight))]
    [NotifyPropertyChangedFor(nameof(IsCapturingScreenshotRegionStart))]
    [NotifyPropertyChangedFor(nameof(IsCapturingScreenshotRegionEnd))]
    public partial EditorCaptureMode CaptureMode { get; internal set; }

    partial void OnCaptureModeChanged(EditorCaptureMode value) => Variables.NotifyColorPickersChanged();

    internal bool IsScreenPixelReaderSupported => _screenPixelReader?.IsSupported is true;

    internal ConditionOperatorRules ConditionRules { get; }

    internal void RefreshAvailableVariableNames()
    {
        var changed = Variables.Refresh();
        OnPropertyChanged(nameof(CanInsertElseBlock));
        OnPropertyChanged(nameof(CanRemoveBlock));
        if (changed)
        {
            Arithmetic.NotifyChanged();
            NotifyScreenReadingChanged();
        }

        Arithmetic.ClearSuggestions();
    }

    public bool IsCapturing => CaptureMode is not EditorCaptureMode.None;
    public bool IsCapturingPosition => CaptureMode is EditorCaptureMode.Position;
    public bool IsCapturingKey => CaptureMode is EditorCaptureMode.Key;
    public bool IsCapturingTargetColor => CaptureMode is EditorCaptureMode.TargetColor;
    public bool IsCapturingConditionLeftColor => CaptureMode is EditorCaptureMode.ConditionLeftColor;
    public bool IsCapturingConditionRightColor => CaptureMode is EditorCaptureMode.ConditionRightColor;
    public bool IsCapturingPixelSearchTopLeft => CaptureMode is EditorCaptureMode.PixelSearchTopLeft;
    public bool IsCapturingPixelSearchBottomRight => CaptureMode is EditorCaptureMode.PixelSearchBottomRight;
    public bool IsCapturingScreenshotRegionStart => CaptureMode is EditorCaptureMode.ScreenshotRegionStart;
    public bool IsCapturingScreenshotRegionEnd => CaptureMode is EditorCaptureMode.ScreenshotRegionEnd;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RunIcon))]
    [NotifyPropertyChangedFor(nameof(RunText))]
    [NotifyPropertyChangedFor(nameof(CanRunTest))]
    [NotifyPropertyChangedFor(nameof(CanRunSelectedTest))]
    public partial bool IsRunningTest { get; internal set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RunSelectedIcon))]
    [NotifyPropertyChangedFor(nameof(RunSelectedText))]
    [NotifyPropertyChangedFor(nameof(CanRunTest))]
    [NotifyPropertyChangedFor(nameof(CanRunSelectedTest))]
    public partial bool IsRunningSelectedTest { get; internal set; }

    public AppIcon RunIcon => IsRunningTest ? AppIcon.Stop : AppIcon.Play;
    public string RunText => IsRunningTest ? Localize("Editor_StopTest") : Localize("Editor_RunTest");

    public AppIcon RunSelectedIcon => IsRunningSelectedTest ? AppIcon.Stop : AppIcon.Play;
    public string RunSelectedText => IsRunningSelectedTest ? Localize("Editor_StopTest") : Localize("Editor_RunSelected");

    public bool CanRunTest => (HasActions || IsRunningTest) && !IsRunningSelectedTest;
    public bool CanRunSelectedTest => (HasSelectedActions || IsRunningSelectedTest) && !IsRunningTest;

    public bool CanUndo => _undo.CanUndo;
    public bool CanRedo => _undo.CanRedo;
    public bool HasActions => Actions.Count > 0;
    public bool HasLoadWarnings => LoadWarnings.Count > 0;

    // Kept manual: coerces the incoming value against RequiresSkipInitialZeroZero and tracks the pre-force state.
    public bool SkipInitialZeroZero
    {
        get => SkipInitialZeroZeroFlag;
        set
        {
            if (_skip.TrySet(value))
            {
                OnPropertyChanged();
                UpdateDirtyState();
            }
        }
    }

    public bool RequiresSkipInitialZeroZero => Actions.Any(EditorActionCoordinateRules.IsCurrentPositionMouseButton);
    public bool CanEditSkipInitialZeroZero => !RequiresSkipInitialZeroZero;

    public static IEnumerable<EditorActionType> ActionTypes => Enum.GetValues<EditorActionType>();
    public static IReadOnlyList<EditorActionType> AddableActionTypes { get; } = Enum
        .GetValues<EditorActionType>()
        .Where(IsUserAddableActionType)
        .ToArray();

    public string FormatActionType(EditorActionType actionType) => _actionDisplayFormatter.FormatActionType(actionType);
    public static IEnumerable<MacroMouseButton> MouseButtons => Enum.GetValues<MacroMouseButton>().Where(button => button is not MacroMouseButton.None);
    public IReadOnlyList<MacroMouseButton> ImageClickButtons { get; } =
        [MacroMouseButton.Left, MacroMouseButton.Right, MacroMouseButton.Middle];
    public static IEnumerable<ScriptValueType> ScriptValueTypes => Enum.GetValues<ScriptValueType>();
    public IEnumerable<ClipboardCopyShortcutOption> ClipboardCopyShortcuts =>
        [
            new(ClipboardCopyShortcut.CtrlC, Localize("Editor_CopyShortcut_CtrlC")),
            new(ClipboardCopyShortcut.CtrlShiftC, Localize("Editor_CopyShortcut_CtrlShiftC")),
        ];
    public static IEnumerable<ScriptNumericSourceType> ScriptNumericSourceTypes => Enum.GetValues<ScriptNumericSourceType>();
    public static IEnumerable<ScriptOperandType> ScriptOperandTypes => Enum.GetValues<ScriptOperandType>();
    public IEnumerable<ShellCommandModeOption> ShellCommandModes => Enum.GetValues<ShellCommandMode>()
        .Select(v => new ShellCommandModeOption(v, Localize($"Enum_ShellCommandMode_{v}")));
    public IEnumerable<WindowCommandModeOption> WindowCommandModes => Enum.GetValues<WindowCommandMode>()
        .Select(v => new WindowCommandModeOption(v, Localize($"Enum_WindowCommandMode_{(v is WindowCommandMode.Floating ? "Float" : v)}")));
    public static IReadOnlyList<string> WindowSearchSelectorKinds { get; } = WindowSelectorSyntax.GetAllowedTokens(WindowCommandMode.Search);
    public static IReadOnlyList<string> WindowFocusSelectorKinds { get; } = WindowSelectorSyntax.GetAllowedTokens(WindowCommandMode.Focus);
    public static IReadOnlyList<string> WindowCloseSelectorKinds { get; } = WindowSelectorSyntax.GetAllowedTokens(WindowCommandMode.Close);
    public static IReadOnlyList<string> WindowActiveFields { get; } = WindowActiveFieldSyntax.Tokens;
    #endregion

    public string SelectedActionDisplayText
    {
        get => SelectedAction?.Text ?? string.Empty;
        set
        {
            if (SelectedAction is null)
            {
                return;
            }

            var text = value;
            if (string.Equals(SelectedAction.Text, text, StringComparison.Ordinal))
            {
                return;
            }

            SelectedAction.Text = text;
        }
    }

    public bool CanInsertElseBlock => CanInsertElseForSelection();
    public bool CanRemoveBlock => CanRemoveSelectedBlock();

    internal static bool IsUserAddableActionType(EditorActionType actionType)
    {
        return actionType is not (EditorActionType.BlockEnd or EditorActionType.ElseBlockStart or EditorActionType.RawScriptStep);
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (IsDisposed)
        {
            return;
        }

        IsDisposed = true;
        try
        {
            _viewModelCts.Cancel();
        }
        catch (AggregateException ex)
        {
            Log.Debug(ex, "[EditorViewModel] Cancellation callbacks failed during dispose");
        }

        _viewModelCts.Dispose();
        _subscriptions.UnsubscribeAll();
        SelectedAction?.PropertyChanged -= _reactions.OnPropertyChanged;
        Actions.CollectionChanged -= OnActionsCollectionChanged;
        SelectedActionUnderlyingIndices.CollectionChanged -= OnSelectedActionUnderlyingIndicesChanged;
        LoadWarnings.CollectionChanged -= OnLoadWarningsCollectionChanged;
        _localizationService.CultureChanged -= OnCultureChanged;
        _captureSession.Dispose();
        _testPlayback.Dispose();
        ImagePreview.Clear();
    }

    private void OnCultureChanged(object? sender, EventArgs e)
    {
        PostToUiThread(() =>
        {
            if (Tab.UsesDefaultMacroName)
            {
                // Relocalizing the default name preserves its default-name status.
                var wasRefreshingPresentation = _isRefreshingPresentation;
                _isRefreshingPresentation = true;
                try
                {
                    MacroName = Localize("Editor_DefaultMacroName");
                }
                finally
                {
                    _isRefreshingPresentation = wasRefreshingPresentation;
                }
                OnPropertyChanged(nameof(MacroName));
            }

            if (_statusKind is EditorStatusKind.Ready)
            {
                SetStatusKind(EditorStatusKind.Ready);
            }

            UpdateActionListPresentation();
            OnPropertyChanged(nameof(ShellCommandModes));
            OnPropertyChanged(nameof(WindowCommandModes));
            OnPropertyChanged(nameof(ClipboardCopyShortcuts));
            OnPropertyChanged(nameof(AddableActionTypes));
            OnPropertyChanged(nameof(ActionTypes));
            OnPropertyChanged(nameof(SelectedAction));
            OnPropertyChanged(nameof(SelectedActionListItem));
            OnPropertyChanged(nameof(CanInsertElseBlock));
            OnPropertyChanged(nameof(CanRemoveBlock));
            OnPropertyChanged(nameof(RunText));
            OnPropertyChanged(nameof(RunSelectedText));
            NotifyScreenReadingChanged();
            Picker.Rebuild(Picker.ActionType);
        });
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
            EditorStatusKind.Other => _status,
            _ => throw new ArgumentOutOfRangeException(nameof(statusKind), statusKind, message: null),
        };
    }

    private bool CanRemoveSelectedBlock()
    {
        return SelectedAction is not null && Actions.IndexOf(SelectedAction) >= 0
&& (EditorActionListStructure.IsScriptBlockStart(SelectedAction.Type) || SelectedAction.Type is EditorActionType.BlockEnd);
    }

    private bool CanInsertElseForSelection()
    {
        if ((SelectedAction?.Type) is not EditorActionType.IfBlockStart)
        {
            return false;
        }

        var ifIndex = Actions.IndexOf(SelectedAction);
        if (ifIndex < 0 || !EditorActionListStructure.TryFindMatchingBlockEnd(Actions, ifIndex, out var blockEndIndex))
        {
            return false;
        }

        return blockEndIndex + 1 >= Actions.Count
            || Actions[blockEndIndex + 1].Type is not EditorActionType.ElseBlockStart;
    }

    private void OnActionsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(ShowMovementCleanupControls));
        _subscriptions.OnCollectionChanged(e);

        if (!IsBatchUpdatingActions)
        {
            RefreshActionCollectionState();
            UpdateDirtyState();
        }
    }

    internal void RefreshActionCollectionState()
    {
        UpdateActionIndices();
        UpdateActionListPresentation();
        _reactions.RefreshCurrentPosition();
        RefreshAvailableVariableNames();
        OnPropertyChanged(nameof(CanRemoveBlock));
        Filters.NotifyChanged();
        OnPropertyChanged(nameof(HasActions));
        OnPropertyChanged(nameof(CanRunTest));
        _selectionController.NormalizeIndices();
        NotifySelectedActionsChanged();
    }

    private void OnSelectedActionUnderlyingIndicesChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        _selectionController.OnIndicesChanged();

    public void ReplaceSelectedActionUnderlyingIndices(IEnumerable<int> underlyingIndices)
    {
        ArgumentNullException.ThrowIfNull(underlyingIndices);

        _selectionController.ReplaceIndices(underlyingIndices);
    }

    internal void NotifyActionTypeChanged()
    {
        OnPropertyChanged(nameof(CanRemoveBlock));
        Filters.NotifyChanged();
    }

    internal void NotifyDelayChanged()
    {
        Filters.NotifyChanged();
        Batch.NotifyChanged();
    }

    private void OnLoadWarningsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(HasLoadWarnings));
    }

    internal void SetLoadWarnings(IEnumerable<EditorActionRestoreWarning> warnings)
    {
        LoadWarnings.Clear();
        foreach (var warning in warnings)
        {
            var stepPreview = warning.Step.Length > 120
                ? warning.Step[..120] + "..."
                : warning.Step;
            LoadWarnings.Add(string.Format(_localizationService.CurrentCulture, Localize("Editor_LoadWarningStep"), warning.StepIndex, warning.Message, stepPreview));
        }
    }

    internal string Localize(string key)
    {
        return _localizationService[key];
    }

    internal string LocalizeFormat(string key, params object[] args) =>
        string.Format(_localizationService.CurrentCulture, Localize(key), args);

    internal void UpdateActionListPresentation()
    {
        var previousSelectionSyncFlag = _selection.IsSelectingFromList;
        _selection.IsSelectingFromList = true;
        try
        {
            Movement.RefreshPlan();
            var rows = _listBuilder.Build(Actions, Movement.Plan, Filters.HideMouseMoves, Filters.HideShortWaits, Filters.SimplifyMovement);
            ActionListItems.Clear();
            foreach (var row in rows.Items)
            {
                ActionListItems.Add(row);
            }

            Filters.HiddenEventCount = rows.HiddenEventCount;
            Movement.NotifyChanged();
            Filters.NotifyChanged();

            _selectionController.NormalizeIndices();
            if (SelectedActionUnderlyingIndices.Count > 0)
            {
                _selectionController.SelectPrimaryFromIndices();
            }
            else
            {
                _selectionController.SyncSelectedRow();
            }

            NotifySelectedActionsChanged();
        }
        finally
        {
            _selection.IsSelectingFromList = previousSelectionSyncFlag;
        }
    }

    internal void NotifySelectedActionListItemChanged() => OnPropertyChanged(nameof(SelectedActionListItem));

    internal void NotifySelectedActionsChanged()
    {
        OnPropertyChanged(nameof(HasSelectedActions));
        OnPropertyChanged(nameof(SelectedActionCount));
        OnPropertyChanged(nameof(ShowSingleSelectedActionProperties));
        OnPropertyChanged(nameof(ShowMultiSelectionPropertiesHint));
        Batch.NotifyChanged();
        OnPropertyChanged(nameof(CanRemoveSelectedActions));
        Filters.NotifyChanged();
        OnPropertyChanged(nameof(CanDuplicateSelectedActions));
        OnPropertyChanged(nameof(CanMoveSelectedActionsUp));
        OnPropertyChanged(nameof(CanMoveSelectedActionsDown));
        OnPropertyChanged(nameof(CanRunSelectedTest));
    }

    internal EditorAction[] GetSelectedActions() => _selectionController.GetSelectedActions();

    internal int[] GetSelectedActionIndices() => _selectionController.GetSelectedIndices();

    internal int[] NormalizeActionIndices(IEnumerable<int> indices) => _selectionController.Normalize(indices);

    internal void SelectUnderlyingIndices(IEnumerable<int> indices) => _selectionController.SelectIndices(indices);

    internal void ClearActionSelection() => _selectionController.Clear();

    internal void RemoveCondensedMovement(IReadOnlyCollection<int> indices) => _listEditor.RemoveCondensedMovement(indices);

    #region Commands

    public void AddAction() => _listEditor.AddAction();

    public void AddCurrentPositionClick() => _listEditor.AddCurrentPositionClick();

    public void InsertElseBlock() => _listEditor.InsertElseBlock();

    public void RemoveBlock() => _listEditor.RemoveBlock();

    public void RemoveAction() => _listEditor.RemoveAction();

    public void RemoveSelectedActions() => _listEditor.RemoveSelectedActions();

    public void DeleteHiddenEvents() => _listEditor.DeleteHiddenEvents();

    public void MoveUp() => _listEditor.MoveUp();

    public void MoveDown() => _listEditor.MoveDown();

    public void MoveSelectedActionsUp() => _listEditor.MoveSelectedActionsUp();

    public void MoveSelectedActionsDown() => _listEditor.MoveSelectedActionsDown();

    public void DuplicateAction() => _listEditor.DuplicateAction();

    public void DuplicateSelectedActions() => _listEditor.DuplicateSelectedActions();

    public void ClearAll() => _listEditor.ClearAll();

    public void Undo() => _undo.Undo();

    public void Redo() => _undo.Redo();

    internal static List<EditorAction> CloneActions(IEnumerable<EditorAction> actions) => actions.Select(action => action.Clone()).ToList();

    internal void SaveUndoState() => _undo.Save();

    internal void ClearUndoHistory() => _undo.Clear();

    internal void RememberCurrentState() => _undo.Remember();

    internal void ResetPropertyEditUndoCoalescing() => _undo.ResetCoalescing();

    internal void NotifySelectedActionDisplayTextChanged() => OnPropertyChanged(nameof(SelectedActionDisplayText));

    internal void NotifyImageMatchModeChanged() => OnPropertyChanged(nameof(SelectedImageSearchMatchMode));

    internal void NotifyCoordinateModeChanged()
    {
        OnPropertyChanged(nameof(SelectedActionIsAbsolute));
        OnPropertyChanged(nameof(SelectedActionIsRelative));
        OnPropertyChanged(nameof(SelectedActionIsRawRelative));
        OnPropertyChanged(nameof(SelectedActionIsLogicalRelative));
    }

    internal void NotifyHistoryChanged()
    {
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
    }

    internal void NotifyActionCountChanged()
    {
        OnPropertyChanged(nameof(HasActions));
        OnPropertyChanged(nameof(CanAddToPlayback));
        OnPropertyChanged(nameof(CanRunTest));
    }

    /// <summary>Completes a structural list edit: refreshes count-dependent state and records the new baseline for undo.</summary>
    internal void FinishListEdit(bool actionCountChanged)
    {
        if (actionCountChanged)
        {
            NotifyActionCountChanged();
        }

        ResetPropertyEditUndoCoalescing();
        RememberCurrentState();
    }

    private void UpdateActionIndices()
    {
        for (var index = 0; index < Actions.Count; index++)
        {
            Actions[index].Index = index + 1;
        }
    }

    internal void NotifyVisibilityChanged()
    {
        Fields.NotifyChanged();
        OnPropertyChanged(nameof(SkipInitialZeroZero));
        OnPropertyChanged(nameof(RequiresSkipInitialZeroZero));
        OnPropertyChanged(nameof(CanEditSkipInitialZeroZero));
        OnPropertyChanged(nameof(CanRemoveBlock));
        Variables.NotifyChanged();
        Arithmetic.NotifyChanged();
        NotifyScreenReadingChanged();
        RefreshAvailableVariableNames();
    }

    internal void NotifyScreenReadingChanged()
    {
        Fields.NotifyChanged();
        ImagePreview.NotifyChanged();
        Variables.ScreenTargetColor.NotifyChanged();
    }

    public Task CaptureMouseAsync() => _capture.CaptureMouseAsync();

    public Task CaptureKeyAsync() => _capture.CaptureKeyAsync();

    public Task CaptureTargetColorAsync() => _capture.CaptureTargetColorAsync();

    public Task CaptureConditionLeftColorAsync() => _capture.CaptureConditionLeftColorAsync();

    public Task CaptureConditionRightColorAsync() => _capture.CaptureConditionRightColorAsync();

    public Task CapturePixelSearchTopLeftAsync() => _capture.CapturePixelSearchTopLeftAsync();

    public Task CapturePixelSearchBottomRightAsync() => _capture.CapturePixelSearchBottomRightAsync();

    public Task CaptureScreenshotRegionStartAsync() => _capture.CaptureScreenshotRegionStartAsync();

    public Task CaptureScreenshotRegionEndAsync() => _capture.CaptureScreenshotRegionEndAsync();

    public void CancelCapture() => _capture.Cancel();

    public Task ToggleTestPlaybackAsync() => _testRunner.ToggleAsync(targetActions: null, isSelectedOnly: false);

    public Task ToggleTestPlaybackSelectedAsync() => _testRunner.ToggleAsync(targetActions: GetSelectedActions(), isSelectedOnly: true);

    public Task SaveMacroAsync() => _files.SaveAsync(saveAs: false);

    public Task SaveMacroAsAsync() => _files.SaveAsync(saveAs: true);

    public Task<bool> SaveMacroForCloseAsync() => _files.SaveAsync(saveAs: false);

    public Task BrowseScreenshotOutputPathAsync() => _files.BrowseScreenshotOutputPathAsync();

    public Task ImportImageAssetAsync() => _files.ImportImageAssetAsync();

    public Task<bool> LoadMacroFromFileAsync(string filePath) => _files.LoadFromFileAsync(filePath);

    internal void CompleteSave(MacroSequence sequence, string filePath, EditorDocumentSnapshot savedState)
    {
        SourcePath = filePath;
        MarkDocumentClean(savedState);
        Status = LocalizeFormat("Editor_StatusSaved", Path.GetFileName(filePath));
        MacroCreated?.Invoke(this, new EditorMacroCreatedEventArgs(sequence, filePath));
    }

    internal void CompleteLoad(MacroSequence sequence, string filePath)
    {
        LoadMacroSequence(sequence, filePath);
        ClearUndoHistory();
        Status = HasLoadWarnings
            ? LocalizeFormat("Editor_StatusLoadedWithWarnings", Path.GetFileName(filePath), LoadWarnings.Count)
            : LocalizeFormat("Editor_StatusLoaded", Path.GetFileName(filePath));
    }

    internal void AddImageAsset(string assetName, string encodedImage)
    {
        _document.ImageAssets[assetName] = encodedImage;
        ImageAssetNames.Add(assetName);
        OnPropertyChanged(nameof(HasImageAssets));
        UpdateDirtyState();

        if (SelectedAction?.Type is EditorActionType.ImageSearch or EditorActionType.ImageClick or EditorActionType.WaitImage)
        {
            SelectedAction.ImageAssetName = assetName;
        }

        Status = LocalizeFormat("Editor_StatusImageImported", assetName);
    }

    internal void ApplyResolvedSkipInitialZeroZero(bool skipInitialZeroZero)
    {
        if (_skip.Apply(skipInitialZeroZero))
        {
            OnPropertyChanged(nameof(SkipInitialZeroZero));
        }
    }

    /// <summary>
    /// Loads a MacroSequence for editing.
    /// </summary>
    public void LoadMacroSequence(MacroSequence sequence, string? sourcePath = null)
    {
        ArgumentNullException.ThrowIfNull(sequence);
        SaveUndoState();

        ClearLoadedMacroSessionLink();
        SourcePath = sourcePath;
        ImagePreview.Clear();
        var restoreResult = _converter.FromMacroSequenceWithDiagnostics(sequence);
        var editorActions = restoreResult.Actions;
        SetLoadWarnings(restoreResult.Warnings);
        if (sequence.ScriptSteps.Count > 0 && !restoreResult.RestoredFromScriptSteps)
        {
            LoadWarnings.Add(Localize("Editor_StatusRestoreWarningFallback"));
        }

        IsBatchUpdatingActions = true;
        try
        {
            Actions.Clear();
            _document.ImageAssets.Clear();
            ImageAssetNames.Clear();
            if (sequence.Images is { Count: > 0 })
            {
                foreach (var image in sequence.Images.OrderBy(image => image.Key, StringComparer.Ordinal))
                {
                    _document.ImageAssets[image.Key] = image.Value;
                    ImageAssetNames.Add(image.Key);
                }
            }

            MacroName = sequence.Name;
            foreach (var action in editorActions)
            {
                Actions.Add(action);
            }
        }
        finally
        {
            IsBatchUpdatingActions = false;
        }

        OnPropertyChanged(nameof(HasImageAssets));
        var hasCurrentPositionMouseButtons = editorActions.Any(EditorActionCoordinateRules.IsCurrentPositionMouseButton);
        _skip.Load(sequence.SkipInitialZeroZero, hasCurrentPositionMouseButtons);

        SelectedAction = Actions.FirstOrDefault();
        OnPropertyChanged(nameof(HasActions));
        RefreshActionCollectionState();
        ResetPropertyEditUndoCoalescing();
        RememberCurrentState();
        MarkDocumentClean();
    }

    #endregion
}
