namespace CrossMacro.UI.ViewModels.Editor;

/// <summary>
/// Validates the editor's actions and turns them into the runtime macro used for saving, test playback and
/// adding to the playback session. Validation problems are reported through dialogs and the status line.
/// </summary>
internal sealed class EditorMacroBuilder(
    EditorViewModel owner,
    EditorDocumentSession document,
    IEditorActionConverter converter,
    IEditorActionValidator validator,
    IDialogService dialogService,
    EditorActionDisplayFormatter actionDisplayFormatter,
    IUiDispatcher uiDispatcher)
{
    /// <param name="targetActions">The actions to build, or null for the whole document.</param>
    internal async Task<MacroSequence?> BuildValidAsync(IReadOnlyList<EditorAction>? targetActions = null)
    {
        var actionsToValidate = targetActions ?? owner.Actions;
        if (actionsToValidate.Count is 0)
        {
            await dialogService.ShowMessageAsync(owner.Localize("Editor_DialogTitleNoActions"), owner.Localize("Editor_DialogMessageNoActions")).ConfigureAwait(false);
            return null;
        }

        var normalizedActions = EditorViewModel.CloneActions(actionsToValidate);
        EditorActionCoordinateRules.NormalizeCurrentPositionMouseButtons(normalizedActions);

        var (isValid, validationErrors) = validator.ValidateAll(normalizedActions);
        var errors = validationErrors.ToList();
        errors.AddRange(ValidateImageSearchAssets(normalizedActions));
        if (!isValid || errors.Count > 0)
        {
            await ReportValidationErrorsAsync(errors).ConfigureAwait(false);
            return null;
        }

        var firstCoordinateAction = normalizedActions.FirstOrDefault(action =>
            EditorActionCoordinateRules.UsesCoordinates(action.Type) && !EditorActionCoordinateRules.IsCurrentPositionMouseButton(action));
        var isAbsolute = firstCoordinateAction?.IsAbsolute ?? false;
        var skipInitialZeroZero = targetActions is null
            ? (owner.SkipInitialZeroZeroFlag || owner.RequiresSkipInitialZeroZero)
            : (normalizedActions.Exists(EditorActionCoordinateRules.IsCurrentPositionMouseButton) || owner.SkipInitialZeroZeroFlag);

        if (targetActions is null)
        {
            await uiDispatcher.RunOnUiThreadAsync(() => owner.ApplyResolvedSkipInitialZeroZero(skipInitialZeroZero)).ConfigureAwait(false);
        }

        var projection = new EditorMacroProjection(normalizedActions, owner.MacroName, isAbsolute, skipInitialZeroZero);
        var sequence = converter.ToMacroSequence(projection);
        if (sequence is null)
        {
            return null;
        }

        sequence.ReplaceImages(document.ImageAssets);
        return sequence;
    }

    private async Task ReportValidationErrorsAsync(IReadOnlyCollection<string> errors)
    {
        var errorMessage = $"{owner.Localize("Editor_ValidationErrorHeader")}\n\n{string.Join('\n', errors.Select(error => $"• {error}"))}";
        await dialogService.ShowMessageAsync(owner.Localize("Editor_DialogTitleValidationErrors"), errorMessage).ConfigureAwait(false);
        await uiDispatcher.RunOnUiThreadAsync(() => owner.Status = owner.LocalizeFormat("Editor_StatusValidationFailed", errors.Count)).ConfigureAwait(false);
    }

    private IEnumerable<string> ValidateImageSearchAssets(IEnumerable<EditorAction> actions)
    {
        var index = 0;
        foreach (var action in actions)
        {
            index++;
            if (action.Type is not (EditorActionType.ImageSearch or EditorActionType.ImageClick or EditorActionType.WaitImage))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(action.ImageAssetName) || !document.ImageAssets.ContainsKey(action.ImageAssetName))
            {
                yield return owner.LocalizeFormat(
                    "Editor_ValidationImageAssetMissing",
                    index,
                    actionDisplayFormatter.FormatActionType(action.Type),
                    action.ImageAssetName);
            }
        }
    }
}
