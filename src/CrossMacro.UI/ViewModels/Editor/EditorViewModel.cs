
namespace CrossMacro.UI.ViewModels.Editor;

/// <summary>
/// ViewModel for the Macro Editor tab.
/// Provides manual macro creation and editing capabilities.
/// </summary>
public partial class EditorViewModel : ViewModelBase, IDisposable
{
    private enum EditorStatusKind
    {
        Ready,
        Other,
    }

    private enum ScriptVariableKind
    {
        Unknown,
        Number,
        Text,
        Boolean,
        Color,
    }

    public enum EditorCaptureMode
    {
        None,
        Position,
        Key,
        TargetColor,
        ConditionLeftColor,
        ConditionRightColor,
        PixelSearchTopLeft,
        PixelSearchBottomRight,
        ScreenshotRegionStart,
        ScreenshotRegionEnd,
    }

    private const string MacroFileExtension = ".macro";

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
    private bool _skipInitialZeroZero;
    private bool _skipInitialZeroZeroForcedByCurrentPosition;
    private bool _skipInitialZeroZeroBeforeCurrentPositionForce;
    private bool _isRestoringState;
    private bool _isSynchronizingActionProperties;
    private bool _isApplyingVariableSuggestion;
    private bool _isBatchUpdatingActions;
    private bool _disposed;
    // Lifetime CTS for operations unrelated to test playback (image import/preview).
    private readonly CancellationTokenSource _viewModelCts = new();
    private bool _usesDefaultMacroName = true;
    private string _untitledTabTitle = "Untitled 1";
    [ObservableProperty]
    private bool _isTabRenameInProgress;
    [ObservableProperty]
    private string _tabRenameText = string.Empty;
#pragma warning disable IDE0032 // SourcePath needs a private setter that also notifies the derived tab-title properties.
    private string? _sourcePath;
#pragma warning restore IDE0032
    private bool AddToPlaybackOnSaveWhenUnlinked { get; set; } = true;
    private bool _isApplyingStatusKind;
    private EditorStatusKind _statusKind = EditorStatusKind.Ready;
    private string? _selectedSetVariableSuggestion;
    private string? _selectedIncDecVariableSuggestion;
    private string? _selectedConditionLeftVariableSuggestion;
    private string? _selectedConditionRightVariableSuggestion;
    private string? _selectedForVariableSuggestion;
    private string? _selectedClipboardVariableSuggestion;
    private string? _selectedScreenTargetColorVariableSuggestion;
    private EditorActionPickerGroup? _newActionGroup;
    private EditorActionPickerChoice? _newActionChoice;
    private readonly HashSet<EditorAction> _subscribedActions = new();
    private static readonly IReadOnlyList<(string ResourceKey, EditorActionType[] ActionTypes)> EditorActionGroupDefinitions =
        [
            ("Editor_ActionGroup_Mouse", new[]
            {
                EditorActionType.MouseMove,
                EditorActionType.MouseClick,
                EditorActionType.MouseDown,
                EditorActionType.MouseUp,
                EditorActionType.MousePosition,
                EditorActionType.ScrollVertical,
                EditorActionType.ScrollHorizontal,
            }),
            ("Editor_ActionGroup_Keyboard", new[]
            {
                EditorActionType.KeyPress,
                EditorActionType.KeyDown,
                EditorActionType.KeyUp,
            }),
            ("Editor_ActionGroup_Timing", new[] { EditorActionType.Delay }),
            ("Editor_ActionGroup_Text", new[] { EditorActionType.TextInput }),
            ("Editor_ActionGroup_Variables", new[]
            {
                EditorActionType.SetVariable,
                EditorActionType.CopySelectionToVariable,
                EditorActionType.IncrementVariable,
                EditorActionType.DecrementVariable,
                EditorActionType.MultiplyVariable,
                EditorActionType.DivideVariable,
            }),
            ("Editor_ActionGroup_FlowControl", new[]
            {
                EditorActionType.RepeatBlockStart,
                EditorActionType.IfBlockStart,
                EditorActionType.WhileBlockStart,
                EditorActionType.ForBlockStart,
                EditorActionType.Break,
                EditorActionType.Continue,
            }),
            ("Editor_ActionGroup_ScreenReading", new[]
            {
                EditorActionType.PixelColor,
                EditorActionType.WaitColor,
                EditorActionType.PixelSearch,
                EditorActionType.ImageSearch,
                EditorActionType.ImageClick,
                EditorActionType.WaitImage,
            }),
            ("Editor_ActionGroup_Screenshot", new[]
            {
                EditorActionType.Screenshot,
            }),
            ("Editor_ActionGroup_Clipboard", new[]
            {
                EditorActionType.ClipboardGet,
                EditorActionType.ClipboardSet,
            }),
            ("Editor_ActionGroup_Shell", new[]
            {
                EditorActionType.ShellCommand,
            }),
            ("Editor_ActionGroup_Window", new[]
            {
                EditorActionType.WindowCommand,
            }),
        ];

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
        ILocalizationService? localizationService = null,
        EditorActionDisplayFormatter? actionDisplayFormatter = null,
        IScreenPixelReader? screenPixelReader = null,
        IImageAssetCodec? imageAssetCodec = null,
        IImageAssetPreviewDecoder? imageAssetPreviewDecoder = null,
        IUiDispatcher? uiDispatcher = null,
        EditorDocumentFactory? documentFactory = null,
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
        _localizationService = localizationService ?? new LocalizationService();
        _actionDisplayFormatter = actionDisplayFormatter ?? new EditorActionDisplayFormatter(_localizationService);
        _screenPixelReader = screenPixelReader;
        _imageAssetCodec = imageAssetCodec;
        _imageAssetPreviewDecoder = imageAssetPreviewDecoder;
        _macroPlayer = macroPlayer ?? throw new ArgumentNullException(nameof(macroPlayer));
        DocumentFactory = documentFactory ?? new EditorDocumentFactory(_converter, _validator, _captureService, _fileManager, _dialogService, _keyCodeMapper, _macroPlayer, _localizationService, _actionDisplayFormatter, _screenPixelReader, _imageAssetCodec, _imageAssetPreviewDecoder, UiDispatcher, timeProvider);
        _macroName = _localizationService["Editor_DefaultMacroName"];
        _status = BuildStatus(EditorStatusKind.Ready);
        RebuildAddableActionGroups(NewActionType);

