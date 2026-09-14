namespace CrossMacro.UI.ViewModels.Editor;

public sealed class EditorDocumentFactory(
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
{
    public EditorViewModel Create() => new(converter, validator, captureService, fileManager, dialogService,
        keyCodeMapper, macroPlayer, localizationService, actionDisplayFormatter, screenPixelReader,
        imageAssetCodec, imageAssetPreviewDecoder, uiDispatcher, this, timeProvider);
}
