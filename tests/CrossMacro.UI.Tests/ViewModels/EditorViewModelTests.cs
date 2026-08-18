
namespace CrossMacro.UI.Tests.ViewModels;

public sealed partial class EditorViewModelTests : IDisposable
{
    private static readonly CancellationToken NonCancelableToken = new(canceled: false);
    private static readonly string[] PngExtensions = ["png"];
    private const string TransparentPngBase64 = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+ip1sAAAAASUVORK5CYII=";

    public static TheoryData<string> Task7BindingMembers => new()
    {
        nameof(EditorViewModel.ShowPixelColorFields),
        nameof(EditorViewModel.ShowWaitColorFields),
        nameof(EditorViewModel.ShowPixelSearchFields),
        nameof(EditorViewModel.ShowScreenReadingFields),
        nameof(EditorViewModel.ShowScreenReadingColorFields),
        nameof(EditorViewModel.ShowScreenReadingPointFields),
        nameof(EditorViewModel.ShowScreenReadingRawAssistance),
        nameof(EditorViewModel.ScreenReadingRawHint),
        nameof(EditorViewModel.ShowScreenReadingColorPreview),
        nameof(EditorViewModel.ScreenReadingColorPreviewHex),
        nameof(EditorViewModel.ScreenTargetColorSources),
        nameof(EditorViewModel.AvailableColorVariableNames),
        nameof(EditorViewModel.HasAvailableColorVariableNames),
        nameof(EditorViewModel.SelectedScreenTargetColorVariableSuggestion),
        nameof(EditorViewModel.ShowScreenTargetColorHexInput),
        nameof(EditorViewModel.ShowScreenTargetColorVariableInput),
        nameof(EditorViewModel.ShowScreenTargetColorVariablePicker),
        nameof(EditorViewModel.AvailableVariableNames),
        nameof(EditorViewModel.HasAvailableVariableNames),
        nameof(EditorViewModel.SelectedSetVariableSuggestion),
        nameof(EditorViewModel.SelectedClipboardVariableSuggestion),
        nameof(EditorViewModel.ShellCommandModes),
        nameof(EditorViewModel.ShowShellCommandFields),
        nameof(EditorViewModel.ShowShellStandardInputFields),
        nameof(EditorViewModel.ShowShellCaptureFields),
        nameof(EditorViewModel.WindowCommandModes),
        nameof(EditorViewModel.WindowSearchSelectorKinds),
        nameof(EditorViewModel.WindowFocusSelectorKinds),
        nameof(EditorViewModel.WindowCloseSelectorKinds),
        nameof(EditorViewModel.WindowActiveFields),
        nameof(EditorViewModel.ShowWindowCommandFields),
        nameof(EditorViewModel.ShowWindowSelectorFields),
        nameof(EditorViewModel.ShowWindowSearchSelectorKinds),
        nameof(EditorViewModel.ShowWindowFocusSelectorKinds),
        nameof(EditorViewModel.ShowWindowCloseSelectorKinds),
        nameof(EditorViewModel.ShowWindowSelectorValueField),
        nameof(EditorViewModel.ShowWindowActiveFieldSelector),
        nameof(EditorViewModel.ShowWindowCoordinateFields),
        nameof(EditorViewModel.ShowWindowDimensionFields),
        nameof(EditorViewModel.ShowWindowTimeoutField),
        nameof(EditorViewModel.ShowWindowOutputVariableField),
        nameof(EditorViewModel.ShowWindowWorkspaceField),
        nameof(EditorViewModel.ShowWindowAddressField),
        nameof(EditorViewModel.SelectedIncDecVariableSuggestion),
        nameof(EditorViewModel.SelectedConditionLeftVariableSuggestion),
        nameof(EditorViewModel.SelectedConditionRightVariableSuggestion),
        nameof(EditorViewModel.SelectedForVariableSuggestion),
        nameof(EditorViewModel.ShowSetVariablePicker),
        nameof(EditorViewModel.ShowClipboardGetFields),
        nameof(EditorViewModel.ShowClipboardVariablePicker),
        nameof(EditorViewModel.ShowScreenshotFields),
        nameof(EditorViewModel.ShowScreenshotRegionFields),
        nameof(EditorViewModel.ShowIncDecVariablePicker),
        nameof(EditorViewModel.ShowConditionLeftVariablePicker),
        nameof(EditorViewModel.ShowConditionLeftOperandTextBox),
        nameof(EditorViewModel.ShowConditionLeftColorPicker),
        nameof(EditorViewModel.ShowConditionRightVariablePicker),
        nameof(EditorViewModel.ShowConditionRightOperandTextBox),
        nameof(EditorViewModel.ShowConditionRightColorPicker),
        nameof(EditorViewModel.ShowForVariablePicker),
        nameof(EditorViewModel.ScriptConditionOperators),
        nameof(EditorViewModel.ConditionRightOperandHint),
        nameof(EditorViewModel.CanUndo),
        nameof(EditorViewModel.CanRedo),
        nameof(EditorViewModel.CaptureMouseAsync),
        nameof(EditorViewModel.CaptureTargetColorAsync),
        nameof(EditorViewModel.CaptureConditionLeftColorAsync),
        nameof(EditorViewModel.CaptureConditionRightColorAsync),
        nameof(EditorViewModel.CapturePixelSearchTopLeftAsync),
        nameof(EditorViewModel.CapturePixelSearchBottomRightAsync),
        nameof(EditorViewModel.CaptureScreenshotRegionStartAsync),
        nameof(EditorViewModel.CaptureScreenshotRegionEndAsync),
        nameof(EditorViewModel.ShowImageSearchFields),
        nameof(EditorViewModel.SelectedImageAssetPreview),
        nameof(EditorViewModel.ShowSelectedImageAssetPreview),
        nameof(EditorViewModel.HasImageAssets),
        nameof(EditorViewModel.ImageAssetNames),
        nameof(EditorViewModel.ImportImageAssetAsync),
        nameof(EditorViewModel.BrowseScreenshotOutputPathAsync),
        nameof(EditorViewModel.CancelCapture),
    };