        ActionListItems = new ObservableCollection<EditorActionListItem>();
        LoadWarnings = new ObservableCollection<string>();
        ImageAssetNames = new ObservableCollection<string>();
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

            _selection.PrimaryAction?.PropertyChanged -= OnSelectedActionPropertyChanged;

            _selection.PrimaryAction = value;

            if (_selection.PrimaryAction is not null)
            {
                _selection.PrimaryAction.PropertyChanged += OnSelectedActionPropertyChanged;
                NormalizeSelectedActionState(_selection.PrimaryAction);
            }

            SyncScriptArithmeticStateFromModel(_selection.PrimaryAction);

            OnPropertyChanged();
            OnPropertyChanged(nameof(HasSelectedAction));
            OnPropertyChanged(nameof(SelectedImageSearchMatchMode));
            OnPropertyChanged(nameof(SelectedActionIsAbsolute));
            OnPropertyChanged(nameof(SelectedActionIsRelative));
            OnPropertyChanged(nameof(SelectedActionIsRawRelative));
            OnPropertyChanged(nameof(SelectedActionIsLogicalRelative));
            NotifyVisibilityChanged();
            OnPropertyChanged(nameof(SelectedActionDisplayText));
            _ = RefreshSelectedImageAssetPreviewAsync();
            ResetPropertyEditUndoCoalescing();
            SyncSelectedActionListItem();
            if (!_selection.IsSelectingFromList)
            {
                SyncSelectedUnderlyingIndicesToPrimarySelection();
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
                SetSelectedActionCoordinateMode(isAbsolute: false, MouseCoordinateSpace.RawDevice);
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
                SetSelectedActionCoordinateMode(isAbsolute: false, MouseCoordinateSpace.RawDevice);
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
                SetSelectedActionCoordinateMode(isAbsolute: false, MouseCoordinateSpace.LogicalDesktop);
            }
        }
    }

    public bool HasSelectedAction => SelectedAction is not null;

    public EditorImageMatchMode SelectedImageSearchMatchMode
    {
        get => SelectedAction?.ImageSearchMatchMode ?? EditorImageMatchMode.Automatic;
        set => SetSelectedImageSearchMatchMode(value);
    }

    public bool HasSelectedActions => SelectedActionUnderlyingIndices.Count > 0;
    public int SelectedActionCount => SelectedActionUnderlyingIndices.Count;
    public bool ShowSingleSelectedActionProperties => HasSelectedAction && SelectedActionCount <= 1;
    public bool ShowBatchDelayProperties => SelectedActionCount > 1 && GetSelectedActions().All(action => action.Type is EditorActionType.Delay);
    public bool ShowMultiSelectionPropertiesHint => SelectedActionCount > 1 && !ShowBatchDelayProperties;
    public bool ShowBatchFixedDelayInput => ShowBatchDelayProperties && !BatchDelayUseRandomDelay;
    public bool ShowBatchRandomDelayOptions => ShowBatchDelayProperties && BatchDelayUseRandomDelay;
    public bool CanRemoveSelectedActions => HasSelectedActions;
    public bool CanDeleteHiddenEvents => Actions.Any(action => EditorActionListMetadata.IsHidden(action, HideMouseMoves, HideShortWaits));
    public bool ShowDeleteHiddenEvents => (HideMouseMoves || HideShortWaits) && CanDeleteHiddenEvents;
    public bool CanHideMouseMoves => Actions.Any(action => action.Type is EditorActionType.MouseMove);
    public bool ShowHideMouseMovesToggle => HideMouseMoves || CanHideMouseMoves;
    public bool CanHideShortWaits => Actions.Any(EditorActionListMetadata.IsShortWait);
    public bool ShowHideShortWaitsToggle => HideShortWaits || CanHideShortWaits;
    public bool CanSimplifyMovement => HasCondensibleMovementRun();
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

    public string BatchDelayDuration
    {
        get => MacroTiming.FormatDuration(GetSelectedDelayActions().FirstOrDefault()?.DelayMicroseconds ?? 0);
        set
        {
            if (!MacroTiming.TryParseDurationMicroseconds(value, out var microseconds))
            {
                return;
            }

            ApplyToSelectedDelayActions(
                nameof(EditorAction.DelayMicroseconds),
                action => action.DelayMicroseconds != microseconds,
                action => action.DelayMicroseconds = microseconds);
        }
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

    [ObservableProperty]
    private EditorActionType _newActionType = EditorActionType.MouseClick;

    partial void OnNewActionTypeChanged(EditorActionType value)
    {
        SyncNewActionPickerSelection(value);
    }

    public IReadOnlyList<EditorActionPickerGroup> AddableActionGroups { get; private set; } = [];

    // Kept manual: reference-equality guard with cascading picker synchronization that writes the backing fields directly.
    public EditorActionPickerGroup? NewActionGroup
    {
        get => _newActionGroup;
        set
        {
            if (ReferenceEquals(_newActionGroup, value))
            {
                return;
            }

            _newActionGroup = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(NewActionChoices));

            if (value != null && !value.Choices.Contains(_newActionChoice))
            {
                NewActionChoice = value.Choices.Count > 0 ? value.Choices[0] : null;
            }
        }
    }

    public IReadOnlyList<EditorActionPickerChoice> NewActionChoices => NewActionGroup?.Choices ?? [];

    // Kept manual: reference-equality guard with cascading picker synchronization that writes the backing fields directly.
    public EditorActionPickerChoice? NewActionChoice
    {
        get => _newActionChoice;
        set
        {
            if (ReferenceEquals(_newActionChoice, value))
            {
                return;
            }

            _newActionChoice = value;
            OnPropertyChanged();

            if (value != null && NewActionType != value.ActionType)
            {
                NewActionType = value.ActionType;
            }
        }
    }

    partial void OnMacroNameChanged(string value)
    {
        _usesDefaultMacroName = string.IsNullOrWhiteSpace(value)
            || string.Equals(value.Trim(), Localize("Editor_DefaultMacroName"), StringComparison.Ordinal);
        OnPropertyChanged(nameof(TabTitle));
        OnPropertyChanged(nameof(TabDisplayTitle));
        UpdateDirtyState();
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TabDisplayTitle))]
    private bool _isDirty;

    [ObservableProperty]
    private bool _isActive;

    public string? SourcePath
    {
        get => _sourcePath;
        private set
        {
            if (!SetProperty(ref _sourcePath, value))
            {
                return;
            }

            OnPropertyChanged(nameof(TabTitle));
            OnPropertyChanged(nameof(TabDisplayTitle));
            OnPropertyChanged(nameof(CanAddToPlayback));
        }
    }

    public string TabTitle
    {
        get
        {
            if (!_usesDefaultMacroName && !string.IsNullOrWhiteSpace(MacroName))
            {
                return MacroName.Trim();
            }

            return string.IsNullOrWhiteSpace(SourcePath)
                ? _untitledTabTitle
                : Path.GetFileNameWithoutExtension(SourcePath);
        }
    }

    public string TabDisplayTitle => IsDirty ? $"{TabTitle} *" : TabTitle;

    public bool IsEmptyAndClean => !IsDirty
        && string.IsNullOrWhiteSpace(SourcePath)
        && Actions.Count is 0
        && _document.ImageAssets.Count is 0;

    public bool CanAddToPlayback => !string.IsNullOrWhiteSpace(SourcePath)
        && LinkedLoadedMacroSessionId is null
        && Actions.Count > 0;

    public void SetUntitledTabTitle(string title)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        if (string.Equals(_untitledTabTitle, title, StringComparison.Ordinal))
        {
            return;
        }

        _untitledTabTitle = title;
        OnPropertyChanged(nameof(TabTitle));
        OnPropertyChanged(nameof(TabDisplayTitle));
    }

    public Guid? LinkedLoadedMacroSessionId { get; private set; }

    public bool ShouldAddToPlaybackOnSave => LinkedLoadedMacroSessionId is null
        && AddToPlaybackOnSaveWhenUnlinked;

    // Kept manual: StatusChanged must fire after the PropertyChanged notification and the setter tracks status-kind bookkeeping.
    public string Status
    {
        get => _status;
        private set
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

    internal EditorDocumentFactory DocumentFactory { get; }

    internal EditorWorkspaceViewModel CreateWorkspace() =>
        new(DocumentFactory, _dialogService, _localizationService, UiDispatcher, this);

    public event EventHandler? LoadRequested;

    public void RequestLoad() => LoadRequested?.Invoke(this, EventArgs.Empty);

    public void BeginTabRename()
    {
        TabRenameText = TabTitle;
        IsTabRenameInProgress = true;
    }

    public void CommitTabRename()
    {
        if (!IsTabRenameInProgress)
        {
            return;
        }

        IsTabRenameInProgress = false;
        if (string.IsNullOrWhiteSpace(TabRenameText))
        {
            return;
        }

        MacroName = TabRenameText.Trim();
        _usesDefaultMacroName = false;
        OnPropertyChanged(nameof(TabTitle));
        OnPropertyChanged(nameof(TabDisplayTitle));
    }

    public void CancelTabRename() => IsTabRenameInProgress = false;

    public async Task AddToPlaybackAsync()
    {
        if (!CanAddToPlayback)
        {
            return;
        }

        var macro = await BuildValidMacroSequenceAsync().ConfigureAwait(false);
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

    private EditorDocumentSnapshot CaptureDocumentState() => _document.Capture(MacroName, _skipInitialZeroZero);

    private void UpdateDirtyState()
    {
        if (!_isRestoringState && !_isBatchUpdatingActions)
        {
            IsDirty = _document.IsDirty(MacroName, _skipInitialZeroZero);
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
    [NotifyPropertyChangedFor(nameof(ShowConditionLeftColorPicker))]
    [NotifyPropertyChangedFor(nameof(ShowConditionRightColorPicker))]
    public partial EditorCaptureMode CaptureMode { get; private set; }

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
    public partial bool IsRunningTest { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RunSelectedIcon))]
    [NotifyPropertyChangedFor(nameof(RunSelectedText))]
    [NotifyPropertyChangedFor(nameof(CanRunTest))]
    [NotifyPropertyChangedFor(nameof(CanRunSelectedTest))]
    public partial bool IsRunningSelectedTest { get; private set; }

    public AppIcon RunIcon => IsRunningTest ? AppIcon.Stop : AppIcon.Play;
    public string RunText => IsRunningTest ? Localize("Editor_StopTest") : Localize("Editor_RunTest");

    public AppIcon RunSelectedIcon => IsRunningSelectedTest ? AppIcon.Stop : AppIcon.Play;
    public string RunSelectedText => IsRunningSelectedTest ? Localize("Editor_StopTest") : Localize("Editor_RunSelected");

    public bool CanRunTest => (HasActions || IsRunningTest) && !IsRunningSelectedTest;
    public bool CanRunSelectedTest => (HasSelectedActions || IsRunningSelectedTest) && !IsRunningTest;

    public bool CanUndo => _history.CanUndo;
    public bool CanRedo => _history.CanRedo;
    public bool HasActions => Actions.Count > 0;
    public bool HasLoadWarnings => LoadWarnings.Count > 0;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasHiddenEvents))]
    public partial int HiddenEventCount { get; private set; }
    public bool HasHiddenEvents => HiddenEventCount > 0;

    // Kept manual: UpdateActionListPresentation must run after the change notifications; a generated OnChanged hook would run before them.
    public bool HideMouseMoves
    {
        get; set
        {
            if (field == value)
            {
                return;
            }

            field = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ShowHideMouseMovesToggle));
            OnPropertyChanged(nameof(ShowDeleteHiddenEvents));
            UpdateActionListPresentation();
        }
    }

    // Kept manual: UpdateActionListPresentation must run after the change notifications; a generated OnChanged hook would run before them.
    public bool HideShortWaits
    {
        get; set
        {
            if (field == value)
            {
                return;
            }

            field = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ShowHideShortWaitsToggle));
            OnPropertyChanged(nameof(ShowDeleteHiddenEvents));
            UpdateActionListPresentation();
        }
    }

    // Kept manual: UpdateActionListPresentation must run after the change notifications; a generated OnChanged hook would run before them.
    public bool SimplifyMovement
    {
        get; set
        {
            if (field == value)
            {
                return;
            }

            field = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ShowSimplifyMovementToggle));
            UpdateActionListPresentation();
        }
    }

    // Kept manual: coerces the incoming value against RequiresSkipInitialZeroZero and tracks the pre-force state.
    public bool SkipInitialZeroZero
    {
        get => _skipInitialZeroZero;
        set
        {
            var normalized = RequiresSkipInitialZeroZero || value;
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
            UpdateDirtyState();
        }
    }

    public bool RequiresSkipInitialZeroZero => Actions.Any(IsCurrentPositionMouseButtonAction);
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
    public IReadOnlyList<string> WindowSearchSelectorKinds { get; } = ["title", "class"];
    public IReadOnlyList<string> WindowFocusSelectorKinds { get; } = ["active", "title", "class", "address"];
    public IReadOnlyList<string> WindowCloseSelectorKinds { get; } = ["active", "title", "address"];
    public IReadOnlyList<string> WindowActiveFields { get; } = ["title", "class", "address", "fullscreen", "maximize", "float", "pinned", "hidden", "geometry"];
    #endregion

    #region Visibility Properties

    /// <summary>
    /// Show coordinates for: MouseMove, MouseClick, MouseDown, MouseUp.
    /// </summary>
    public bool ShowCoordinates => SelectedAction is not null && UsesCoordinateFields(SelectedAction.Type)
