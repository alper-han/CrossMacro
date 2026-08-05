// Behavioral cluster extracted from the fixture to keep test ownership explicit.
namespace CrossMacro.UI.Tests.ViewModels;

public sealed partial class EditorViewModelTests
{

    [Theory]
    [MemberData(nameof(Task7BindingMembers))]
    public void Task7BindingMembers_RemainPublic(string memberName)
    {
        _ = typeof(EditorViewModel)
            .GetMember(memberName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
            .Should()
            .NotBeEmpty();
    }

    [Fact]
    public void SelectedActionDisplayText_ForTextInput_PreservesMultilineText()
    {
        // Arrange
        var action = new EditorAction
        {
            Type = EditorActionType.TextInput,
            Text = "\basd\r\nasd\t\\",
        };
        _viewModel.Actions.Add(action);
        _viewModel.SelectedAction = action;

        // Act / Assert
        _ = _viewModel.SelectedActionDisplayText.Should().Be("\basd\r\nasd\t\\");
    }

    [Fact]
    public void SelectedActionDisplayText_ForTextInput_SetsRawMultilineText()
    {
        // Arrange
        var action = new EditorAction { Type = EditorActionType.TextInput };
        _viewModel.Actions.Add(action);
        _viewModel.SelectedAction = action;

        // Act
        _viewModel.SelectedActionDisplayText = "first line\nsecond line\t\\";

        // Assert
        _ = action.Text.Should().Be("first line\nsecond line\t\\");
    }

    [Fact]
    public void TextInputAcceptsReturn_WhenSelectedActionIsTextInput_ReturnsTrue()
    {
        var action = new EditorAction { Type = EditorActionType.TextInput };
        _viewModel.Actions.Add(action);
        _viewModel.SelectedAction = action;

        _ = _viewModel.TextInputAcceptsReturn.Should().BeTrue();
    }

    [Fact]
    public void TextInputAcceptsReturn_WhenSelectedActionIsNonTextPayload_ReturnsFalse()
    {
        var action = new EditorAction { Type = EditorActionType.MouseClick };
        _viewModel.Actions.Add(action);
        _viewModel.SelectedAction = action;

        _ = _viewModel.TextInputAcceptsReturn.Should().BeFalse();
    }

    [Fact]
    public void SelectedActionDisplayText_WhenSelectedActionChanges_RaisesPropertyChanged()
    {
        // Arrange
        var action = new EditorAction
        {
            Type = EditorActionType.TextInput,
            Text = "\b",
        };
        var changed = new List<string?>();
        _viewModel.PropertyChanged += (_, args) => changed.Add(args.PropertyName);

        // Act
        _viewModel.Actions.Add(action);
        _viewModel.SelectedAction = action;

        // Assert
        _ = changed.Should().Contain(nameof(EditorViewModel.SelectedActionDisplayText));
        _ = _viewModel.SelectedActionDisplayText.Should().Be("\b");
    }

    [Fact]
    public void AddableActionGroups_ExcludeInternalAndAdvancedActions()
    {
        var groupedActionTypes = _viewModel.AddableActionGroups
            .SelectMany(group => group.Choices)
            .Select(choice => choice.ActionType)
            .ToArray();

        _ = groupedActionTypes.Should().Contain(EditorViewModel.AddableActionTypes);
        _ = groupedActionTypes.Should().OnlyHaveUniqueItems();
        _ = groupedActionTypes.Should().NotContain(EditorActionType.RawScriptStep);
        _ = groupedActionTypes.Should().NotContain(EditorActionType.BlockEnd);
        _ = groupedActionTypes.Should().NotContain(EditorActionType.ElseBlockStart);
        _ = _viewModel.AddableActionGroups.Select(group => group.DisplayName).Should().NotContain("Advanced");
    }

    [Fact]
    public void AddableActionGroups_IncludesClipboardActions()
    {
        var clipboardGroup = _viewModel.AddableActionGroups.Single(group =>
            group.Choices.Any(choice => choice.ActionType is EditorActionType.ClipboardGet));

        _ = clipboardGroup.Choices.Select(choice => choice.ActionType).Should().Equal(
            EditorActionType.ClipboardGet,
            EditorActionType.ClipboardSet);
    }

    [Fact]
    public void AddableActionGroups_IncludesSingleShellCommandAction()
    {
        var shellGroup = _viewModel.AddableActionGroups.Single(group =>
            group.Choices.Any(choice => choice.ActionType is EditorActionType.ShellCommand));

        _ = shellGroup.Choices.Select(choice => choice.ActionType).Should().Equal(EditorActionType.ShellCommand);
    }

    [Fact]
    public void AddableActionGroups_IncludesSingleWindowCommandAction()
    {
        var windowGroup = _viewModel.AddableActionGroups.Single(group =>
            group.Choices.Any(choice => choice.ActionType is EditorActionType.WindowCommand));

        _ = windowGroup.Choices.Select(choice => choice.ActionType).Should().Equal(EditorActionType.WindowCommand);
    }

    [Fact]
    public void AddableActionGroups_IncludesSingleScreenshotAction()
    {
        var screenshotGroup = _viewModel.AddableActionGroups.Single(group =>
            group.Choices.Any(choice => choice.ActionType is EditorActionType.Screenshot));

        _ = screenshotGroup.Choices.Select(choice => choice.ActionType).Should().Equal(EditorActionType.Screenshot);
    }

    [Fact]
    public void AddAction_ForShellCommand_InitializesDefaultsAndShowsShellFields()
    {
        _viewModel.NewActionType = EditorActionType.ShellCommand;

        _viewModel.AddAction();

        var action = _viewModel.Actions.Should().ContainSingle().Subject;
        _ = action.Type.Should().Be(EditorActionType.ShellCommand);
        _ = action.ShellCommandMode.Should().Be(ShellCommandMode.Shell);
        _ = action.ShellCommand.Should().Be("echo hello");
        _ = action.ShellExitCodeVariableName.Should().Be("exit_code");
        _ = _viewModel.ShowShellCommandFields.Should().BeTrue();
        _ = _viewModel.ShowShellStandardInputFields.Should().BeFalse();
        _ = _viewModel.ShowShellCaptureFields.Should().BeFalse();
    }

    [Fact]
    public void AddAction_ForWindowCommand_InitializesDefaultsAndShowsWindowFields()
    {
        _viewModel.NewActionType = EditorActionType.WindowCommand;

        _viewModel.AddAction();

        var action = _viewModel.Actions.Should().ContainSingle().Subject;
        _ = action.Type.Should().Be(EditorActionType.WindowCommand);
        _ = action.WindowCommandMode.Should().Be(WindowCommandMode.Active);
        _ = action.WindowActiveField.Should().Be("title");
        _ = action.WindowOutputVariable.Should().Be("windowResult");
        _ = _viewModel.ShowWindowCommandFields.Should().BeTrue();
        _ = _viewModel.ShowWindowActiveFieldSelector.Should().BeTrue();
        _ = _viewModel.ShowWindowOutputVariableField.Should().BeTrue();
    }

    [Theory]
    [InlineData(WindowCommandMode.Search, true, true, false, false, false, true, false, false, false, true, false)]
    [InlineData(WindowCommandMode.Wait, true, true, false, false, true, true, false, false, false, true, false)]
    [InlineData(WindowCommandMode.Focus, true, false, true, false, false, false, false, false, false, true, false)]
    [InlineData(WindowCommandMode.Close, true, false, false, true, false, false, false, false, false, true, false)]
    [InlineData(WindowCommandMode.Move, false, false, false, false, false, false, true, false, false, false, false)]
    [InlineData(WindowCommandMode.Resize, false, false, false, false, false, false, false, true, false, false, false)]
    [InlineData(WindowCommandMode.WorkspaceSwitch, false, false, false, false, false, false, false, false, true, false, false)]
    [InlineData(WindowCommandMode.WorkspaceMoveWindow, false, false, false, false, false, false, false, false, true, false, true)]
    public void SelectedAction_ForWindowMode_TogglesModeSpecificFields(
        WindowCommandMode mode,
        bool showSelector,
        bool showSearchKinds,
        bool showFocusKinds,
        bool showCloseKinds,
        bool showTimeout,
        bool showOutput,
        bool showCoordinate,
        bool showDimension,
        bool showWorkspace,
        bool showSelectorValue,
        bool showAddress)
    {
        var action = new EditorAction
        {
            Type = EditorActionType.WindowCommand,
            WindowCommandMode = mode,
            WindowSelectorKind = "title",
            WindowSelectorValue = "Firefox",
            WindowWorkspace = "2",
        };
        _viewModel.Actions.Add(action);
        _viewModel.SelectedAction = action;

        _ = _viewModel.ShowWindowCommandFields.Should().BeTrue();
        _ = _viewModel.ShowWindowSelectorFields.Should().Be(showSelector);
        _ = _viewModel.ShowWindowSearchSelectorKinds.Should().Be(showSearchKinds);
        _ = _viewModel.ShowWindowFocusSelectorKinds.Should().Be(showFocusKinds);
        _ = _viewModel.ShowWindowCloseSelectorKinds.Should().Be(showCloseKinds);
        _ = _viewModel.ShowWindowTimeoutField.Should().Be(showTimeout);
        _ = _viewModel.ShowWindowOutputVariableField.Should().Be(showOutput);
        _ = _viewModel.ShowWindowCoordinateFields.Should().Be(showCoordinate);
        _ = _viewModel.ShowWindowDimensionFields.Should().Be(showDimension);
        _ = _viewModel.ShowWindowWorkspaceField.Should().Be(showWorkspace);
        _ = _viewModel.ShowWindowSelectorValueField.Should().Be(showSelectorValue);
        _ = _viewModel.ShowWindowAddressField.Should().Be(showAddress);
    }

    [Fact]
    public void AddAction_ForScreenshot_InitializesClipboardDefaultAndShowsScreenshotFields()
    {
        _viewModel.NewActionType = EditorActionType.Screenshot;

        _viewModel.AddAction();

        var action = _viewModel.Actions.Should().ContainSingle().Subject;
        _ = action.Type.Should().Be(EditorActionType.Screenshot);
        _ = action.ScreenshotCopyToClipboard.Should().BeTrue();
        _ = action.ScreenshotOutputPath.Should().BeEmpty();
        _ = _viewModel.ShowScreenshotFields.Should().BeTrue();
        _ = _viewModel.ShowScreenshotRegionFields.Should().BeFalse();
    }

    [Fact]
    public void SelectedAction_ForScreenshotUseRegion_TogglesRegionFields()
    {
        var action = new EditorAction { Type = EditorActionType.Screenshot };
        _viewModel.Actions.Add(action);
        _viewModel.SelectedAction = action;

        _ = _viewModel.ShowScreenshotFields.Should().BeTrue();
        _ = _viewModel.ShowScreenshotRegionFields.Should().BeFalse();

        action.ScreenshotUseRegion = true;

        _ = _viewModel.ShowScreenshotRegionFields.Should().BeTrue();
    }

    [Theory]
    [InlineData(EditorActionType.ImageSearch, false)]
    [InlineData(EditorActionType.ImageClick, true)]
    [InlineData(EditorActionType.WaitImage, false)]
    public void SelectedAction_WhenImageActionSelected_TogglesMouseButton(EditorActionType actionType, bool expected)
    {
        var action = new EditorAction { Type = actionType };
        _viewModel.Actions.Add(action);

        _viewModel.SelectedAction = action;

        _ = _viewModel.ShowMouseButton.Should().Be(expected);
    }

    [Fact]
    public void ImageClickButtons_ExposeOnlySupportedGrammarButtons()
    {
        _ = _viewModel.ImageClickButtons.Should().Equal(MacroMouseButton.Left, MacroMouseButton.Right, MacroMouseButton.Middle);
    }

    [Fact]
    public async Task ImportImageAssetAsync_WhenPngExceedsSupportedDimensions_ShowsErrorAndLeavesAssetsEmpty()
    {
        var pngPath = Path.Combine(Path.GetTempPath(), $"crossmacro-target-{Guid.NewGuid():N}.png");
        await File.WriteAllBytesAsync(pngPath, CreateOversizedPngBytes(), NonCancelableToken);
        try
        {
            _ = _dialogService
                .ShowOpenFileDialogAsync(Arg.Any<string>(), Arg.Any<FileDialogFilter[]>())
                .Returns(pngPath);

            await _viewModel.ImportImageAssetAsync();

            _ = _viewModel.HasImageAssets.Should().BeFalse();
            _ = _viewModel.ImageAssetNames.Should().BeEmpty();
            _ = _viewModel.Status.Should().Contain("Editor_StatusImageImportError");
        }
        finally
        {
            File.Delete(pngPath);
        }
    }

    [Theory]
    [InlineData(ShellCommandMode.Shell, false, false)]
    [InlineData(ShellCommandMode.ShellCapture, false, true)]
    [InlineData(ShellCommandMode.ShellInput, true, false)]
    [InlineData(ShellCommandMode.ShellCaptureInput, true, true)]
    public void SelectedAction_ForShellMode_TogglesModeSpecificFields(ShellCommandMode mode, bool showInput, bool showCapture)
    {
        var action = new EditorAction
        {
            Type = EditorActionType.ShellCommand,
            ShellCommandMode = mode,
            ShellCommand = "echo ok",
        };
        _viewModel.Actions.Add(action);
        _viewModel.SelectedAction = action;

        _ = _viewModel.ShowShellCommandFields.Should().BeTrue();
        _ = _viewModel.ShowShellStandardInputFields.Should().Be(showInput);
        _ = _viewModel.ShowShellCaptureFields.Should().Be(showCapture);
    }

    [Theory]
    [InlineData(EditorActionType.PixelColor)]
    [InlineData(EditorActionType.WaitColor)]
    [InlineData(EditorActionType.PixelSearch)]
    public void AddAction_ForScreenReadingActions_InitializesStructuredFields(EditorActionType actionType)
    {
        _viewModel.NewActionType = actionType;

        _viewModel.AddAction();

        var action = _viewModel.Actions.Should().ContainSingle().Subject;
        _ = action.Type.Should().Be(actionType);
        _ = action.ScreenColorHex.Should().Be("FFFFFF");
        _ = action.ScreenColorVariableName.Should().Be(actionType is EditorActionType.WaitColor ? "wait_ok" : "color");
        _ = action.ScreenFoundXVariableName.Should().Be("found_x");
        _ = action.ScreenFoundYVariableName.Should().Be("found_y");
        _ = action.ScreenTimeoutMs.Should().Be(5000);
        _ = action.ScreenTolerance.Should().Be(0);
        _ = action.ScreenWidth.Should().Be(actionType is EditorActionType.PixelSearch ? 1920 : 1);
        _ = action.ScreenHeight.Should().Be(actionType is EditorActionType.PixelSearch ? 1080 : 1);
        _ = _viewModel.SelectedAction.Should().BeSameAs(action);
    }

    [Theory]
    [InlineData(EditorActionType.PixelColor, true, false, false, true, false, true)]
    [InlineData(EditorActionType.WaitColor, false, true, false, true, true, true)]
    [InlineData(EditorActionType.PixelSearch, false, false, true, true, true, false)]
    public void SelectedAction_ForScreenReadingActions_ExposesMatchingEditorPanel(
        EditorActionType actionType,
        bool showPixelColor,
        bool showWaitColor,
        bool showPixelSearch,
        bool showScreenReadingFields,
        bool showScreenReadingColorFields,
        bool showScreenReadingPointFields)
    {
        var action = new EditorAction { Type = actionType, ScreenColorHex = "A1B2C3" };
        _viewModel.Actions.Add(action);

        _viewModel.SelectedAction = action;

        _ = _viewModel.ShowPixelColorFields.Should().Be(showPixelColor);
        _ = _viewModel.ShowWaitColorFields.Should().Be(showWaitColor);
        _ = _viewModel.ShowPixelSearchFields.Should().Be(showPixelSearch);
        _ = _viewModel.ShowScreenReadingFields.Should().Be(showScreenReadingFields);
        _ = _viewModel.ShowScreenReadingColorFields.Should().Be(showScreenReadingColorFields);
        _ = _viewModel.ShowScreenReadingPointFields.Should().Be(showScreenReadingPointFields);
        _ = _viewModel.ShowScreenReadingColorPreview.Should().Be(actionType is EditorActionType.WaitColor or EditorActionType.PixelSearch);
        _ = _viewModel.ScreenReadingColorPreviewHex.Should().Be(actionType is EditorActionType.PixelColor ? string.Empty : "A1B2C3");
        _ = _viewModel.ShowScreenReadingRawAssistance.Should().BeFalse();
        _ = _viewModel.ScreenReadingRawHint.Should().BeEmpty();
    }

    [Fact]
    public void ScreenReadingFields_WhenSelectedActionIsNotScreenReading_AreHidden()
    {
        var action = new EditorAction { Type = EditorActionType.TextInput };
        _viewModel.Actions.Add(action);

        _viewModel.SelectedAction = action;

        _ = _viewModel.ShowScreenReadingFields.Should().BeFalse();
    }

    [Theory]
    [InlineData(EditorActionType.WaitColor)]
    [InlineData(EditorActionType.PixelSearch)]
    public void ScreenTargetColorSource_WhenVariableOrManualChanges_TogglesHexAndVariableVisibility(EditorActionType actionType)
    {
        var action = new EditorAction
        {
            Type = actionType,
            ScreenColorHex = "FFFFFF",
        };
        _viewModel.Actions.Add(action);
        _viewModel.SelectedAction = action;

        action.ScreenTargetColorSource = EditorActionScreenTargetColorSource.Variable;

        _ = _viewModel.ShowScreenTargetColorHexInput.Should().BeFalse();
        _ = _viewModel.ShowScreenTargetColorVariableInput.Should().BeTrue();
        _ = _viewModel.ShowScreenReadingColorPreview.Should().BeFalse();

        action.ScreenTargetColorSource = EditorActionScreenTargetColorSource.ManualHex;

        _ = _viewModel.ShowScreenTargetColorHexInput.Should().BeTrue();
        _ = _viewModel.ShowScreenTargetColorVariableInput.Should().BeFalse();
        _ = _viewModel.ShowScreenReadingColorPreview.Should().BeTrue();
    }

    [Theory]
    [InlineData("window active title activeTitle", "Editor_RawScriptHint_Window", "localized window hint")]
    [InlineData("clipboard set \"hello\"", "Editor_RawScriptHint_Clipboard", "localized clipboard hint")]
    [InlineData("shell \"notify-send done\" 1 250 5000", "Editor_RawScriptHint_Shell", "localized shell hint")]
    [InlineData("pixelcolor rel 1 2 sampled", "Editor_RawScreenReadingHint_PixelColor", "localized pixelcolor hint")]
    [InlineData("waitcolor 10 20 00FF00 1000 wait_ok", "Editor_RawScreenReadingHint_WaitColor", "localized waitcolor hint")]
    [InlineData("pixelsearch 0 0 100 100 00FF00 found", "Editor_RawScreenReadingHint_PixelSearch", "localized pixelsearch hint")]
    public void TextInputHint_ForRawScriptStep_UsesLocalizedHint(
        string rawStep,
        string resourceKey,
        string expectedHint)
    {
        _ = _localizationService[resourceKey].Returns(expectedHint);
        var action = new EditorAction
        {
            Type = EditorActionType.RawScriptStep,
            Text = rawStep,
        };
        _viewModel.Actions.Add(action);
        _viewModel.SelectedAction = action;

        _ = _viewModel.TextInputHint.Should().Be(expectedHint);
    }

    [Fact]
    public void ScreenReadingRawHint_ForScreenReadingRawScript_StillUsesLocalizedHint()
    {
        _ = _localizationService["Editor_RawScreenReadingHint_WaitColor"].Returns("localized waitcolor hint");
        var action = new EditorAction
        {
            Type = EditorActionType.RawScriptStep,
            Text = "waitcolor 10 20 00FF00 1000 wait_ok",
        };
        _viewModel.Actions.Add(action);
        _viewModel.SelectedAction = action;

        _ = _viewModel.ShowScreenReadingRawAssistance.Should().BeTrue();
        _ = _viewModel.ScreenReadingRawHint.Should().Be("localized waitcolor hint");
    }

    [Fact]
    public void SelectedActionDisplayText_WhenRawScriptTextChanges_RaisesTextInputHintNotification()
    {
        _ = _localizationService["Editor_RawScriptHint_Window"].Returns("window hint");
        _ = _localizationService["Editor_RawScriptHint_Clipboard"].Returns("clipboard hint");

        var action = new EditorAction
        {
            Type = EditorActionType.RawScriptStep,
            Text = "window active title activeTitle",
        };
        var changed = new List<string?>();
        _viewModel.PropertyChanged += (_, args) => changed.Add(args.PropertyName);

        _viewModel.Actions.Add(action);
        _viewModel.SelectedAction = action;
        changed.Clear();

        action.Text = "clipboard set \"hello\"";

        _ = changed.Should().Contain(nameof(EditorViewModel.TextInputHint));
        _ = _viewModel.TextInputHint.Should().Be("clipboard hint");
    }

    [Fact]
    public void ConditionOperandTextBoxes_WhenNoVariablesExist_RemainVisibleForManualVariableEntry()
    {
        var action = new EditorAction
        {
            Type = EditorActionType.IfBlockStart,
            ScriptLeftOperandType = ScriptOperandType.VariableReference,
            ScriptRightOperandType = ScriptOperandType.VariableReference,
        };
        _viewModel.Actions.Add(action);
        _viewModel.SelectedAction = action;

        _ = _viewModel.ShowConditionLeftVariablePicker.Should().BeFalse();
        _ = _viewModel.ShowConditionLeftOperandTextBox.Should().BeTrue();
        _ = _viewModel.ShowConditionRightVariablePicker.Should().BeFalse();
        _ = _viewModel.ShowConditionRightOperandTextBox.Should().BeTrue();
    }

    [Fact]
    public void ConditionColorPickers_WhenColorOperandsSelected_AreVisibleWithManualTextBoxes()
    {
        var action = new EditorAction
        {
            Type = EditorActionType.IfBlockStart,
            ScriptLeftOperandType = ScriptOperandType.Color,
            ScriptRightOperandType = ScriptOperandType.Color,
        };
        _viewModel.Actions.Add(action);
        _viewModel.SelectedAction = action;

        _ = _viewModel.ShowConditionLeftOperandTextBox.Should().BeTrue();
        _ = _viewModel.ShowConditionRightOperandTextBox.Should().BeTrue();
        _ = _viewModel.ShowConditionLeftColorPicker.Should().BeTrue();
        _ = _viewModel.ShowConditionRightColorPicker.Should().BeTrue();
    }

    [Fact]
    public void CultureChanged_RefreshesLocalizedComputedPropertiesAndActionListPresentation()
    {
        _viewModel.NewActionType = EditorActionType.IfBlockStart;
        _viewModel.AddAction();

        _ = _localizationService["Editor_CurrentPositionUse"].Returns("[Editor_CurrentPositionUse:updated]");
        _ = _localizationService["Editor_TextToType"].Returns("[Editor_TextToType:updated]");
        _ = _localizationService["Editor_EnterTextToType"].Returns("[Editor_EnterTextToType:updated]");
        _ = _localizationService["Editor_TextToTypeHint"].Returns("[Editor_TextToTypeHint:updated] {0}");
        _ = _localizationService["Editor_BlockName_If"].Returns("IfTokenUpdated");

        _localizationService.CultureChanged += Raise.Event<EventHandler>(_localizationService, EventArgs.Empty);

        _ = _viewModel.CurrentPositionToggleLabel.Should().Be("[Editor_CurrentPositionUse:updated]");
        _ = _viewModel.TextInputLabel.Should().Be("[Editor_TextToType:updated]");
        _ = _viewModel.TextInputWatermark.Should().Be("[Editor_EnterTextToType:updated]");
        _ = _viewModel.TextInputHint.Should().Contain("[Editor_TextToTypeHint:updated]");
        _ = _viewModel.ActionListItems[1].DisplayName.Should().Be("End IfTokenUpdated");
    }

    [Fact]
    public void CultureChanged_RefreshesLocalizedRawScreenReadingHint()
    {
        var rawAction = new EditorAction
        {
            Type = EditorActionType.RawScriptStep,
            Text = "waitcolor 10 20 00FF00 1000 wait_ok",
        };
        _ = _localizationService["Editor_RawScreenReadingHint_WaitColor"].Returns("initial raw hint");
        _viewModel.Actions.Add(rawAction);
        _viewModel.SelectedAction = rawAction;
        _ = _viewModel.ScreenReadingRawHint.Should().Be("initial raw hint");
        var changed = new List<string?>();
        _viewModel.PropertyChanged += (_, args) => changed.Add(args.PropertyName);

        _ = _localizationService["Editor_RawScreenReadingHint_WaitColor"].Returns("updated raw hint");
        _localizationService.CultureChanged += Raise.Event<EventHandler>(_localizationService, EventArgs.Empty);

        _ = _viewModel.ScreenReadingRawHint.Should().Be("updated raw hint");
        _ = changed.Should().Contain(nameof(EditorViewModel.ScreenReadingRawHint));

    }

    [Fact]
    public void CultureChanged_RefreshesLocalizedConditionHintNotification()
    {
        var action = new EditorAction
        {
            Type = EditorActionType.IfBlockStart,
            ScriptLeftOperandType = ScriptOperandType.Number,
            ScriptRightOperandType = ScriptOperandType.Number,
        };
        _viewModel.Actions.Add(action);
        _viewModel.SelectedAction = action;
        action.ScriptRightOperandType = ScriptOperandType.Color;
        _ = _localizationService["Editor_ConditionColorHint"].Returns("updated condition hint");
        var changed = new List<string?>();
        _viewModel.PropertyChanged += (_, args) => changed.Add(args.PropertyName);

        _localizationService.CultureChanged += Raise.Event<EventHandler>(_localizationService, EventArgs.Empty);

        _ = _viewModel.ConditionRightOperandHint.Should().Be("updated condition hint");
        _ = changed.Should().Contain(nameof(EditorViewModel.ConditionRightOperandHint));
    }

    [Fact]
    public void CultureChanged_WhenMovementRunIsCondensed_RefreshesCondensedDisplayAndHint()
    {
        for (var index = 0; index < 6; index++)
        {
            _viewModel.Actions.Add(new EditorAction
            {
                Type = EditorActionType.MouseMove,
                X = index,
                Y = index + 1,
            });
        }

        _viewModel.SimplifyMovement = true;
        var originalDisplay = _viewModel.ActionListItems[0].DisplayName;
        var originalHint = _viewModel.ActionListItems[0].CondensedHint;

        _ = _localizationService["Editor_Action_MouseMoveAbsolute"].Returns("Mouvement vers ({0}, {1})");
        _ = _localizationService["Editor_SimplifiedMovementHint"].Returns("{0} actions de mouvement masquées");

        _localizationService.CultureChanged += Raise.Event<EventHandler>(_localizationService, EventArgs.Empty);

        _ = _viewModel.ActionListItems.Should().ContainSingle();
        _ = _viewModel.ActionListItems[0].DisplayName.Should().NotBe(originalDisplay);
        _ = _viewModel.ActionListItems[0].CondensedHint.Should().NotBe(originalHint);
        _ = _viewModel.ActionListItems[0].DisplayName.Should().Be("Mouvement vers (5, 6)");
        _ = _viewModel.ActionListItems[0].CondensedHint.Should().Be("5 actions de mouvement masquées");
        _ = _viewModel.ActionListItems[0].DisplayTooltip.Should().Be("5 actions de mouvement masquées");
    }

    [Fact]
    public void CultureChanged_WhenReadyStatusDisplayed_RebuildsReadyStatusInNewLanguage()
    {
        _ = _localizationService["Editor_StatusReady"].Returns("[Editor_StatusReady:updated]");

        _localizationService.CultureChanged += Raise.Event<EventHandler>(_localizationService, EventArgs.Empty);

        _ = _viewModel.Status.Should().Be("[Editor_StatusReady:updated]");
    }

    [Fact]
    public void AddableActionTypes_HidesManagedBlockTokens()
    {
        _ = EditorViewModel.AddableActionTypes.Should().NotContain(EditorActionType.BlockEnd);
        _ = EditorViewModel.AddableActionTypes.Should().NotContain(EditorActionType.ElseBlockStart);
        _ = EditorViewModel.AddableActionTypes.Should().NotContain(EditorActionType.RawScriptStep);
    }

    [Fact]
    public void AddableActionTypes_ContainsLoopControlActions()
    {
        _ = EditorViewModel.AddableActionTypes.Should().Contain(EditorActionType.Break);
        _ = EditorViewModel.AddableActionTypes.Should().Contain(EditorActionType.Continue);
    }

    [Fact]
    public void ActionListItems_ByDefault_ShowsMouseMovesAndShortDelays()
    {
        _viewModel.Actions.Add(new EditorAction { Type = EditorActionType.MouseMove, X = 100, Y = 200 });
        _viewModel.Actions.Add(new EditorAction { Type = EditorActionType.Delay, DelayMs = 4 });
        _viewModel.Actions.Add(new EditorAction { Type = EditorActionType.MouseClick, X = 100, Y = 200 });

        _ = _viewModel.HideMouseMoves.Should().BeFalse();
        _ = _viewModel.HideShortWaits.Should().BeFalse();
        _ = _viewModel.HiddenEventCount.Should().Be(0);
        _ = _viewModel.HasHiddenEvents.Should().BeFalse();
        _ = _viewModel.ActionListItems.Should().HaveCount(3);
        _ = _viewModel.ActionListItems[0].IsNoise.Should().BeTrue();
        _ = _viewModel.ActionListItems[1].IsNoise.Should().BeTrue();
        _ = _viewModel.ActionListItems[2].IsNoise.Should().BeFalse();
    }

    [Fact]
    public void ActionListItems_WhenMovementAndShortWaitsHidden_ExcludesFilteredRows()
    {
        _viewModel.Actions.Add(new EditorAction { Type = EditorActionType.MouseMove, X = 100, Y = 200 });
        _viewModel.Actions.Add(new EditorAction { Type = EditorActionType.Delay, DelayMs = 4 });
        _viewModel.Actions.Add(new EditorAction { Type = EditorActionType.Delay, DelayMs = 20 });

        HideMovementAndShortWaitRows();

        _ = _viewModel.HiddenEventCount.Should().Be(2);
        _ = _viewModel.HasHiddenEvents.Should().BeTrue();
        _ = _viewModel.ActionListItems.Should().ContainSingle();
        _ = _viewModel.ActionListItems[0].Action.Type.Should().Be(EditorActionType.Delay);
        _ = _viewModel.ActionListItems[0].Action.DelayMs.Should().Be(20);
    }

    [Fact]
    public void ActionListItems_ByDefault_ProjectsAllRowsAndPreservesActionReferences()
    {
        var move = new EditorAction { Type = EditorActionType.MouseMove, X = 100, Y = 200 };
        var delay = new EditorAction { Type = EditorActionType.Delay, DelayMs = 4 };
        var click = new EditorAction { Type = EditorActionType.MouseClick, X = 100, Y = 200 };

        _viewModel.Actions.Add(move);
        _viewModel.Actions.Add(delay);
        _viewModel.Actions.Add(click);

        _ = _viewModel.ActionListItems.Should().HaveCount(3);
        _ = _viewModel.ActionListItems.Select(item => item.Action).Should().Equal(move, delay, click);
        _ = _viewModel.ActionListItems.Should().OnlyContain(item => item.RepresentsSourceAction);
    }

    [Fact]
    public void ActionListItems_ForTextInput_EscapesControlCharactersInDisplayName()
    {
        // Arrange
        _viewModel.Actions.Add(new EditorAction
        {
            Type = EditorActionType.TextInput,
            Text = "\basd\r\nasd\t",
        });

        // Act / Assert
        _ = _viewModel.ActionListItems.Should().ContainSingle();
        _ = _viewModel.ActionListItems[0].DisplayName.Should().Be("Type \"⌫asd↵↵asd⇥\"");
    }

    [Theory]
    [InlineData(EditorActionType.MouseClick, true, "Editor_Action_MouseClickAbsolute")]
    [InlineData(EditorActionType.MouseClick, false, "Editor_Action_MouseClickRelative")]
    [InlineData(EditorActionType.MouseDown, true, "Editor_Action_MouseDownAbsolute")]
    [InlineData(EditorActionType.MouseDown, false, "Editor_Action_MouseDownRelative")]
    [InlineData(EditorActionType.MouseUp, true, "Editor_Action_MouseUpAbsolute")]
    [InlineData(EditorActionType.MouseUp, false, "Editor_Action_MouseUpRelative")]
    public void ActionListItems_ForCoordinateMouseButtonActions_ShowsCoordinateModeAndPosition(
        EditorActionType actionType,
        bool isAbsolute,
        string expectedResourceKey)
    {
        var action = new EditorAction
        {
            Type = actionType,
            IsAbsolute = isAbsolute,
            X = isAbsolute ? 100 : 5,
            Y = isAbsolute ? 200 : -3,
        };

        _viewModel.Actions.Add(action);

        _ = _viewModel.ActionListItems.Should().ContainSingle();
        _ = _viewModel.ActionListItems[0].DisplayName.Should().Be(expectedResourceKey);
    }

    [Theory]
    [InlineData(EditorActionType.MouseDown, "Editor_Action_MouseDownCurrent")]
    [InlineData(EditorActionType.MouseUp, "Editor_Action_MouseUpCurrent")]
    public void ActionListItems_ForCurrentPositionMouseButtonActions_ShowsCurrentPositionMode(
        EditorActionType actionType,
        string expectedResourceKey)
    {
        _viewModel.Actions.Add(new EditorAction
        {
            Type = actionType,
            UseCurrentPosition = true,
            IsAbsolute = false,
        });

        _ = _viewModel.ActionListItems.Should().ContainSingle();
        _ = _viewModel.ActionListItems[0].DisplayName.Should().Be(expectedResourceKey);
    }

    [Fact]
    public void ActionListItems_WhenNoiseVisible_ExposeZeroBasedUnderlyingIndexAndOneBasedIndex()
    {
        _viewModel.Actions.Add(new EditorAction { Type = EditorActionType.MouseMove, X = 100, Y = 200 });
        _viewModel.Actions.Add(new EditorAction { Type = EditorActionType.Delay, DelayMs = 4 });
        _viewModel.Actions.Add(new EditorAction { Type = EditorActionType.MouseClick, X = 100, Y = 200 });

        _ = _viewModel.ActionListItems.Select(item => item.UnderlyingIndex).Should().Equal(0, 1, 2);
        _ = _viewModel.ActionListItems.Select(item => item.Index).Should().Equal(1, 2, 3);
        _ = _viewModel.ActionListItems.Select(item => item.CondensedHiddenCount).Should().Equal(0, 0, 0);
    }

    [Fact]
    public void ActionListItems_WhenMovementAndShortWaitsHidden_PreservesOriginalIndicesAndLeavesCondensedHiddenCountZero()
    {
        _viewModel.Actions.Add(new EditorAction { Type = EditorActionType.MouseMove, X = 100, Y = 200 });
        _viewModel.Actions.Add(new EditorAction { Type = EditorActionType.Delay, DelayMs = 4 });
        _viewModel.Actions.Add(new EditorAction { Type = EditorActionType.MouseClick, X = 100, Y = 200 });
        _viewModel.Actions.Add(new EditorAction { Type = EditorActionType.Delay, DelayMs = 8 });
        _viewModel.Actions.Add(new EditorAction { Type = EditorActionType.Delay, DelayMs = 20 });

        HideMovementAndShortWaitRows();

        _ = _viewModel.HiddenEventCount.Should().Be(3);
        _ = _viewModel.ActionListItems.Should().HaveCount(2);
        _ = _viewModel.ActionListItems.Select(item => item.UnderlyingIndex).Should().Equal(2, 4);
        _ = _viewModel.ActionListItems.Select(item => item.Index).Should().Equal(3, 5);
        _ = _viewModel.ActionListItems.Select(item => item.CondensedHiddenCount).Should().Equal(0, 0);
    }

    [Fact]
    public void SelectedActionUnderlyingIndices_WhenProjectionHidesPrimaryRow_SelectsFirstVisibleSelectedAction()
    {
        var move = new EditorAction { Type = EditorActionType.MouseMove, X = 100, Y = 200 };
        var delay = new EditorAction { Type = EditorActionType.Delay, DelayMs = 4 };
        var click = new EditorAction { Type = EditorActionType.MouseClick, X = 100, Y = 200 };

        _viewModel.Actions.Add(move);
        _viewModel.Actions.Add(delay);
        _viewModel.Actions.Add(click);
        _viewModel.SelectedActionUnderlyingIndices.Add(0);
        _viewModel.SelectedActionUnderlyingIndices.Add(2);

        HideMovementAndShortWaitRows();

        _ = _viewModel.SelectedActionUnderlyingIndices.Should().Equal(0, 2);
        _ = _viewModel.SelectedAction.Should().BeSameAs(click);
        _ = _viewModel.SelectedActionListItem.Should().BeSameAs(_viewModel.ActionListItems[0]);
    }

    [Fact]
    public void FilterToggleVisibility_HidesUnavailableInactiveFiltersAndKeepsActiveFiltersVisible()
    {
        _ = _viewModel.ShowHideMouseMovesToggle.Should().BeFalse();
        _ = _viewModel.ShowHideShortWaitsToggle.Should().BeFalse();
        _ = _viewModel.ShowSimplifyMovementToggle.Should().BeFalse();

        _viewModel.HideMouseMoves = true;
        _viewModel.HideShortWaits = true;
        _viewModel.SimplifyMovement = true;

        _ = _viewModel.ShowHideMouseMovesToggle.Should().BeTrue();
        _ = _viewModel.ShowHideShortWaitsToggle.Should().BeTrue();
        _ = _viewModel.ShowSimplifyMovementToggle.Should().BeTrue();
    }

    [Fact]
    public void FilterToggleVisibility_ShowsOnlyFiltersWithEligibleEvents()
    {
        _viewModel.Actions.Add(new EditorAction { Type = EditorActionType.MouseMove, X = 1, Y = 1 });
        _ = _viewModel.ShowHideMouseMovesToggle.Should().BeTrue();
        _ = _viewModel.ShowHideShortWaitsToggle.Should().BeFalse();
        _ = _viewModel.ShowSimplifyMovementToggle.Should().BeFalse();

        _viewModel.Actions.Add(new EditorAction { Type = EditorActionType.Delay, DelayMs = 5 });
        _ = _viewModel.ShowHideShortWaitsToggle.Should().BeTrue();
        _ = _viewModel.ShowSimplifyMovementToggle.Should().BeFalse();

        AddCondensibleRun(_viewModel, 6);

        _ = _viewModel.ShowSimplifyMovementToggle.Should().BeTrue();
    }

    [Fact]
    public void FilterToggleVisibility_UpdatesWhenActionEligibilityChanges()
    {
        var action = new EditorAction { Type = EditorActionType.Delay, DelayMs = 10 };
        _viewModel.Actions.Add(action);
        _ = _viewModel.ShowHideShortWaitsToggle.Should().BeFalse();

        action.DelayMs = 5;
        _ = _viewModel.ShowHideShortWaitsToggle.Should().BeTrue();

        action.UseRandomDelay = true;
        _ = _viewModel.ShowHideShortWaitsToggle.Should().BeFalse();
    }

    [Fact]
    public void ShowDeleteHiddenEvents_RequiresActiveHideFilterAndHiddenCandidates()
    {
        _viewModel.Actions.Add(new EditorAction { Type = EditorActionType.MouseMove, X = 1, Y = 1 });

        _ = _viewModel.CanDeleteHiddenEvents.Should().BeFalse();
        _ = _viewModel.ShowDeleteHiddenEvents.Should().BeFalse();

        _viewModel.HideMouseMoves = true;

        _ = _viewModel.ShowDeleteHiddenEvents.Should().BeTrue();
    }

    [Fact]
    public void ReplaceSelectedActionUnderlyingIndices_WhenVisibleRowRemovedFromListSelection_WritesBackSubsetSelection()
    {
        var first = new EditorAction { Type = EditorActionType.MouseClick, X = 1, Y = 1 };
        var second = new EditorAction { Type = EditorActionType.Delay, DelayMs = 20 };
        var third = new EditorAction { Type = EditorActionType.MouseClick, X = 3, Y = 3 };
        _viewModel.Actions.Add(first);
        _viewModel.Actions.Add(second);
        _viewModel.Actions.Add(third);
        _viewModel.ReplaceSelectedActionUnderlyingIndices([0, 2]);

        _viewModel.ReplaceSelectedActionUnderlyingIndices([0]);

        _ = _viewModel.SelectedActionUnderlyingIndices.Should().Equal(0);
        _ = _viewModel.SelectedAction.Should().BeSameAs(first);
        _ = _viewModel.SelectedActionListItem.Should().BeSameAs(_viewModel.ActionListItems[0]);
    }

    [Fact]
    public void BatchDelayProperties_WhenMultipleDelayActionsSelected_AreVisibleAndApplyToAllSelectedDelays()
    {
        var first = new EditorAction { Type = EditorActionType.Delay, DelayMs = 20, RandomDelayMinMs = 5, RandomDelayMaxMs = 30 };
        var second = new EditorAction { Type = EditorActionType.Delay, DelayMs = 40, RandomDelayMinMs = 10, RandomDelayMaxMs = 50 };
        var click = new EditorAction { Type = EditorActionType.MouseClick, X = 1 };
        _viewModel.Actions.Add(first);
        _viewModel.Actions.Add(second);
        _viewModel.Actions.Add(click);

        _viewModel.ReplaceSelectedActionUnderlyingIndices([0, 1]);
        _viewModel.BatchDelayMs = 125;
        _viewModel.BatchDelayUseRandomDelay = true;
        _viewModel.BatchRandomDelayMinMs = 25;
        _viewModel.BatchRandomDelayMaxMs = 250;

        _ = _viewModel.ShowSingleSelectedActionProperties.Should().BeFalse();
        _ = _viewModel.ShowBatchDelayProperties.Should().BeTrue();
        _ = _viewModel.ShowBatchRandomDelayOptions.Should().BeTrue();
        _ = first.DelayMs.Should().Be(125);
        _ = second.DelayMs.Should().Be(125);
        _ = first.UseRandomDelay.Should().BeTrue();
        _ = second.UseRandomDelay.Should().BeTrue();
        _ = first.RandomDelayMinMs.Should().Be(25);
        _ = second.RandomDelayMinMs.Should().Be(25);
        _ = first.RandomDelayMaxMs.Should().Be(250);
        _ = second.RandomDelayMaxMs.Should().Be(250);
        _ = click.DelayMs.Should().Be(0);
    }

    [Fact]
    public void ActionListItems_SimplifyMovement_DefaultsOffAndShowsRawProjection()
    {
        AddCondensibleRun(_viewModel, 6);

        _ = _viewModel.SimplifyMovement.Should().BeFalse();
        _ = _viewModel.HiddenEventCount.Should().Be(0);
        _ = _viewModel.ActionListItems.Should().HaveCount(6);
        _ = _viewModel.ActionListItems.Select(item => item.CondensedHiddenCount).Should().AllBeEquivalentTo(0);
    }

    [Fact]
    public void ActionListItems_WhenSimplifyMovementEnabled_CondensesSixActionRun()
    {
        AddCondensibleRun(_viewModel, 6);
        var originalCount = _viewModel.Actions.Count;

        _viewModel.SimplifyMovement = true;

        _ = _viewModel.Actions.Should().HaveCount(originalCount);
        _ = _viewModel.HiddenEventCount.Should().Be(0);
        _ = _viewModel.ActionListItems.Should().ContainSingle();
        var item = _viewModel.ActionListItems[0];
        _ = item.Action.Type.Should().Be(EditorActionType.MouseMove);
        _ = item.UnderlyingIndex.Should().Be(4);
        _ = item.Index.Should().Be(5);
        _ = item.CondensedHiddenCount.Should().Be(5);
    }

    [Fact]
    public void ActionListItems_WhenSimplifyMovementEnabled_DoesNotCondenseFiveActionRun()
    {
        AddCondensibleRun(_viewModel, 5);

        _viewModel.SimplifyMovement = true;

        _ = _viewModel.ActionListItems.Should().HaveCount(5);
        _ = _viewModel.ActionListItems.Select(item => item.UnderlyingIndex).Should().Equal(0, 1, 2, 3, 4);
        _ = _viewModel.ActionListItems.Select(item => item.CondensedHiddenCount).Should().AllBeEquivalentTo(0);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(10, false)]
    [InlineData(20, false)]
    [InlineData(4, true)]
    public void ActionListItems_WhenSimplifyMovementEnabled_DelayRulesDetermineRunBoundaries(int delayMs, bool useRandomDelay)
    {
        AddCondensibleRun(_viewModel, 3);
        _viewModel.Actions.Add(new EditorAction
        {
            Type = EditorActionType.Delay,
            DelayMs = delayMs,
            UseRandomDelay = useRandomDelay,
            RandomDelayMinMs = 1,
            RandomDelayMaxMs = 9,
        });
        AddCondensibleRun(_viewModel, 3);

        _viewModel.SimplifyMovement = true;

        _ = _viewModel.ActionListItems.Should().HaveCount(7);
        _ = _viewModel.ActionListItems.Select(item => item.UnderlyingIndex).Should().Equal(0, 1, 2, 3, 4, 5, 6);
        _ = _viewModel.ActionListItems.Select(item => item.CondensedHiddenCount).Should().AllBeEquivalentTo(0);
    }

    [Fact]
    public void ActionListItems_WhenCondensedRunIsFollowedByDifferentAction_KeepsFollowingActionVisible()
    {
        AddCondensibleRun(_viewModel, 6);
        var click = new EditorAction { Type = EditorActionType.MouseClick, X = 10, Y = 20 };
        _viewModel.Actions.Add(click);

        _viewModel.SimplifyMovement = true;

        _ = _viewModel.ActionListItems.Should().HaveCount(2);
        _ = _viewModel.ActionListItems[0].UnderlyingIndex.Should().Be(4);
        _ = _viewModel.ActionListItems[0].CondensedHiddenCount.Should().Be(5);
        _ = _viewModel.ActionListItems[1].Action.Should().BeSameAs(click);
        _ = _viewModel.ActionListItems[1].UnderlyingIndex.Should().Be(6);
        _ = _viewModel.ActionListItems[1].Index.Should().Be(7);
    }

    [Fact]
    public void ActionListItems_WhenRepresentativeIsNotFinalAction_CountsTrailingHiddenRows()
    {
        _viewModel.Actions.Add(new EditorAction { Type = EditorActionType.MouseMove, X = 0, Y = 1 });
        _viewModel.Actions.Add(new EditorAction { Type = EditorActionType.Delay, DelayMs = 4 });
        _viewModel.Actions.Add(new EditorAction { Type = EditorActionType.MouseMove, X = 2, Y = 3 });
        _viewModel.Actions.Add(new EditorAction { Type = EditorActionType.Delay, DelayMs = 4 });
        _viewModel.Actions.Add(new EditorAction { Type = EditorActionType.MouseMove, X = 4, Y = 5 });
        _viewModel.Actions.Add(new EditorAction { Type = EditorActionType.Delay, DelayMs = 4 });

        _viewModel.SimplifyMovement = true;

        _ = _viewModel.ActionListItems.Should().ContainSingle();
        _ = _viewModel.ActionListItems[0].Action.Type.Should().Be(EditorActionType.MouseMove);
        _ = _viewModel.ActionListItems[0].UnderlyingIndex.Should().Be(4);
        _ = _viewModel.ActionListItems[0].Index.Should().Be(5);
        _ = _viewModel.ActionListItems[0].CondensedHiddenCount.Should().Be(5);
    }

    [Fact]
    public void ActionListItems_WhenCondensedRunHasNoMouseMove_UsesFinalShortDelayAsRepresentative()
    {
        for (var index = 0; index < 6; index++)
        {
            _viewModel.Actions.Add(new EditorAction { Type = EditorActionType.Delay, DelayMs = 4 });
        }

        _viewModel.SimplifyMovement = true;

        _ = _viewModel.ActionListItems.Should().ContainSingle();
        _ = _viewModel.ActionListItems[0].Action.Type.Should().Be(EditorActionType.Delay);
        _ = _viewModel.ActionListItems[0].UnderlyingIndex.Should().Be(5);
        _ = _viewModel.ActionListItems[0].Index.Should().Be(6);
        _ = _viewModel.ActionListItems[0].CondensedHiddenCount.Should().Be(5);
    }

    [Fact]
    public void ActionListItems_WhenSimplifyMovementEnabled_DoesNotSummarizeDragMovement()
    {
        var down = new EditorAction { Type = EditorActionType.MouseDown };
        var up = new EditorAction { Type = EditorActionType.MouseUp };
        _viewModel.Actions.Add(down);
        AddCondensibleRun(_viewModel, 6);
        _viewModel.Actions.Add(up);

        _viewModel.SimplifyMovement = true;

        _ = _viewModel.ActionListItems.Should().HaveCount(8);
        _ = _viewModel.ActionListItems[0].Action.Should().BeSameAs(down);
        _ = _viewModel.ActionListItems.Skip(1).Take(6).Select(item => item.Action.Type).Should().Equal(
            EditorActionType.MouseMove,
            EditorActionType.Delay,
            EditorActionType.MouseMove,
            EditorActionType.Delay,
            EditorActionType.MouseMove,
            EditorActionType.Delay);
        _ = _viewModel.ActionListItems.Should().OnlyContain(item => item.CondensedHiddenCount == 0);
        _ = _viewModel.ActionListItems[7].Action.Should().BeSameAs(up);
    }

    [Fact]
    public void ActionListItems_WhenHideMouseMovesEnabled_HidesDragMovementRows()
    {
        var down = new EditorAction { Type = EditorActionType.MouseDown };
        var dragMove = new EditorAction { Type = EditorActionType.MouseMove, X = 10, Y = 20 };
        var up = new EditorAction { Type = EditorActionType.MouseUp };
        _viewModel.Actions.Add(down);
        _viewModel.Actions.Add(dragMove);
        _viewModel.Actions.Add(up);

        _viewModel.HideMouseMoves = true;

        _ = _viewModel.ActionListItems.Select(item => item.Action).Should().Equal(down, up);
        _ = _viewModel.HiddenEventCount.Should().Be(1);
    }

    [Theory]
    [MemberData(nameof(ActionVisualMetadataCases))]
    public void ActionListItems_ExposeVisualMetadataForActionTaxonomy(
        EditorAction action,
        EditorActionVisualKind visualKind,
        bool isNoise,
        bool isImportant,
        bool isCleanupEligible)
    {
        _viewModel.Actions.Add(action);

        var item = _viewModel.ActionListItems.Should().ContainSingle().Subject;
        _ = item.VisualKind.Should().Be(visualKind);
        _ = item.IsNoise.Should().Be(isNoise);
        _ = item.IsImportant.Should().Be(isImportant);
        _ = item.IsCleanupEligible.Should().Be(isCleanupEligible);
        _ = item.RepresentsSourceAction.Should().BeTrue();
        _ = item.CondensedHiddenCount.Should().Be(0);
    }

    [Fact]
    public void DeleteHiddenEvents_WhenSelectedVisibleActionSurvives_PreservesSelectionByActionIdentity()
    {
        // Arrange
        var move = new EditorAction { Type = EditorActionType.MouseMove, X = 1, Y = 2 };
        var selectedClick = new EditorAction { Type = EditorActionType.MouseClick, X = 3, Y = 4 };
        var shortDelay = new EditorAction { Type = EditorActionType.Delay, DelayMs = 9 };
        var finalClick = new EditorAction { Type = EditorActionType.MouseClick, X = 5, Y = 6 };
        _viewModel.Actions.Add(move);
        _viewModel.Actions.Add(selectedClick);
        _viewModel.Actions.Add(shortDelay);
        _viewModel.Actions.Add(finalClick);
        _viewModel.HideMouseMoves = true;
        _viewModel.HideShortWaits = true;
        _viewModel.SelectedAction = selectedClick;

        // Act
        _viewModel.DeleteHiddenEvents();

        // Assert
        _ = _viewModel.Actions.Should().Equal(selectedClick, finalClick);
        _ = _viewModel.SelectedAction.Should().BeSameAs(selectedClick);
        _ = _viewModel.SelectedActionListItem.Should().BeSameAs(_viewModel.ActionListItems[0]);
        _ = _viewModel.SelectedActionUnderlyingIndices.Should().Equal(0);
        _ = _viewModel.HasSelectedActions.Should().BeTrue();
        _ = _viewModel.CanRemoveSelectedActions.Should().BeTrue();
        _ = _viewModel.ShowSingleSelectedActionProperties.Should().BeTrue();
        _ = _viewModel.Status.Should().Be("[Editor_StatusDeletedHiddenEvents]");
    }

    [Fact]
    public async Task SaveMacroAsync_WhenNoActions_ShowsMessage()
    {
        // Act
        await _viewModel.SaveMacroAsync();

        // Assert
        await _dialogService.Received(1).ShowMessageAsync(
            Arg.Is<string>(m => m.Contains("NoActions", StringComparison.Ordinal)),
            Arg.Is<string>(m => m.Contains("NoActions", StringComparison.Ordinal)),
            "OK");
    }

    [Fact]
    public async Task SaveMacroAsync_WhenValidationFails_ShowsValidationMessage()
    {
        // Arrange
        _viewModel.AddAction();
        _ = _validator.ValidateAll(Arg.Any<IEnumerable<EditorAction>>())
            .Returns((false, new List<string> { "Error A", "Error B" }));

        // Act
        await _viewModel.SaveMacroAsync();

        // Assert
        await _dialogService.Received(1).ShowMessageAsync(
            Arg.Is<string>(m => m.Contains("ValidationErrors", StringComparison.Ordinal)),
            Arg.Is<string>(m => m.Contains("Error A", StringComparison.Ordinal)),
            "OK");
        _ = _viewModel.Status.Should().Contain("[Editor_StatusValidationFailed]");
    }

    [Theory]
    [InlineData(EditorActionType.ImageSearch)]
    [InlineData(EditorActionType.ImageClick)]
    [InlineData(EditorActionType.WaitImage)]
    public void SelectedAction_WhenImageActionSelected_ShowsImageOutputVariablesAndTimeout(EditorActionType actionType)
    {
        var action = new EditorAction { Type = actionType };
        _viewModel.Actions.Add(action);

        _viewModel.SelectedAction = action;

        _ = _viewModel.ShowImageSearchFields.Should().BeTrue();
        _ = _viewModel.ShowImageOutputVariableFields.Should().BeTrue();
        _ = _viewModel.ShowImageWaitTimeoutField.Should().BeTrue();
    }

    [Fact]
    public void DelayVisibility_TogglesBetweenFixedAndRandomInputs()
    {
        // Arrange
        _viewModel.NewActionType = EditorActionType.Delay;
        _viewModel.AddAction();

        // Assert
        _ = _viewModel.ShowDelay.Should().BeTrue();
        _ = _viewModel.ShowFixedDelayInput.Should().BeTrue();
        _ = _viewModel.ShowRandomDelayOptions.Should().BeFalse();

        // Act
        _viewModel.SelectedAction!.UseRandomDelay = true;

        // Assert
        _ = _viewModel.ShowFixedDelayInput.Should().BeFalse();
        _ = _viewModel.ShowRandomDelayOptions.Should().BeTrue();
    }

    [Theory]
    [InlineData(EditorActionType.MouseMove)]
    [InlineData(EditorActionType.MouseClick)]
    [InlineData(EditorActionType.MouseDown)]
    [InlineData(EditorActionType.MouseUp)]
    public void ShowCoordinates_ForCoordinateBasedMouseActions_IsTrue(EditorActionType actionType)
    {
        // Arrange
        _viewModel.NewActionType = actionType;

        // Act
        _viewModel.AddAction();

        // Assert
        _ = _viewModel.ShowCoordinates.Should().BeTrue();
    }

    [Theory]
    [InlineData(EditorActionType.MouseMove)]
    [InlineData(EditorActionType.MouseClick)]
    [InlineData(EditorActionType.MouseDown)]
    [InlineData(EditorActionType.MouseUp)]
    public void ShowCoordModeToggle_ForCoordinateBasedMouseActions_IsTrue(EditorActionType actionType)
    {
        // Arrange
        _viewModel.NewActionType = actionType;

        // Act
        _viewModel.AddAction();

        // Assert
        _ = _viewModel.ShowCoordModeToggle.Should().BeTrue();
    }

    [Fact]
    public void ScriptSetVariableAction_ShowsStructuredFieldsOnly()
    {
        // Arrange
        _viewModel.NewActionType = EditorActionType.SetVariable;

        // Act
        _viewModel.AddAction();

        // Assert
        _ = _viewModel.ShowSetVariableFields.Should().BeTrue();
        _ = _viewModel.ShowTextInput.Should().BeFalse();
        _ = _viewModel.ShowIncDecFields.Should().BeFalse();
        _ = _viewModel.ShowConditionFields.Should().BeFalse();
    }

    [Fact]
    public void ForAction_WhenStepEnabled_ShowsStepFields()
    {
        // Arrange
        _viewModel.NewActionType = EditorActionType.ForBlockStart;
        _viewModel.AddAction();

        // Act
        _viewModel.SelectedAction!.ForHasStep = true;

        // Assert
        _ = _viewModel.ShowForFields.Should().BeTrue();
        _ = _viewModel.ShowForStepFields.Should().BeTrue();
    }

    [Fact]
    public void VariableSuggestionBindings_ForScriptFields_WriteBackToSelectedActions()
    {
        // Arrange
        _viewModel.NewActionType = EditorActionType.SetVariable;
        _viewModel.AddAction();
        _viewModel.SelectedAction!.ScriptVariableName = "shared";

        _viewModel.NewActionType = EditorActionType.IncrementVariable;
        _viewModel.AddAction();
        var incrementAction = _viewModel.SelectedAction!;

        _viewModel.NewActionType = EditorActionType.IfBlockStart;
        _viewModel.AddAction();
        var conditionAction = _viewModel.SelectedAction!;
        conditionAction.ScriptLeftOperandType = ScriptOperandType.VariableReference;
        conditionAction.ScriptRightOperandType = ScriptOperandType.VariableReference;

        _viewModel.NewActionType = EditorActionType.ForBlockStart;
        _viewModel.AddAction();
        var forAction = _viewModel.SelectedAction!;

        // Act / Assert
        _viewModel.SelectedAction = incrementAction;
        _ = _viewModel.ShowIncDecVariablePicker.Should().BeTrue();
        _viewModel.SelectedIncDecVariableSuggestion = "shared";
        _ = _viewModel.SelectedIncDecVariableSuggestion.Should().BeNull();
        _ = incrementAction.ScriptVariableName.Should().Be("shared");

        _viewModel.SelectedAction = conditionAction;
        _ = _viewModel.ShowConditionLeftVariablePicker.Should().BeTrue();
        _ = _viewModel.ShowConditionLeftOperandTextBox.Should().BeFalse();
        _ = _viewModel.ShowConditionRightVariablePicker.Should().BeTrue();
        _ = _viewModel.ShowConditionRightOperandTextBox.Should().BeFalse();
        _viewModel.SelectedConditionLeftVariableSuggestion = "shared";
        _viewModel.SelectedConditionRightVariableSuggestion = "shared";
        _ = _viewModel.SelectedConditionLeftVariableSuggestion.Should().Be("shared");
        _ = _viewModel.SelectedConditionRightVariableSuggestion.Should().Be("shared");
        _ = conditionAction.ScriptLeftOperand.Should().Be("shared");
        _ = conditionAction.ScriptRightOperand.Should().Be("shared");

        _viewModel.SelectedAction = forAction;
        _ = _viewModel.ShowForVariablePicker.Should().BeTrue();
        _viewModel.SelectedForVariableSuggestion = "shared";
        _ = _viewModel.SelectedForVariableSuggestion.Should().BeNull();
        _ = forAction.ForVariableName.Should().Be("shared");

        _ = _viewModel.AvailableVariableNames.Should().Contain("shared");
        _ = _viewModel.HasAvailableVariableNames.Should().BeTrue();
    }

    [Fact]
    public void ActionListPresentation_WhenNestedBlocksExist_ShowsIndentAndContextualEndLabels()
    {
        // Arrange
        _viewModel.NewActionType = EditorActionType.RepeatBlockStart;
        _viewModel.AddAction();
        _viewModel.SelectedAction = _viewModel.Actions[0];
        _viewModel.NewActionType = EditorActionType.IfBlockStart;
        _viewModel.AddAction();

        // Assert
        _ = _viewModel.ActionListItems[0].IndentLevel.Should().Be(0);
        _ = _viewModel.ActionListItems[1].IndentLevel.Should().Be(1);
        _ = _viewModel.ActionListItems[2].DisplayName.Should().Be("End IfToken");
        _ = _viewModel.ActionListItems[2].IndentLevel.Should().Be(1);
        _ = _viewModel.ActionListItems[3].DisplayName.Should().Be("End RepeatToken");
        _ = _viewModel.ActionListItems[3].IndentLevel.Should().Be(0);
    }

    [Fact]
    public void CurrentPositionToggle_IsVisibleForMouseDownAndMouseUp()
    {
        // Arrange
        _viewModel.NewActionType = EditorActionType.MouseDown;
        _viewModel.AddAction();

        // Assert
        _ = _viewModel.ShowCurrentPositionToggle.Should().BeTrue();
        _ = _viewModel.CurrentPositionToggleLabel.Should().Be("[Editor_CurrentPositionHold]");

        // Act
        _viewModel.NewActionType = EditorActionType.MouseUp;
        _viewModel.AddAction();

        // Assert
        _ = _viewModel.ShowCurrentPositionToggle.Should().BeTrue();
        _ = _viewModel.CurrentPositionToggleLabel.Should().Be("[Editor_CurrentPositionRelease]");
    }

    [Fact]
    public void SelectedActionCoordinateModeProperties_IgnoreRadioUncheckWritesAndPersistCheckedModeAcrossSelectionChanges()
    {
        // Arrange
        var moveAction = new EditorAction { Type = EditorActionType.MouseMove, IsAbsolute = true, X = 10, Y = 20 };
        var clickAction = new EditorAction { Type = EditorActionType.MouseClick, IsAbsolute = true, X = 30, Y = 40 };
        _viewModel.Actions.Add(moveAction);
        _viewModel.Actions.Add(clickAction);
        _viewModel.SelectedAction = moveAction;

        // Act: Avalonia first checks the relative radio, then may uncheck the absolute radio during rebind.
        _viewModel.SelectedActionIsRelative = true;
        _viewModel.SelectedActionIsAbsolute = false;
        _viewModel.SelectedAction = clickAction;
        _viewModel.SelectedAction = moveAction;

        // Assert
        _ = moveAction.IsAbsolute.Should().BeFalse();
        _ = clickAction.IsAbsolute.Should().BeTrue();
        _ = _viewModel.SelectedActionIsRelative.Should().BeTrue();
        _ = _viewModel.SelectedActionIsAbsolute.Should().BeFalse();
    }
}