    private readonly IEditorActionConverter _converter;
    private readonly IEditorActionValidator _validator;
    private readonly ICoordinateCaptureService _captureService;
    private readonly IMacroFileManager _fileManager;
    private readonly IDialogService _dialogService;
    private readonly IKeyCodeMapper _keyCodeMapper;
    private readonly ILocalizationService _localizationService;
    private readonly IScreenPixelReader _screenPixelReader;
    private readonly IMacroPlayer _macroPlayer;
    private readonly EditorViewModel _viewModel;

    public EditorViewModelTests()
    {
        _converter = Substitute.For<IEditorActionConverter>();
        _validator = Substitute.For<IEditorActionValidator>();
        _captureService = Substitute.For<ICoordinateCaptureService>();
        _fileManager = Substitute.For<IMacroFileManager>();
        _dialogService = Substitute.For<IDialogService>();
        _keyCodeMapper = Substitute.For<IKeyCodeMapper>();
        _localizationService = Substitute.For<ILocalizationService>();
        _screenPixelReader = Substitute.For<IScreenPixelReader>();
        _macroPlayer = Substitute.For<IMacroPlayer>();
        _ = _keyCodeMapper.GetKeyName(Arg.Any<int>()).Returns("A");
        _ = _screenPixelReader.IsSupported.Returns(returnThis: true);
        _ = _localizationService.CurrentCulture.Returns(System.Globalization.CultureInfo.InvariantCulture);
        _ = _localizationService[Arg.Any<string>()].Returns(call => call.Arg<string>() switch
        {
            "Editor_DefaultMacroName" => "[Editor_DefaultMacroName]",
            "Editor_StatusReady" => "[Editor_StatusReady]",
            "Editor_StatusAddedAction" => "[Editor_StatusAddedAction] {0}",
            "Editor_StatusRemovedAction" => "[Editor_StatusRemovedAction]",
            "Editor_StatusRemovedSelectedActions" => "[Editor_StatusRemovedSelectedActions]",
            "Editor_StatusDuplicatedSelectedActions" => "[Editor_StatusDuplicatedSelectedActions]",
            "Editor_StatusMovedSelectedActionsUp" => "[Editor_StatusMovedSelectedActionsUp]",
            "Editor_StatusMovedSelectedActionsDown" => "[Editor_StatusMovedSelectedActionsDown]",
            "Editor_StatusDeletedHiddenEvents" => "[Editor_StatusDeletedHiddenEvents]",
            "Editor_StatusNoHiddenEventsToDelete" => "[Editor_StatusNoHiddenEventsToDelete]",
            "Editor_SimplifiedMovementHint" => "[Editor_SimplifiedMovementHint] {0}",
            "Editor_StatusUndone" => "[Editor_StatusUndone]",
            "Editor_StatusRedone" => "[Editor_StatusRedone]",
            "Editor_StatusCaptureSelectionChanged" => "[Editor_StatusCaptureSelectionChanged]",
            "Editor_StatusInsertedElseBlock" => "[Editor_StatusInsertedElseBlock]",
            "Editor_StatusOperationBlocked" => "[Editor_StatusOperationBlocked]",
            "Editor_StatusAutoManagedAction" => "[Editor_StatusAutoManagedAction]",
            "Editor_StatusPixelReaderUnavailable" => "[Editor_StatusPixelReaderUnavailable]",
            "Editor_StatusCaptureColorPrompt" => "[Editor_StatusCaptureColorPrompt]",
            "Editor_StatusCaptureColorFailed" => "[Editor_StatusCaptureColorFailed] {0}",
            "Editor_StatusCapturedColor" => "[Editor_StatusCapturedColor] {0} {1} {2}",
            "Editor_StatusCapturedRegionTopLeft" => "[Editor_StatusCapturedRegionTopLeft] {0} {1}",
            "Editor_StatusCapturedRegionBottomRight" => "[Editor_StatusCapturedRegionBottomRight] {0} {1}",
            "Editor_StatusCaptureRegionInvalidBottomRight" => "[Editor_StatusCaptureRegionInvalidBottomRight]",
            "Editor_StatusCaptureCancelled" => "[Editor_StatusCaptureCancelled]",
            "Editor_StatusRemovedBlock" => "[Editor_StatusRemovedBlock]",
            "Editor_StatusValidationFailed" => "[Editor_StatusValidationFailed]",
            "Editor_DialogTitleNoActions" => "[Editor_DialogTitleNoActions]",
            "Editor_DialogMessageNoActions" => "[Editor_DialogMessageNoActions]",
            "Editor_DialogTitleValidationErrors" => "[Editor_DialogTitleValidationErrors]",
            "Editor_ValidationErrorHeader" => "[Editor_ValidationErrorHeader]",
            "Editor_DialogButtonOk" => "[Editor_DialogButtonOk]",
            "Editor_CurrentPositionClick" => "[Editor_CurrentPositionClick]",
            "Editor_CurrentPositionHold" => "[Editor_CurrentPositionHold]",
            "Editor_CurrentPositionRelease" => "[Editor_CurrentPositionRelease]",
            "Editor_CurrentPositionUse" => "[Editor_CurrentPositionUse]",
            "Editor_TextInputEscapedControlHint" => "[Editor_TextInputEscapedControlHint]",
            "Editor_Action_TextInput" => "Type \"{0}\"",
            "Editor_BlockName_If" => "IfToken",
            "Editor_BlockName_Repeat" => "RepeatToken",
            "Editor_BlockName_Else" => "ElseToken",
            "Editor_BlockName_While" => "WhileToken",
            "Editor_BlockName_For" => "ForToken",
            "Editor_BlockName_Block" => "BlockToken",
            _ when call.Arg<string>().StartsWith("Editor_ActionType_", StringComparison.Ordinal) => call.Arg<string>()["Editor_ActionType_".Length..],
            _ => call.Arg<string>(),
        });

        _ = _validator.ValidateAll(Arg.Any<IEnumerable<EditorAction>>()).Returns((true, new List<string>()));

        var imageAssetCodec = new ImageAssetCodec();

        _viewModel = new EditorViewModel(
            _converter,
            _validator,
            _captureService,
            _fileManager,
            _dialogService,
            _keyCodeMapper,
            _macroPlayer,
            _localizationService,
            new EditorActionDisplayFormatter(_localizationService),
            _screenPixelReader,
            imageAssetCodec,
            new ImageAssetPreviewDecoder(imageAssetCodec));
    }

