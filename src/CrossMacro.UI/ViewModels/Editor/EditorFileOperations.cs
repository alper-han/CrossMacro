namespace CrossMacro.UI.ViewModels.Editor;

/// <summary>File and dialog driven editor operations: save, load, screenshot path and image asset import.</summary>
internal sealed class EditorFileOperations(
    EditorViewModel owner,
    EditorDocumentSession document,
    EditorMacroBuilder macroBuilder,
    IMacroFileManager fileManager,
    IDialogService dialogService,
    IImageAssetCodec? imageAssetCodec,
    IUiDispatcher uiDispatcher,
    CancellationToken lifetimeToken)
{
    internal async Task<bool> SaveAsync(bool saveAs)
    {
        var sequence = await macroBuilder.BuildValidAsync().ConfigureAwait(false);
        if (sequence is null)
        {
            return false;
        }

        try
        {
            var filePath = saveAs ? null : owner.SourcePath;
            if (string.IsNullOrWhiteSpace(filePath))
            {
                var filters = MacroFileFormat.CreateDialogFilters(owner.Localize("Editor_MacroFileDialogName"));
                var baseName = MacroFileFormat.WithoutExtension(owner.MacroName);
                filePath = await dialogService.ShowSaveFileDialogAsync(owner.Localize("Editor_SaveDialogTitle"), $"{baseName}{MacroFileFormat.Extension}", filters).ConfigureAwait(false);
            }

            if (string.IsNullOrEmpty(filePath))
            {
                await uiDispatcher.RunOnUiThreadAsync(() => owner.Status = owner.Localize("Editor_StatusSaveCancelled")).ConfigureAwait(false);
                return false;
            }

            var savedState = await uiDispatcher.InvokeAsync<EditorDocumentSnapshot>(owner.CaptureDocumentState).ConfigureAwait(false);
            await fileManager.SaveAsync(sequence, filePath).ConfigureAwait(false);

            await uiDispatcher.RunOnUiThreadAsync(() => owner.CompleteSave(sequence, filePath, savedState)).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            await uiDispatcher.RunOnUiThreadAsync(() => owner.Status = owner.LocalizeFormat("Editor_StatusSaveError", ex.Message)).ConfigureAwait(false);
            return false;
        }
    }

    internal async Task<bool> LoadFromFileAsync(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        try
        {
            var sequence = await fileManager.LoadAsync(filePath).ConfigureAwait(false);
            if (sequence is null)
            {
                await uiDispatcher.RunOnUiThreadAsync(() =>
                {
                    owner.SetLoadWarnings([]);
                    owner.Status = owner.Localize("Editor_StatusLoadFailed");
                }).ConfigureAwait(false);
                return false;
            }

            await uiDispatcher.RunOnUiThreadAsync(() => owner.CompleteLoad(sequence, filePath)).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            await uiDispatcher.RunOnUiThreadAsync(() => owner.Status = owner.LocalizeFormat("Editor_StatusLoadError", ex.Message)).ConfigureAwait(false);
            return false;
        }
    }

    internal async Task BrowseScreenshotOutputPathAsync()
    {
        var action = owner.SelectedAction;
        if (action is null)
        {
            owner.Status = owner.Localize("Editor_StatusSelectActionFirst");
            return;
        }

        if (action.Type is not EditorActionType.Screenshot)
        {
            owner.Status = owner.Localize("Editor_StatusOperationBlocked");
            return;
        }

        var filters = new[]
        {
            new FileDialogFilter { Name = owner.Localize("Editor_ScreenshotFileDialogName"), Extensions = ["png"] },
        };
        var currentFileName = Path.GetFileName(action.ScreenshotOutputPath);
        var defaultFileName = string.IsNullOrWhiteSpace(currentFileName)
            ? owner.Localize("Editor_ScreenshotDefaultFileName")
            : currentFileName;

        var filePath = await dialogService.ShowSaveFileDialogAsync(
            owner.Localize("Editor_ScreenshotSaveDialogTitle"),
            defaultFileName,
            filters).ConfigureAwait(false);

        if (!string.IsNullOrEmpty(filePath))
        {
            await uiDispatcher.RunOnUiThreadAsync(() => action.ScreenshotOutputPath = filePath).ConfigureAwait(false);
        }
    }

    internal async Task ImportImageAssetAsync()
    {
        var filters = new[]
        {
            new FileDialogFilter { Name = owner.Localize("Editor_ImageAssetFileDialogName"), Extensions = ["png"] },
        };

        var filePath = await dialogService.ShowOpenFileDialogAsync(owner.Localize("Editor_ImageAssetImportDialogTitle"), filters).ConfigureAwait(false);
        if (string.IsNullOrEmpty(filePath))
        {
            await uiDispatcher.RunOnUiThreadAsync(() => owner.Status = owner.Localize("Editor_StatusImageImportCancelled")).ConfigureAwait(false);
            return;
        }

        try
        {
            var codec = imageAssetCodec ?? throw new InvalidOperationException("Image asset codec is not registered.");
            using var frame = await codec.DecodeFileAsync(filePath, lifetimeToken).ConfigureAwait(false);
            using var encoded = new MemoryStream();
            await codec.EncodePngAsync(frame, encoded, lifetimeToken).ConfigureAwait(false);
            var encodedImage = Convert.ToBase64String(encoded.ToArray());
            await uiDispatcher.RunOnUiThreadAsync(() => owner.AddImageAsset(GenerateUniqueImageAssetName(Path.GetFileNameWithoutExtension(filePath)), encodedImage)).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or NotSupportedException or ArgumentException or InvalidOperationException)
        {
            await uiDispatcher.RunOnUiThreadAsync(() => owner.Status = owner.LocalizeFormat("Editor_StatusImageImportError", ex.Message)).ConfigureAwait(false);
        }
    }

    private string GenerateUniqueImageAssetName(string? sourceName)
    {
        var baseName = NormalizeImageAssetName(sourceName);
        var candidate = baseName;
        var suffix = 2;
        while (document.ImageAssets.ContainsKey(candidate))
        {
            candidate = $"{baseName}_{suffix.ToString(CultureInfo.InvariantCulture)}";
            suffix++;
        }

        return candidate;
    }

    private static string NormalizeImageAssetName(string? sourceName)
    {
        var raw = string.IsNullOrWhiteSpace(sourceName) ? "image" : sourceName.Trim();
        var chars = raw.Select(ch => char.IsAsciiLetterOrDigit(ch) || ch == '_' ? ch : '_').ToArray();
        var normalized = new string(chars).Trim('_');
        if (string.IsNullOrWhiteSpace(normalized))
        {
            normalized = "image";
        }

        if (!char.IsAsciiLetter(normalized[0]) && normalized[0] != '_')
        {
            normalized = $"image_{normalized}";
        }

        return ScriptIdentifierSyntax.IsValidVariableName(normalized) ? normalized : "image";
    }
}
