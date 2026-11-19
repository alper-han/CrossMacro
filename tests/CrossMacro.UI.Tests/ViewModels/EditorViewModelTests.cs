using CrossMacro.Infrastructure.Services.ScreenCapture;

namespace CrossMacro.UI.Tests.ViewModels;

public sealed partial class EditorViewModelTests : IDisposable
{
    private static readonly CancellationToken NonCancelableToken = new(canceled: false);
    private static readonly string[] PngExtensions = ["png"];
    private const string TransparentPngBase64 = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+ip1sAAAAASUVORK5CYII=";

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
        _screenPixelReader = Substitute.For<IScreenPixelReader>();
        _macroPlayer = Substitute.For<IMacroPlayer>();
        _ = _keyCodeMapper.GetKeyName(Arg.Any<int>()).Returns("A");
        _ = _screenPixelReader.IsSupported.Returns(returnThis: true);
        _localizationService = TestLocalization.Bracketed(
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Editor_StatusValidationFailed"] = "[Editor_StatusValidationFailed]",
                ["Editor_DialogButtonOk"] = "[Editor_DialogButtonOk]",
                ["Editor_TextInputEscapedControlHint"] = "[Editor_TextInputEscapedControlHint]",
                ["Editor_Action_TextInput"] = "Type \"{0}\"",
                ["Editor_BlockName_If"] = "IfToken",
                ["Editor_BlockName_Repeat"] = "RepeatToken",
                ["Editor_BlockName_Else"] = "ElseToken",
                ["Editor_BlockName_While"] = "WhileToken",
                ["Editor_BlockName_For"] = "ForToken",
                ["Editor_BlockName_Block"] = "BlockToken",
                ["Editor_Action_EndBlock"] = "End {0}",
                ["Editor_LoadWarningStep"] = "Step {0}: {1} ({2})",
            },
            TestLocalization.EditorActionTypeName);

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
            new ImageAssetPreviewDecoder(imageAssetCodec),
            uiDispatcher: ImmediateUiDispatcher.Instance);
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
        _viewModel.Filters.HideMouseMoves = true;
        _viewModel.Filters.HideShortWaits = true;
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