    public void Dispose()
    {
        _viewModel.Dispose();
    }



































































































    public static IEnumerable<object[]> ActionVisualMetadataCases()
    {
        yield return MetadataCase(new EditorAction { Type = EditorActionType.MouseMove }, EditorActionVisualKind.Movement, isNoise: true, isImportant: false, isCleanupEligible: true);
        yield return MetadataCase(new EditorAction { Type = EditorActionType.Delay, DelayMs = 1 }, EditorActionVisualKind.Noise, isNoise: true, isImportant: false, isCleanupEligible: true);
        yield return MetadataCase(new EditorAction { Type = EditorActionType.Delay, DelayMs = 9 }, EditorActionVisualKind.Noise, isNoise: true, isImportant: false, isCleanupEligible: true);
        yield return MetadataCase(new EditorAction { Type = EditorActionType.Delay, DelayMs = 0 }, EditorActionVisualKind.Timing, isNoise: false, isImportant: false, isCleanupEligible: false);
        yield return MetadataCase(new EditorAction { Type = EditorActionType.Delay, DelayMs = 10 }, EditorActionVisualKind.Timing, isNoise: false, isImportant: true, isCleanupEligible: false);
        yield return MetadataCase(new EditorAction { Type = EditorActionType.Delay, DelayMs = 20 }, EditorActionVisualKind.Timing, isNoise: false, isImportant: true, isCleanupEligible: false);
        yield return MetadataCase(new EditorAction { Type = EditorActionType.Delay, UseRandomDelay = true, RandomDelayMinMs = 1, RandomDelayMaxMs = 9 }, EditorActionVisualKind.Timing, isNoise: false, isImportant: true, isCleanupEligible: false);
        yield return MetadataCase(new EditorAction { Type = EditorActionType.MouseClick }, EditorActionVisualKind.PointerInput, isNoise: false, isImportant: true, isCleanupEligible: false);
        yield return MetadataCase(new EditorAction { Type = EditorActionType.MouseDown }, EditorActionVisualKind.PointerInput, isNoise: false, isImportant: true, isCleanupEligible: false);
        yield return MetadataCase(new EditorAction { Type = EditorActionType.MouseUp }, EditorActionVisualKind.PointerInput, isNoise: false, isImportant: true, isCleanupEligible: false);
        yield return MetadataCase(new EditorAction { Type = EditorActionType.ScrollVertical, ScrollAmount = 1 }, EditorActionVisualKind.PointerInput, isNoise: false, isImportant: true, isCleanupEligible: false);
        yield return MetadataCase(new EditorAction { Type = EditorActionType.ScrollHorizontal, ScrollAmount = 1 }, EditorActionVisualKind.PointerInput, isNoise: false, isImportant: true, isCleanupEligible: false);
        yield return MetadataCase(new EditorAction { Type = EditorActionType.KeyPress, KeyCode = 65 }, EditorActionVisualKind.Keyboard, isNoise: false, isImportant: true, isCleanupEligible: false);
        yield return MetadataCase(new EditorAction { Type = EditorActionType.KeyDown, KeyCode = 65 }, EditorActionVisualKind.Keyboard, isNoise: false, isImportant: true, isCleanupEligible: false);
        yield return MetadataCase(new EditorAction { Type = EditorActionType.KeyUp, KeyCode = 65 }, EditorActionVisualKind.Keyboard, isNoise: false, isImportant: true, isCleanupEligible: false);
        yield return MetadataCase(new EditorAction { Type = EditorActionType.TextInput, Text = "abc" }, EditorActionVisualKind.Text, isNoise: false, isImportant: true, isCleanupEligible: false);
        yield return MetadataCase(new EditorAction { Type = EditorActionType.RepeatBlockStart }, EditorActionVisualKind.ControlFlow, isNoise: false, isImportant: true, isCleanupEligible: false);
        yield return MetadataCase(new EditorAction { Type = EditorActionType.IfBlockStart }, EditorActionVisualKind.ControlFlow, isNoise: false, isImportant: true, isCleanupEligible: false);
        yield return MetadataCase(new EditorAction { Type = EditorActionType.ElseBlockStart }, EditorActionVisualKind.ControlFlow, isNoise: false, isImportant: true, isCleanupEligible: false);
        yield return MetadataCase(new EditorAction { Type = EditorActionType.WhileBlockStart }, EditorActionVisualKind.ControlFlow, isNoise: false, isImportant: true, isCleanupEligible: false);
        yield return MetadataCase(new EditorAction { Type = EditorActionType.ForBlockStart }, EditorActionVisualKind.ControlFlow, isNoise: false, isImportant: true, isCleanupEligible: false);
        yield return MetadataCase(new EditorAction { Type = EditorActionType.BlockEnd }, EditorActionVisualKind.ControlFlow, isNoise: false, isImportant: true, isCleanupEligible: false);
        yield return MetadataCase(new EditorAction { Type = EditorActionType.Break }, EditorActionVisualKind.ControlFlow, isNoise: false, isImportant: true, isCleanupEligible: false);
        yield return MetadataCase(new EditorAction { Type = EditorActionType.Continue }, EditorActionVisualKind.ControlFlow, isNoise: false, isImportant: true, isCleanupEligible: false);
        yield return MetadataCase(new EditorAction { Type = EditorActionType.SetVariable }, EditorActionVisualKind.Variable, isNoise: false, isImportant: true, isCleanupEligible: false);
        yield return MetadataCase(new EditorAction { Type = EditorActionType.IncrementVariable }, EditorActionVisualKind.Variable, isNoise: false, isImportant: true, isCleanupEligible: false);
        yield return MetadataCase(new EditorAction { Type = EditorActionType.DecrementVariable }, EditorActionVisualKind.Variable, isNoise: false, isImportant: true, isCleanupEligible: false);
        yield return MetadataCase(new EditorAction { Type = EditorActionType.RawScriptStep, Text = "raw" }, EditorActionVisualKind.Raw, isNoise: false, isImportant: true, isCleanupEligible: false);
    }

