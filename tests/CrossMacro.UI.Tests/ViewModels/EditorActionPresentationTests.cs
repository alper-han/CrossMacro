namespace CrossMacro.UI.Tests.ViewModels;

public sealed class EditorActionPresentationTests
{
    [Fact]
    public void EveryActionType_PreservesDefaultTextAndMeaningfulOverrides()
    {
        foreach (var type in Enum.GetValues<EditorActionType>())
        {
            var presentation = EditorActionPresentation.For(type);
            var expectedPosition = "Editor_CurrentPositionUse";
            if (type is EditorActionType.MouseClick) { expectedPosition = "Editor_CurrentPositionClick"; }
            else if (type is EditorActionType.MouseDown) { expectedPosition = "Editor_CurrentPositionHold"; }
            else if (type is EditorActionType.MouseUp) { expectedPosition = "Editor_CurrentPositionRelease"; }
            Assert.Equal(expectedPosition, presentation.CurrentPositionLabelKey);

            if (type is EditorActionType.RawScriptStep)
            {
                Assert.Equal("Editor_RawScriptStep", presentation.TextInputLabelKey);
                Assert.Equal("Editor_OriginalScriptLine", presentation.TextInputWatermarkKey);
                Assert.Equal("Editor_RawScriptHint", presentation.TextInputHintKey);
            }
            else if (type is EditorActionType.ClipboardSet)
            {
                Assert.Equal("Editor_ClipboardText", presentation.TextInputLabelKey);
                Assert.Equal("Editor_ClipboardTextPlaceholder", presentation.TextInputWatermarkKey);
                Assert.Equal("Editor_ClipboardSetHint", presentation.TextInputHintKey);
            }
            else
            {
                Assert.Equal("Editor_TextToType", presentation.TextInputLabelKey);
                Assert.Equal("Editor_EnterTextToType", presentation.TextInputWatermarkKey);
                Assert.Equal("Editor_TextToTypeHint", presentation.TextInputHintKey);
            }
        }
        Assert.Equal(EditorActionPresentation.For(EditorActionType.MouseMove), EditorActionPresentation.For(type: null));
    }

    [Fact]
    public void UndefinedActionType_IsRejected()
    {
        _ = Assert.Throws<InvalidOperationException>(() => EditorActionPresentation.For((EditorActionType)int.MaxValue));
    }

    [Fact]
    public void ActionTypeChange_InvalidatesNormalizationVisibilityVariablesPreviewAndText()
    {
        Assert.Equal(new EditorPresentationChanges(
            NormalizeAction: true, Visibility: true, ScreenReading: false, VariableNames: true,
            ImagePreview: true, TextInput: true, KeyName: false, Coordinates: false),
            EditorPresentationChanges.For(nameof(EditorAction.Type)));
    }

    [Fact]
    public void ImageAssetChange_InvalidatesVariableSuggestionsAndPreviewWithoutNormalizingAction()
    {
        Assert.Equal(new EditorPresentationChanges(
            NormalizeAction: false, Visibility: false, ScreenReading: false, VariableNames: true,
            ImagePreview: true, TextInput: false, KeyName: false, Coordinates: false),
            EditorPresentationChanges.For(nameof(EditorAction.ImageAssetName)));
    }

    [Fact]
    public void CoordinateChange_DoesNotRequestUnrelatedPresentationWork()
    {
        var expected = new EditorPresentationChanges(
            NormalizeAction: false, Visibility: false, ScreenReading: false, VariableNames: false,
            ImagePreview: false, TextInput: false, KeyName: false, Coordinates: true);
        Assert.Equal(expected, EditorPresentationChanges.For(nameof(EditorAction.IsAbsolute)));
        Assert.Equal(expected, EditorPresentationChanges.For(nameof(EditorAction.CoordinateSpace)));
        Assert.Equal(default, EditorPresentationChanges.For(nameof(EditorAction.Index)));
        Assert.Equal(default, EditorPresentationChanges.For(propertyName: null));
    }
}