&& !IsCurrentPositionMouseButtonAction(SelectedAction);

    /// <summary>
    /// Show Absolute/Relative toggle for all coordinate-bearing mouse actions.
    /// </summary>
    public bool ShowCoordModeToggle => SelectedAction is not null && UsesCoordinateFields(SelectedAction.Type)
&& !IsCurrentPositionMouseButtonAction(SelectedAction);

    /// <summary>
    /// Show current-position toggle for mouse button actions.
    /// </summary>
    public bool ShowCurrentPositionToggle => SelectedAction?.Type is
        EditorActionType.MouseClick or
        EditorActionType.MouseDown or
        EditorActionType.MouseUp;
    public string CurrentPositionToggleLabel => Localize(EditorActionPresentation.For(SelectedAction?.Type).CurrentPositionLabelKey);

    /// <summary>
    /// Show mouse button for: MouseClick, ImageClick, MouseDown, MouseUp
    /// </summary>
    public bool ShowMouseButton => SelectedAction?.Type is
        EditorActionType.MouseClick or
        EditorActionType.ImageClick or
        EditorActionType.MouseDown or
        EditorActionType.MouseUp;
    public bool ShowImageClickButton => (SelectedAction?.Type) is EditorActionType.ImageClick;

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
    public bool ShowDelay => (SelectedAction?.Type) is EditorActionType.Delay;

    /// <summary>
    /// Show fixed delay value when random delay is disabled.
    /// </summary>
    public bool ShowFixedDelayInput => ShowDelay && (SelectedAction?.UseRandomDelay) is not true;

    /// <summary>
    /// Show random delay bounds when random delay is enabled.
    /// </summary>
    public bool ShowRandomDelayOptions => ShowDelay && (SelectedAction?.UseRandomDelay) is true;

    /// <summary>
    /// Show scroll amount for: ScrollVertical, ScrollHorizontal
    /// </summary>
    public bool ShowScrollAmount => SelectedAction?.Type is
        EditorActionType.ScrollVertical or
        EditorActionType.ScrollHorizontal;

    /// <summary>
    /// Show text payload field for TextInput, RawScriptStep, and ClipboardSet.
    /// </summary>
    public bool ShowTextInput => SelectedAction?.Type is EditorActionType.TextInput or EditorActionType.RawScriptStep or EditorActionType.ClipboardSet;
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
    public bool ShowSetVariableFields => (SelectedAction?.Type) is EditorActionType.SetVariable;
    public bool ShowClipboardGetFields => (SelectedAction?.Type) is EditorActionType.ClipboardGet;
    public bool ShowCopySelectionToVariableFields => (SelectedAction?.Type) is EditorActionType.CopySelectionToVariable;
    public bool ShowMousePositionFields => (SelectedAction?.Type) is EditorActionType.MousePosition;
    public bool ShowScreenshotFields => (SelectedAction?.Type) is EditorActionType.Screenshot;
    public bool ShowScreenshotRegionFields => ShowScreenshotFields && (SelectedAction?.ScreenshotUseRegion) is true;
    public bool ShowShellCommandFields => (SelectedAction?.Type) is EditorActionType.ShellCommand;
    public bool ShowShellStandardInputFields => ShowShellCommandFields
        && SelectedAction?.ShellCommandMode is ShellCommandMode.ShellInput or ShellCommandMode.ShellCaptureInput;
    public bool ShowShellCaptureFields => ShowShellCommandFields
        && SelectedAction?.ShellCommandMode is ShellCommandMode.ShellCapture or ShellCommandMode.ShellCaptureInput;
    public bool ShowWindowCommandFields => (SelectedAction?.Type) is EditorActionType.WindowCommand;
    public bool ShowWindowSelectorFields => ShowWindowCommandFields
        && SelectedAction?.WindowCommandMode is WindowCommandMode.Search or WindowCommandMode.Wait or WindowCommandMode.Focus or WindowCommandMode.Close;
    public bool ShowWindowSearchSelectorKinds => ShowWindowCommandFields
        && SelectedAction?.WindowCommandMode is WindowCommandMode.Search or WindowCommandMode.Wait;
    public bool ShowWindowFocusSelectorKinds => ShowWindowCommandFields && (SelectedAction?.WindowCommandMode) is WindowCommandMode.Focus;
    public bool ShowWindowCloseSelectorKinds => ShowWindowCommandFields && (SelectedAction?.WindowCommandMode) is WindowCommandMode.Close;
    public bool ShowWindowSelectorValueField => ShowWindowSelectorFields && (SelectedAction?.WindowSelectorKind) is not "active";
    public bool ShowWindowActiveFieldSelector => ShowWindowCommandFields && (SelectedAction?.WindowCommandMode) is WindowCommandMode.Active;
    public bool ShowWindowCoordinateFields => ShowWindowCommandFields && (SelectedAction?.WindowCommandMode) is WindowCommandMode.Move;
    public bool ShowWindowDimensionFields => ShowWindowCommandFields && (SelectedAction?.WindowCommandMode) is WindowCommandMode.Resize;
    public bool ShowWindowTimeoutField => ShowWindowCommandFields && (SelectedAction?.WindowCommandMode) is WindowCommandMode.Wait;
    public bool ShowWindowOutputVariableField => ShowWindowCommandFields
        && SelectedAction?.WindowCommandMode is WindowCommandMode.Active or WindowCommandMode.Search or WindowCommandMode.Wait or WindowCommandMode.WorkspaceGet;
    public bool ShowWindowWorkspaceField => ShowWindowCommandFields
        && SelectedAction?.WindowCommandMode is WindowCommandMode.WorkspaceSwitch or WindowCommandMode.WorkspaceMoveActive or WindowCommandMode.WorkspaceMoveWindow;
    public bool ShowWindowAddressField => ShowWindowCommandFields && (SelectedAction?.WindowCommandMode) is WindowCommandMode.WorkspaceMoveWindow;
    public bool ShowIncDecFields => SelectedAction?.Type is EditorActionType.IncrementVariable or EditorActionType.DecrementVariable or EditorActionType.MultiplyVariable or EditorActionType.DivideVariable;
    public bool ShowRepeatFields => (SelectedAction?.Type) is EditorActionType.RepeatBlockStart;
    public bool ShowConditionFields => SelectedAction?.Type is EditorActionType.IfBlockStart or EditorActionType.WhileBlockStart;
    public bool ShowForFields => (SelectedAction?.Type) is EditorActionType.ForBlockStart;
    public bool ShowForStepFields => ShowForFields && (SelectedAction?.ForHasStep) is true;
    public bool CanInsertElseBlock => CanInsertElseForSelection();
    public bool CanRemoveBlock => CanRemoveSelectedBlock();

    public string TextInputLabel => Localize(EditorActionPresentation.For(SelectedAction?.Type).TextInputLabelKey);

    public string TextInputWatermark => Localize(EditorActionPresentation.For(SelectedAction?.Type).TextInputWatermarkKey);

    public string TextInputHint
    {
        get
        {
            var presentation = EditorActionPresentation.For(SelectedAction?.Type);
            return SelectedAction?.Type is EditorActionType.RawScriptStep && TryGetRawScriptHint(SelectedAction.Text, out var hint)
                ? hint
                : Localize(presentation.TextInputHintKey);
        }
    }

    public bool TextInputAcceptsReturn => SelectedAction?.Type is EditorActionType.TextInput or EditorActionType.RawScriptStep or EditorActionType.ClipboardSet;

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
        try
        {
            _viewModelCts.Cancel();
        }
        catch (AggregateException ex)
        {
            Log.Debug(ex, "[EditorViewModel] Cancellation callbacks failed during dispose");
        }

        _viewModelCts.Dispose();
        foreach (var action in _subscribedActions)
        {
            action.PropertyChanged -= OnAnyActionPropertyChanged;
        }

        _subscribedActions.Clear();
        SelectedAction?.PropertyChanged -= OnSelectedActionPropertyChanged;
        Actions.CollectionChanged -= OnActionsCollectionChanged;
        SelectedActionUnderlyingIndices.CollectionChanged -= OnSelectedActionUnderlyingIndicesChanged;
        LoadWarnings.CollectionChanged -= OnLoadWarningsCollectionChanged;
        _localizationService.CultureChanged -= OnCultureChanged;
        _captureSession.Dispose();
        _testPlayback.Dispose();
        SetSelectedImageAssetPreview(preview: null);
    }

    private void OnCultureChanged(object? sender, EventArgs e)
    {
        PostToUiThread(() =>
        {
            if (_usesDefaultMacroName)
            {
                // Direct field write: relocalizing the default name must not clear the default-name flag via the setter hook.
#pragma warning disable MVVMTK0034
                _macroName = Localize("Editor_DefaultMacroName");
#pragma warning restore MVVMTK0034
                OnPropertyChanged(nameof(MacroName));
            }

            if (_statusKind is EditorStatusKind.Ready)
            {
                SetStatusKind(EditorStatusKind.Ready);
            }

            UpdateActionListPresentation();
            OnPropertyChanged(nameof(CurrentPositionToggleLabel));
            OnPropertyChanged(nameof(TextInputLabel));
            OnPropertyChanged(nameof(TextInputWatermark));
            OnPropertyChanged(nameof(TextInputHint));
            OnPropertyChanged(nameof(ShellCommandModes));
            OnPropertyChanged(nameof(WindowCommandModes));
            OnPropertyChanged(nameof(ClipboardCopyShortcuts));
            OnPropertyChanged(nameof(AddableActionTypes));
            OnPropertyChanged(nameof(ActionTypes));
            OnPropertyChanged(nameof(SelectedAction));
            OnPropertyChanged(nameof(SelectedActionListItem));
            OnPropertyChanged(nameof(CanInsertElseBlock));
            OnPropertyChanged(nameof(CanRemoveBlock));
            OnPropertyChanged(nameof(ConditionRightOperandHint));
            OnPropertyChanged(nameof(RunText));
            OnPropertyChanged(nameof(RunSelectedText));
            NotifyScreenReadingComputedPropertiesChanged();
            RebuildAddableActionGroups(NewActionType);
        });
    }

    private void RebuildAddableActionGroups(EditorActionType preferredActionType)
    {
        AddableActionGroups = EditorActionGroupDefinitions
            .Select(definition => new EditorActionPickerGroup(
                Localize(definition.ResourceKey),
                definition.ActionTypes
                    .Where(IsUserAddableActionType)
                    .Select(actionType => new EditorActionPickerChoice(actionType, _actionDisplayFormatter.FormatActionType(actionType)))
                    .ToArray()))
            .Where(group => group.Choices.Count > 0)
            .ToArray();

        OnPropertyChanged(nameof(AddableActionGroups));
        SyncNewActionPickerSelection(preferredActionType);
    }

    private void SyncNewActionPickerSelection(EditorActionType actionType)
    {
        foreach (var group in AddableActionGroups)
        {
            var choice = group.Choices.FirstOrDefault(item => item.ActionType == actionType);
            if (choice == null)
            {
                continue;
            }

            var groupChanged = !ReferenceEquals(_newActionGroup, group);
            var choiceChanged = !ReferenceEquals(_newActionChoice, choice);
            _newActionGroup = group;
            _newActionChoice = choice;

            if (groupChanged)
            {
                OnPropertyChanged(nameof(NewActionGroup));
                OnPropertyChanged(nameof(NewActionChoices));
            }

            if (choiceChanged)
            {
                OnPropertyChanged(nameof(NewActionChoice));
            }

            return;
        }

        var fallbackGroup = AddableActionGroups.Count > 0 ? AddableActionGroups[0] : null;
        var fallbackChoice = fallbackGroup?.Choices.Count > 0 ? fallbackGroup.Choices[0] : null;
        _newActionGroup = fallbackGroup;
        _newActionChoice = fallbackChoice;

        OnPropertyChanged(nameof(NewActionGroup));
        OnPropertyChanged(nameof(NewActionChoices));
        OnPropertyChanged(nameof(NewActionChoice));
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

    private static bool IsCurrentPositionMouseButtonAction(EditorAction? action)
    {
        return action?.Type is EditorActionType.MouseClick or EditorActionType.MouseDown or EditorActionType.MouseUp
            && action.UseCurrentPosition;
    }

    private bool CanRemoveSelectedBlock()
    {
        return SelectedAction is not null && Actions.IndexOf(SelectedAction) >= 0
&& (IsScriptBlockStartAction(SelectedAction.Type) || SelectedAction.Type is EditorActionType.BlockEnd);
    }

    private bool CanInsertElseForSelection()
    {
        if ((SelectedAction?.Type) is not EditorActionType.IfBlockStart)
        {
            return false;
        }

        var ifIndex = Actions.IndexOf(SelectedAction);
        if (ifIndex < 0 || !TryFindMatchingBlockEnd(ifIndex, out var blockEndIndex))
        {
            return false;
        }

        return blockEndIndex + 1 >= Actions.Count
            || Actions[blockEndIndex + 1].Type is not EditorActionType.ElseBlockStart;
    }

    private void OnActionsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action is NotifyCollectionChangedAction.Reset)
        {
            foreach (var action in _subscribedActions)
            {
                action.PropertyChanged -= OnAnyActionPropertyChanged;
            }

            _subscribedActions.Clear();
        SelectedAction?.PropertyChanged -= OnSelectedActionPropertyChanged;

            foreach (var action in Actions)
            {
                action.PropertyChanged += OnAnyActionPropertyChanged;
                _ = _subscribedActions.Add(action);
            }
        }
        else
        {
            if (e.OldItems is not null)
            {
                foreach (var item in e.OldItems.OfType<EditorAction>().Where(item => _subscribedActions.Remove(item)))
                {
                    item.PropertyChanged -= OnAnyActionPropertyChanged;
                }
            }

            if (e.NewItems is not null)
            {
                foreach (var item in e.NewItems.OfType<EditorAction>().Where(item => _subscribedActions.Add(item)))
                {
                    item.PropertyChanged += OnAnyActionPropertyChanged;
                }
            }
        }

        if (!_isBatchUpdatingActions)
        {
            RefreshActionCollectionState();
            UpdateDirtyState();
        }
    }

    private void RefreshActionCollectionState()
    {
        UpdateActionIndices();
        UpdateActionListPresentation();
        RefreshCurrentPositionConfiguration();
        RefreshAvailableVariableNames();
        OnPropertyChanged(nameof(CanRemoveBlock));
        OnPropertyChanged(nameof(CanDeleteHiddenEvents));
        OnPropertyChanged(nameof(ShowDeleteHiddenEvents));
        NotifyFilterToggleAvailabilityChanged();
        OnPropertyChanged(nameof(HasActions));
        OnPropertyChanged(nameof(CanRunTest));
        NormalizeSelectedUnderlyingIndices();
        NotifySelectedActionsChanged();
    }

    private void OnSelectedActionUnderlyingIndicesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_selection.IsSynchronizingIndices)
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
            .Order()
            .ToArray();

        if (SelectedActionUnderlyingIndices.SequenceEqual(normalized))
        {
            return;
        }

        _selection.IsSynchronizingIndices = true;
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
            _selection.IsSynchronizingIndices = false;
        }

        SelectPrimaryActionFromUnderlyingSelection();
        NotifySelectedActionsChanged();
    }

    private void OnAnyActionPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        UpdateDirtyState();
        if (!string.Equals(e.PropertyName, nameof(EditorAction.Index), StringComparison.Ordinal))
        {
            UpdateActionListPresentation();
        }

        if (string.Equals(e.PropertyName, nameof(EditorAction.Type), StringComparison.Ordinal))
        {
            OnPropertyChanged(nameof(CanRemoveBlock));
            OnPropertyChanged(nameof(CanDeleteHiddenEvents));
            OnPropertyChanged(nameof(ShowDeleteHiddenEvents));
            NotifyFilterToggleAvailabilityChanged();
        }

        if (e.PropertyName is nameof(EditorAction.DelayMs) or nameof(EditorAction.DelayMicroseconds) or nameof(EditorAction.UseRandomDelay))
        {
            OnPropertyChanged(nameof(CanDeleteHiddenEvents));
            OnPropertyChanged(nameof(ShowDeleteHiddenEvents));
            NotifyFilterToggleAvailabilityChanged();
            OnPropertyChanged(nameof(BatchDelayDuration));
        }

        if (string.Equals(e.PropertyName, nameof(EditorAction.Text), StringComparison.Ordinal))
        {
            OnPropertyChanged(nameof(TextInputHint));
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
            or nameof(EditorAction.MousePositionXVariableName)
            or nameof(EditorAction.MousePositionYVariableName)
            or nameof(EditorAction.ScreenColorVariableName)
            or nameof(EditorAction.ScreenFoundVariableName)
            or nameof(EditorAction.ScreenFoundXVariableName)
            or nameof(EditorAction.ScreenFoundYVariableName)
            or nameof(EditorAction.ShellExitCodeVariableName)
            or nameof(EditorAction.ShellStandardOutputVariableName)
            or nameof(EditorAction.ShellStandardErrorVariableName)
            or nameof(EditorAction.WindowOutputVariable)))
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
            LoadWarnings.Add($"Step {warning.StepIndex.ToString(CultureInfo.InvariantCulture)}: {warning.Message} ({stepPreview})");
        }
    }

    private string Localize(string key)
    {
        return _localizationService[key];
    }

    private void UpdateActionListPresentation()
    {
        var previousSelectionSyncFlag = _selection.IsSelectingFromList;
        _selection.IsSelectingFromList = true;
        try
        {
            ActionListItems.Clear();

            var depth = 0;
            var blockStack = new Stack<EditorActionType>();
            var isDragging = false;

            var hiddenEventCount = 0;
            for (var index = 0; index < Actions.Count; index++)
            {
                var action = Actions[index];
                var isInsideDrag = isDragging;
                var isLowImportance = EditorActionListMetadata.IsLowImportance(action, isInsideDrag);
                if (EditorActionListMetadata.IsHidden(action, HideMouseMoves, HideShortWaits))
                {
                    hiddenEventCount++;
                    EditorActionListMetadata.UpdateDragState(action, ref isDragging);
                    continue;
                }

                var condensedRun = SimplifyMovement && !isInsideDrag
                    ? TryGetCondensibleRun(index)
                    : null;
                if (condensedRun != null)
                {
                    var representativeAction = Actions[condensedRun.RepresentativeIndex];
                    var representativeIsLowImportance = EditorActionListMetadata.IsLowImportance(representativeAction, isInsideDrag: false);
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

                if (action.Type is EditorActionType.BlockEnd)
                {
                    if (depth > 0)
                    {
                        depth--;
                    }

                    var displayName = blockStack.Count > 0
                        ? $"End {_actionDisplayFormatter.FormatBlockName(blockStack.Pop())}"
                        : Localize("Editor_Action_EndBlockShort");

                    ActionListItems.Add(CreateActionListItem(action, index, depth, displayName, isLowImportance, condensedHiddenCount: 0));
                    EditorActionListMetadata.UpdateDragState(action, ref isDragging);
                    continue;
                }

                var rowDisplayName = _actionDisplayFormatter.Format(action);

                ActionListItems.Add(CreateActionListItem(action, index, depth, rowDisplayName, isLowImportance, condensedHiddenCount: 0));

                if (IsScriptBlockStartAction(action.Type))
                {
                    blockStack.Push(action.Type);
                    depth++;
                }

                EditorActionListMetadata.UpdateDragState(action, ref isDragging);
            }

            HiddenEventCount = hiddenEventCount;

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
            _selection.IsSelectingFromList = previousSelectionSyncFlag;
        }
    }

    private sealed record CondensibleRun(int EndIndex, int RepresentativeIndex, int HiddenCount);

    private CondensibleRun? TryGetCondensibleRun(int startIndex)
    {
        if (!EditorActionListMetadata.IsMovementCandidate(Actions[startIndex]))
        {
            return null;
        }

        var endIndex = startIndex;
        var representativeIndex = startIndex;
        var lastMouseMoveIndex = -1;

        for (var index = startIndex; index < Actions.Count; index++)
        {
            var action = Actions[index];
            if (!EditorActionListMetadata.IsMovementCandidate(action))
            {
                break;
            }

            endIndex = index;
            representativeIndex = index;
            if (action.Type is EditorActionType.MouseMove)
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
        var visualKind = EditorActionListMetadata.GetVisualKind(action, isNoise);

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
            EditorActionListMetadata.IsImportant(action, isNoise),
            EditorActionListMetadata.IsCleanupEligible(action, isNoise),
            condensedHiddenCount,
            representsSourceAction: true,
            isNoise);
    }

    private void SyncSelectedActionListItem()
    {
        var selectedRow = SelectedAction is null
            ? null
            : ActionListItems.FirstOrDefault(item => ReferenceEquals(item.Action, SelectedAction));
        if (ReferenceEquals(_selection.SelectedRow, selectedRow))
        {
            return;
        }

        _selection.SelectedRow = selectedRow;
        OnPropertyChanged(nameof(SelectedActionListItem));
    }

    private void SyncSelectedUnderlyingIndicesToPrimarySelection()
    {
        _selection.IsSynchronizingIndices = true;
        try
        {
            SelectedActionUnderlyingIndices.Clear();

            var selectedIndex = SelectedAction is null
                ? -1
                : Actions.IndexOf(SelectedAction);
            if (selectedIndex >= 0)
            {
                SelectedActionUnderlyingIndices.Add(selectedIndex);
            }
        }
        finally
        {
            _selection.IsSynchronizingIndices = false;
        }

        NotifySelectedActionsChanged();
    }

    private void NormalizeSelectedUnderlyingIndices()
    {
        if (_selection.IsSynchronizingIndices)
        {
            return;
        }

        var normalized = SelectedActionUnderlyingIndices
            .Where(index => index >= 0 && index < Actions.Count)
            .Distinct()
            .Order()
            .ToArray();

        if (SelectedActionUnderlyingIndices.SequenceEqual(normalized))
        {
            return;
        }

        _selection.IsSynchronizingIndices = true;
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
            _selection.IsSynchronizingIndices = false;
        }
    }

    private void SelectPrimaryActionFromUnderlyingSelection()
    {
        var selectedIndexSet = SelectedActionUnderlyingIndices.ToHashSet();
        var selectedRow = ActionListItems
            .Where(item => item.RepresentsSourceAction && selectedIndexSet.Contains(item.UnderlyingIndex))
            .OrderBy(item => item.UnderlyingIndex)
            .FirstOrDefault();

        _selection.IsSelectingFromList = true;
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
            _selection.IsSelectingFromList = false;
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
        OnPropertyChanged(nameof(BatchDelayDuration));
        OnPropertyChanged(nameof(BatchRandomDelayMinMs));
        OnPropertyChanged(nameof(BatchRandomDelayMaxMs));
        OnPropertyChanged(nameof(CanRemoveSelectedActions));
        OnPropertyChanged(nameof(CanDeleteHiddenEvents));
        OnPropertyChanged(nameof(ShowDeleteHiddenEvents));
        OnPropertyChanged(nameof(CanDuplicateSelectedActions));
        OnPropertyChanged(nameof(CanMoveSelectedActionsUp));
        OnPropertyChanged(nameof(CanMoveSelectedActionsDown));
        OnPropertyChanged(nameof(CanRunSelectedTest));
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

    internal EditorAction[] GetSelectedActions()
    {
        return SelectedActionUnderlyingIndices
            .Where(index => index >= 0 && index < Actions.Count)
            .Distinct()
            .Order()
            .Select(index => Actions[index])
            .ToArray();
    }

    private EditorAction[] GetSelectedDelayActions()
    {
        return GetSelectedActions()
            .Where(action => action.Type is EditorActionType.Delay)
            .ToArray();
    }

    private void ApplyToSelectedDelayActions(string propertyName, Func<EditorAction, bool> shouldUpdate, Action<EditorAction> update)
    {
        var actions = GetSelectedDelayActions();
        if (actions.Length is 0 || !actions.Any(shouldUpdate))
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
            nameof(EditorAction.DelayMs) or nameof(EditorAction.DelayMicroseconds) => nameof(BatchDelayDuration),
            nameof(EditorAction.RandomDelayMinMs) => nameof(BatchRandomDelayMinMs),
            nameof(EditorAction.RandomDelayMaxMs) => nameof(BatchRandomDelayMaxMs),
            _ => string.Empty,
        });
    }

    private bool HasCondensibleMovementRun()
    {
        var isDragging = false;
        var runLength = 0;
        foreach (var action in Actions)
        {
            if (!isDragging && EditorActionListMetadata.IsMovementCandidate(action))
            {
                runLength++;
                if (runLength >= 6)
                {
                    return true;
                }
            }
            else
            {
                runLength = 0;
            }

            EditorActionListMetadata.UpdateDragState(action, ref isDragging);
        }

        return false;
    }

}