    private static void AddCondensibleRun(EditorViewModel viewModel, int actionCount)
    {
        for (var index = 0; index < actionCount; index++)
        {
            viewModel.Actions.Add(index % 2 is 0
                ? new EditorAction { Type = EditorActionType.MouseMove, X = index, Y = index + 1 }
                : new EditorAction { Type = EditorActionType.Delay, DelayMs = 4 });
        }
    }

    private void HideMovementAndShortWaitRows()
    {
        _viewModel.HideMouseMoves = true;
        _viewModel.HideShortWaits = true;
    }

    private static EditorActionListItem CreateActionListItem(EditorAction action, bool representsSourceAction)
    {
        return new EditorActionListItem(
            action,
            index: 0,
            underlyingIndex: 0,
            indentLevel: 0,
            displayName: "Action",
            condensedHint: string.Empty,
            visualKind: EditorActionVisualKind.PointerInput,
            isImportant: false,
            isCleanupEligible: false,
            condensedHiddenCount: 0,
            representsSourceAction: representsSourceAction);
    }

    private static object[] MetadataCase(
        EditorAction action,
        EditorActionVisualKind visualKind,
        bool isNoise,
        bool isImportant,
        bool isCleanupEligible)
    {
        return [action, visualKind, isNoise, isImportant, isCleanupEligible];
    }
























































































































    private static byte[] CreateOversizedPngBytes()
    {
        return
        [
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
            0x00, 0x00, 0x00, 0x0D,
            0x49, 0x48, 0x44, 0x52,
            0x00, 0x00, 0x1E, 0x01,
            0x00, 0x00, 0x00, 0x01,
            0x08,
            0x02,
            0x00,
            0x00,
            0x00,
            0x00, 0x00, 0x00, 0x00,
        ];
    }
}
