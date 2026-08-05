// Behavioral cluster extracted from the fixture to keep test ownership explicit.
namespace CrossMacro.UI.Tests.ViewModels;

public sealed partial class EditorViewModelTests
{

    [Fact]
    public async Task BrowseScreenshotOutputPathAsync_WhenPathChosen_UsesPngSaveDialogAndUpdatesSelectedAction()
    {
        var action = new EditorAction
        {
            Type = EditorActionType.Screenshot,
            ScreenshotOutputPath = "/tmp/current-shot.png",
        };
        _viewModel.Actions.Add(action);
        _viewModel.SelectedAction = action;
        _ = _dialogService
            .ShowSaveFileDialogAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<FileDialogFilter[]>())
            .Returns("/tmp/new-shot.png");

        await _viewModel.BrowseScreenshotOutputPathAsync();

        _ = await _dialogService.Received(1).ShowSaveFileDialogAsync(
            "Editor_ScreenshotSaveDialogTitle",
            "current-shot.png",
            Arg.Is<FileDialogFilter[]>(filters =>
                filters.Length == 1
                && filters[0].Name == "Editor_ScreenshotFileDialogName"
                && filters[0].Extensions.SequenceEqual(PngExtensions)));
        _ = action.ScreenshotOutputPath.Should().Be("/tmp/new-shot.png");
        _ = _viewModel.CanUndo.Should().BeTrue();
    }

    [Fact]
    public async Task BrowseScreenshotOutputPathAsync_WhenCancelled_DoesNotUpdateSelectedAction()
    {
        var action = new EditorAction { Type = EditorActionType.Screenshot };
        _viewModel.Actions.Add(action);
        _viewModel.SelectedAction = action;
        _ = _dialogService
            .ShowSaveFileDialogAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<FileDialogFilter[]>())
            .Returns((string?)null);

        await _viewModel.BrowseScreenshotOutputPathAsync();

        _ = await _dialogService.Received(1).ShowSaveFileDialogAsync(
            "Editor_ScreenshotSaveDialogTitle",
            "Editor_ScreenshotDefaultFileName",
            Arg.Any<FileDialogFilter[]>());
        _ = action.ScreenshotOutputPath.Should().BeEmpty();
        _ = _viewModel.CanUndo.Should().BeFalse();
    }

    [Theory]
    [InlineData(EditorActionType.ImageSearch)]
    [InlineData(EditorActionType.ImageClick)]
    [InlineData(EditorActionType.WaitImage)]
    public async Task ImportImageAssetAsync_WhenPngChosen_AddsAssetNameAndSelectsImageAction(EditorActionType actionType)
    {
        var pngPath = Path.Combine(Path.GetTempPath(), $"crossmacro-target-{Guid.NewGuid():N}.png");
        await File.WriteAllBytesAsync(pngPath, Convert.FromBase64String(TransparentPngBase64), NonCancelableToken);
        try
        {
            var action = new EditorAction { Type = actionType };
            _viewModel.Actions.Add(action);
            _viewModel.SelectedAction = action;
            _ = _dialogService
                .ShowOpenFileDialogAsync(Arg.Any<string>(), Arg.Any<FileDialogFilter[]>())
                .Returns(pngPath);

            await _viewModel.ImportImageAssetAsync();

            _ = await _dialogService.Received(1).ShowOpenFileDialogAsync(
                "Editor_ImageAssetImportDialogTitle",
                Arg.Is<FileDialogFilter[]>(filters =>
                    filters.Length == 1
                    && filters[0].Name == "Editor_ImageAssetFileDialogName"
                    && filters[0].Extensions.SequenceEqual(PngExtensions)));
            _ = _viewModel.HasImageAssets.Should().BeTrue();
            _ = _viewModel.ImageAssetNames.Should().ContainSingle().Which.Should().StartWith("crossmacro_target_");
            _ = action.ImageAssetName.Should().Be(_viewModel.ImageAssetNames[0]);
            _ = _viewModel.Status.Should().Contain("Editor_StatusImageImported");
        }
        finally
        {
            File.Delete(pngPath);
        }
    }

    [Theory]
    [InlineData(EditorActionType.ImageSearch)]
    [InlineData(EditorActionType.ImageClick)]
    [InlineData(EditorActionType.WaitImage)]
    public void AddAction_ForImageActions_InitializesSharedDefaultsAndUsesImportedAsset(EditorActionType actionType)
    {
        _viewModel.ImageAssetNames.Add("Target_1");
        _viewModel.NewActionType = actionType;

        _viewModel.AddAction();

        var action = _viewModel.Actions.Should().ContainSingle().Subject;
        _ = action.Type.Should().Be(actionType);
        _ = action.ImageAssetName.Should().Be("Target_1");
        _ = action.ImageSearchSimilarity.Should().Be(1.0);
        _ = action.ImageSearchDownsample.Should().Be(1);
        _ = action.ScreenWidth.Should().Be(EditorActionScreenReadingPayload.DefaultSearchScreenWidth);
        _ = action.ScreenHeight.Should().Be(EditorActionScreenReadingPayload.DefaultSearchScreenHeight);
        if (actionType is EditorActionType.ImageClick)
        {
            _ = action.Button.Should().Be(MacroMouseButton.Left);
        }
        _ = _viewModel.SelectedAction.Should().BeSameAs(action);
    }

    [Fact]
    public async Task ImportImageAssetAsync_WhenCancelled_LeavesAssetsEmpty()
    {
        _ = _dialogService
            .ShowOpenFileDialogAsync(Arg.Any<string>(), Arg.Any<FileDialogFilter[]>())
            .Returns((string?)null);

        await _viewModel.ImportImageAssetAsync();

        _ = _viewModel.HasImageAssets.Should().BeFalse();
        _ = _viewModel.ImageAssetNames.Should().BeEmpty();
        _ = _viewModel.Status.Should().Be("Editor_StatusImageImportCancelled");
    }

    [Fact]
    public void ScreenReadingColorPreview_WhenSelectedColorChanges_NormalizesAndNotifies()
    {
        var action = new EditorAction { Type = EditorActionType.WaitColor, ScreenColorHex = "ffffff" };
        var changed = new List<string?>();
        _viewModel.Actions.Add(action);
        _viewModel.SelectedAction = action;
        _viewModel.PropertyChanged += (_, args) => changed.Add(args.PropertyName);

        action.ScreenColorHex = "12abef";

        _ = _viewModel.ScreenReadingColorPreviewHex.Should().Be("12ABEF");
        _ = _viewModel.ShowScreenReadingColorPreview.Should().BeTrue();
        _ = changed.Should().Contain(nameof(EditorViewModel.ScreenReadingColorPreviewHex));
        _ = changed.Should().Contain(nameof(EditorViewModel.ShowScreenReadingColorPreview));
    }

    [Fact]
    public void Undo_AfterScreenReadingPropertyEdit_RestoresPreviousValue()
    {
        _viewModel.NewActionType = EditorActionType.PixelSearch;
        _viewModel.AddAction();
        var action = _viewModel.SelectedAction!;
        action.ScreenColorHex = "00FF00";
        action.ScreenFoundVariableName = "found";
        action.ScreenFoundXVariableName = "found_x";
        action.ScreenFoundYVariableName = "found_y";

        action.ScreenFoundVariableName = "is_found";
        _viewModel.Undo();

        _ = _viewModel.SelectedAction.Should().NotBeNull();
        _ = _viewModel.SelectedAction!.ScreenFoundVariableName.Should().Be("found");
    }

    [Fact]
    public void Undo_AfterScreenReadingCoordinateEdit_RestoresPreviousValue()
    {
        _viewModel.NewActionType = EditorActionType.PixelColor;
        _viewModel.AddAction();
        var action = _viewModel.SelectedAction!;
        action.ScreenX = 10;
        action.ScreenY = 20;

        action.ScreenX = 30;
        _viewModel.Undo();

        _ = _viewModel.SelectedAction.Should().NotBeNull();
        _ = _viewModel.SelectedAction!.ScreenX.Should().Be(10);
        _ = _viewModel.SelectedAction.ScreenY.Should().Be(20);
    }

    [Fact]
    public void Undo_AfterImageSearchMatchModeEdits_RestoresIntermediateState()
    {
        _viewModel.NewActionType = EditorActionType.ImageSearch;
        _viewModel.AddAction();
        var action = _viewModel.SelectedAction!;

        _viewModel.SelectedImageSearchMatchMode = EditorImageMatchMode.BestMatch;
        _ = action.ImageSearchMatchModeWasExplicit.Should().BeTrue();

        _viewModel.Undo();

        _ = _viewModel.SelectedAction.Should().NotBeNull();
        _ = _viewModel.SelectedAction!.ImageSearchMatchMode.Should().Be(EditorImageMatchMode.FirstThresholdMatch);
        _ = _viewModel.SelectedAction.ImageSearchMatchModeWasExplicit.Should().BeFalse();

        _viewModel.Redo();

        _ = _viewModel.SelectedAction!.ImageSearchMatchMode.Should().Be(EditorImageMatchMode.BestMatch);
        _ = _viewModel.SelectedAction.ImageSearchMatchModeWasExplicit.Should().BeTrue();
    }

    [Theory]
    [InlineData(EditorActionType.PixelColor)]
    [InlineData(EditorActionType.WaitColor)]
    [InlineData(EditorActionType.PixelSearch)]
    public void EditorActionClone_ForScreenReadingActions_CopiesPayload(EditorActionType actionType)
    {
        var action = new EditorAction
        {
            Type = actionType,
            IsAbsolute = false,
            ScreenX = -3,
            ScreenY = 4,
            ScreenLeft = 5,
            ScreenTop = 6,
            ScreenWidth = 7,
            ScreenHeight = 8,
            ScreenColorHex = "00aaee",
            ScreenColorVariableName = "sample_color",
            ScreenTimeoutMs = 1234,
            ScreenTolerance = 9,
            ScreenFoundVariableName = "found",
            ScreenFoundXVariableName = "found_x",
            ScreenFoundYVariableName = "found_y",
        };

        var clone = action.Clone();

        _ = clone.Id.Should().NotBe(action.Id);
        _ = clone.TryGetScreenReadingPayload(out var clonePayload).Should().BeTrue();
        _ = action.TryGetScreenReadingPayload(out var originalPayload).Should().BeTrue();
        _ = clonePayload.Should().Be(originalPayload);
    }

    [Fact]
    public async Task CaptureMouseAsync_WhenSelectionChanges_IgnoresCapturedPosition()
    {
        // Arrange
        _viewModel.AddAction();
        var firstAction = _viewModel.SelectedAction!;
        _viewModel.AddAction();
        var secondAction = _viewModel.SelectedAction!;
        _viewModel.SelectedAction = firstAction;

        var captureResult = new TaskCompletionSource<(int X, int Y)?>(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = _captureService.CaptureMousePositionAsync(Arg.Any<CancellationToken>()).Returns(_ => captureResult.Task);

        // Act
        var captureTask = _viewModel.CaptureMouseAsync();
        _viewModel.SelectedAction = secondAction;
        captureResult.SetResult((640, 480));
        await captureTask;

        // Assert
        _ = firstAction.X.Should().Be(0);
        _ = firstAction.Y.Should().Be(0);
        _ = secondAction.X.Should().Be(0);
        _ = secondAction.Y.Should().Be(0);
        _ = _viewModel.Status.Should().Be("[Editor_StatusCaptureSelectionChanged]");
    }

    [Fact]
    public async Task CaptureMouseAsync_WhenCaptureIsCancelled_ReportsCancellationAndClearsMode()
    {
        _viewModel.AddAction();
        _ = _captureService.CaptureMousePositionAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<(int X, int Y)?>(null));

        await _viewModel.CaptureMouseAsync();

        _ = _viewModel.CaptureMode.Should().Be(EditorViewModel.EditorCaptureMode.None);
        _ = _viewModel.Status.Should().Be("[Editor_StatusCaptureCancelled]");
    }

    [Fact]
    public async Task CaptureMouseAsync_WhenCaptureFails_ReportsErrorAndClearsMode()
    {
        _viewModel.AddAction();
        _ = _captureService.CaptureMousePositionAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromException<(int X, int Y)?>(new InvalidOperationException("capture failed")));

        await _viewModel.CaptureMouseAsync();

        _ = _viewModel.CaptureMode.Should().Be(EditorViewModel.EditorCaptureMode.None);
        _ = _viewModel.Status.Should().Be("Editor_StatusCaptureError");
    }

    [Fact]
    public void CancelCapture_CancelsNeutralCaptureServiceAndUpdatesEditorState()
    {
        _viewModel.CancelCapture();

        _captureService.Received(1).CancelCapture();
        _ = _viewModel.CaptureMode.Should().Be(EditorViewModel.EditorCaptureMode.None);
        _ = _viewModel.Status.Should().Be("[Editor_StatusCaptureCancelled]");
    }

    [Fact]
    public async Task CaptureMouseAsync_WhenSelectedActionIsRelative_StoresCapturedPositionAsAbsoluteAction()
    {
        // Arrange
        var action = new EditorAction
        {
            Type = EditorActionType.MouseClick,
            Button = MacroMouseButton.Left,
            IsAbsolute = false,
            X = 3,
            Y = -2,
        };
        _viewModel.Actions.Add(action);
        _viewModel.SelectedAction = action;
        _ = _captureService.CaptureMousePositionAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<(int X, int Y)?>((640, 480)));

        // Act
        await _viewModel.CaptureMouseAsync();

        // Assert
        _ = action.IsAbsolute.Should().BeTrue();
        _ = action.X.Should().Be(640);
        _ = action.Y.Should().Be(480);
    }

    [Fact]
    public async Task CaptureMouseAsync_WhenSelectedActionIsPixelColor_StoresCapturedPositionAsAbsoluteScreenPoint()
    {
        // Arrange
        var action = new EditorAction
        {
            Type = EditorActionType.PixelColor,
            IsAbsolute = false,
            X = 3,
            Y = -2,
            ScreenX = 10,
            ScreenY = 20,
        };
        _viewModel.Actions.Add(action);
        _viewModel.SelectedAction = action;
        _ = _captureService.CaptureMousePositionAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<(int X, int Y)?>((640, 480)));

        // Act
        await _viewModel.CaptureMouseAsync();

        // Assert
        _ = action.IsAbsolute.Should().BeTrue();
        _ = action.ScreenX.Should().Be(640);
        _ = action.ScreenY.Should().Be(480);
        _ = action.X.Should().Be(3);
        _ = action.Y.Should().Be(-2);
    }

    [Fact]
    public async Task CaptureMouseAsync_WhenSelectedActionIsWaitColor_StoresCapturedPositionAsScreenPoint()
    {
        // Arrange
        var action = new EditorAction
        {
            Type = EditorActionType.WaitColor,
            X = 3,
            Y = -2,
            ScreenX = 10,
            ScreenY = 20,
        };
        _viewModel.Actions.Add(action);
        _viewModel.SelectedAction = action;
        _ = _captureService.CaptureMousePositionAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<(int X, int Y)?>((640, 480)));

        // Act
        await _viewModel.CaptureMouseAsync();

        // Assert
        _ = action.ScreenX.Should().Be(640);
        _ = action.ScreenY.Should().Be(480);
        _ = action.X.Should().Be(3);
        _ = action.Y.Should().Be(-2);
    }

    [Fact]
    public async Task CaptureTargetColorAsync_WhenSelectedActionIsWaitColor_StoresCapturedPixelColor()
    {
        // Arrange
        var action = new EditorAction { Type = EditorActionType.WaitColor, ScreenColorHex = "000000" };
        _viewModel.Actions.Add(action);
        _viewModel.SelectedAction = action;
        _ = _captureService.CaptureMousePositionAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<(int X, int Y)?>((10, 20)));
        _ = _screenPixelReader.GetPixelAsync(Arg.Any<ScreenPoint>(), Arg.Any<ScreenReadOptions>())
            .Returns(Task.FromResult(ScreenReadResultFactory.Success<ScreenPixelColor>(new ScreenPixelColor(0x12, 0xAB, 0xEF))));

        // Act
        await _viewModel.CaptureTargetColorAsync();

        // Assert
        _ = action.ScreenColorHex.Should().Be("12ABEF");
        _ = _screenPixelReader.Received(1).GetPixelAsync(
            Arg.Is<ScreenPoint>(point => point.X == 10 && point.Y == 20),
            Arg.Any<ScreenReadOptions>());
        _ = _viewModel.Status.Should().Be("[Editor_StatusCapturedColor] 12ABEF 10 20");
    }

    [Fact]
    public async Task CaptureTargetColorAsync_WhenSelectedActionIsPixelSearch_StoresCapturedPixelColor()
    {
        // Arrange
        var action = new EditorAction { Type = EditorActionType.PixelSearch, ScreenColorHex = "000000" };
        _viewModel.Actions.Add(action);
        _viewModel.SelectedAction = action;
        _ = _captureService.CaptureMousePositionAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<(int X, int Y)?>((30, 40)));
        _ = _screenPixelReader.GetPixelAsync(Arg.Any<ScreenPoint>(), Arg.Any<ScreenReadOptions>())
            .Returns(Task.FromResult(ScreenReadResultFactory.Success<ScreenPixelColor>(new ScreenPixelColor(0x01, 0x23, 0x45))));

        // Act
        await _viewModel.CaptureTargetColorAsync();

        // Assert
        _ = action.ScreenColorHex.Should().Be("012345");
        _ = _screenPixelReader.Received(1).GetPixelAsync(
            Arg.Is<ScreenPoint>(point => point.X == 30 && point.Y == 40),
            Arg.Any<ScreenReadOptions>());
    }

    [Fact]
    public async Task CaptureTargetColorAsync_WhenSelectionChanges_DoesNotMutateColor()
    {
        // Arrange
        var firstAction = new EditorAction { Type = EditorActionType.WaitColor, ScreenColorHex = "111111" };
        var secondAction = new EditorAction { Type = EditorActionType.WaitColor, ScreenColorHex = "222222" };
        _viewModel.Actions.Add(firstAction);
        _viewModel.Actions.Add(secondAction);
        _viewModel.SelectedAction = firstAction;

        var captureResult = new TaskCompletionSource<(int X, int Y)?>(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = _captureService.CaptureMousePositionAsync(Arg.Any<CancellationToken>()).Returns(_ => captureResult.Task);

        // Act
        var captureTask = _viewModel.CaptureTargetColorAsync();
        _viewModel.SelectedAction = secondAction;
        captureResult.SetResult((50, 60));
        await captureTask;

        // Assert
        _ = firstAction.ScreenColorHex.Should().Be("111111");
        _ = secondAction.ScreenColorHex.Should().Be("222222");
        _ = _viewModel.Status.Should().Be("[Editor_StatusCaptureSelectionChanged]");
        _ = _screenPixelReader.DidNotReceive().GetPixelAsync(Arg.Any<ScreenPoint>(), Arg.Any<ScreenReadOptions>());
    }

    [Fact]
    public async Task CaptureTargetColorAsync_WhenSelectedActionDoesNotUseTargetColor_BlocksCapture()
    {
        // Arrange
        var action = new EditorAction { Type = EditorActionType.PixelColor, ScreenColorHex = "111111" };
        _viewModel.Actions.Add(action);
        _viewModel.SelectedAction = action;

        // Act
        await _viewModel.CaptureTargetColorAsync();

        // Assert
        _ = action.ScreenColorHex.Should().Be("111111");
        _ = _viewModel.Status.Should().Be("[Editor_StatusOperationBlocked]");
        _ = _captureService.DidNotReceive().CaptureMousePositionAsync(Arg.Any<CancellationToken>());
        _ = _screenPixelReader.DidNotReceive().GetPixelAsync(Arg.Any<ScreenPoint>(), Arg.Any<ScreenReadOptions>());
    }

    [Fact]
    public async Task CaptureConditionRightColorAsync_WhenRightOperandIsColor_StoresCapturedPixelColor()
    {
        var action = new EditorAction
        {
            Type = EditorActionType.IfBlockStart,
            ScriptRightOperandType = ScriptOperandType.Color,
            ScriptRightOperand = "000000",
        };
        _viewModel.Actions.Add(action);
        _viewModel.SelectedAction = action;
        _ = _captureService.CaptureMousePositionAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<(int X, int Y)?>((70, 80)));
        _ = _screenPixelReader.GetPixelAsync(Arg.Any<ScreenPoint>(), Arg.Any<ScreenReadOptions>())
            .Returns(Task.FromResult(ScreenReadResultFactory.Success<ScreenPixelColor>(new ScreenPixelColor(0xDE, 0xAD, 0xBE))));

        await _viewModel.CaptureConditionRightColorAsync();

        _ = action.ScriptRightOperand.Should().Be("DEADBE");
        _ = _screenPixelReader.Received(1).GetPixelAsync(
            Arg.Is<ScreenPoint>(point => point.X == 70 && point.Y == 80),
            Arg.Any<ScreenReadOptions>());
        _ = _viewModel.Status.Should().Be("[Editor_StatusCapturedColor] DEADBE 70 80");
    }

    [Fact]
    public async Task CaptureConditionLeftColorAsync_WhenLeftOperandIsColor_StoresCapturedPixelColor()
    {
        var action = new EditorAction
        {
            Type = EditorActionType.WhileBlockStart,
            ScriptLeftOperandType = ScriptOperandType.Color,
            ScriptLeftOperand = "000000",
        };
        _viewModel.Actions.Add(action);
        _viewModel.SelectedAction = action;
        _ = _captureService.CaptureMousePositionAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<(int X, int Y)?>((71, 81)));
        _ = _screenPixelReader.GetPixelAsync(Arg.Any<ScreenPoint>(), Arg.Any<ScreenReadOptions>())
            .Returns(Task.FromResult(ScreenReadResultFactory.Success<ScreenPixelColor>(new ScreenPixelColor(0x12, 0x34, 0x56))));

        await _viewModel.CaptureConditionLeftColorAsync();

        _ = action.ScriptLeftOperand.Should().Be("123456");
    }

    [Fact]
    public async Task CaptureConditionRightColorAsync_WhenOperandTypeChanges_DoesNotMutateOperand()
    {
        var action = new EditorAction
        {
            Type = EditorActionType.IfBlockStart,
            ScriptRightOperandType = ScriptOperandType.Color,
            ScriptRightOperand = "111111",
        };
        _viewModel.Actions.Add(action);
        _viewModel.SelectedAction = action;

        _ = _captureService.CaptureMousePositionAsync(Arg.Any<CancellationToken>()).Returns(_ =>
        {
            action.ScriptRightOperandType = ScriptOperandType.Text;
            return Task.FromResult<(int X, int Y)?>((73, 83));
        });

        await _viewModel.CaptureConditionRightColorAsync();

        _ = action.ScriptRightOperand.Should().Be("111111");
        _ = _viewModel.Status.Should().Be("[Editor_StatusOperationBlocked]");
        _ = _screenPixelReader.DidNotReceive().GetPixelAsync(Arg.Any<ScreenPoint>(), Arg.Any<ScreenReadOptions>());
    }

    [Fact]
    public async Task CapturePixelSearchTopLeftAsync_PreservesExistingBottomRightWhenPossible()
    {
        // Arrange
        var action = new EditorAction
        {
            Type = EditorActionType.PixelSearch,
            ScreenLeft = 10,
            ScreenTop = 20,
            ScreenWidth = 11,
            ScreenHeight = 21,
        };
        _viewModel.Actions.Add(action);
        _viewModel.SelectedAction = action;
        _ = _captureService.CaptureMousePositionAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<(int X, int Y)?>((15, 25)));

        // Act
        await _viewModel.CapturePixelSearchTopLeftAsync();

        // Assert
        _ = action.ScreenLeft.Should().Be(15);
        _ = action.ScreenTop.Should().Be(25);
        _ = action.ScreenWidth.Should().Be(6);
        _ = action.ScreenHeight.Should().Be(16);
        _ = _viewModel.Status.Should().Be("[Editor_StatusCapturedRegionTopLeft] 15 25");
    }

    [Fact]
    public async Task CapturePixelSearchBottomRightAsync_StoresInclusiveRegionDimensions()
    {
        // Arrange
        var action = new EditorAction
        {
            Type = EditorActionType.PixelSearch,
            ScreenLeft = 10,
            ScreenTop = 20,
            ScreenWidth = 1,
            ScreenHeight = 1,
        };
        _viewModel.Actions.Add(action);
        _viewModel.SelectedAction = action;
        _ = _captureService.CaptureMousePositionAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<(int X, int Y)?>((14, 24)));

        // Act
        await _viewModel.CapturePixelSearchBottomRightAsync();

        // Assert
        _ = action.ScreenWidth.Should().Be(5);
        _ = action.ScreenHeight.Should().Be(5);
        _ = _viewModel.Status.Should().Be("[Editor_StatusCapturedRegionBottomRight] 14 24");
    }

    [Fact]
    public async Task CapturePixelSearchBottomRightAsync_WhenCapturedPointIsInvalid_PreservesDimensionsAndSetsStatus()
    {
        // Arrange
        var action = new EditorAction
        {
            Type = EditorActionType.PixelSearch,
            ScreenLeft = 10,
            ScreenTop = 20,
            ScreenWidth = 7,
            ScreenHeight = 8,
        };
        _viewModel.Actions.Add(action);
        _viewModel.SelectedAction = action;
        _ = _captureService.CaptureMousePositionAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<(int X, int Y)?>((9, 25)));

        // Act
        await _viewModel.CapturePixelSearchBottomRightAsync();

        // Assert
        _ = action.ScreenWidth.Should().Be(7);
        _ = action.ScreenHeight.Should().Be(8);
        _ = _viewModel.Status.Should().Be("[Editor_StatusCaptureRegionInvalidBottomRight]");
    }

    [Theory]
    [InlineData(EditorActionType.WaitColor)]
    [InlineData(EditorActionType.ImageSearch)]
    [InlineData(EditorActionType.ImageClick)]
    [InlineData(EditorActionType.WaitImage)]
    public async Task CapturePixelSearchTopLeftAsync_OnlyAllowsPixelSearchAndImageActions(EditorActionType actionType)
    {
        // Arrange
        var action = new EditorAction { Type = actionType, ScreenLeft = 10, ScreenTop = 20 };
        _viewModel.Actions.Add(action);
        _viewModel.SelectedAction = action;

        if (actionType is EditorActionType.ImageSearch or EditorActionType.ImageClick or EditorActionType.WaitImage)
        {
            _ = _captureService.CaptureMousePositionAsync(Arg.Any<CancellationToken>())
                .Returns(Task.FromResult<(int X, int Y)?>((12, 34)));
        }

        // Act
        await _viewModel.CapturePixelSearchTopLeftAsync();

        // Assert
        if (actionType is EditorActionType.WaitColor)
        {
            _ = action.ScreenLeft.Should().Be(10);
            _ = action.ScreenTop.Should().Be(20);
            _ = _viewModel.Status.Should().Be("[Editor_StatusOperationBlocked]");
            _ = _captureService.DidNotReceive().CaptureMousePositionAsync(Arg.Any<CancellationToken>());
        }
        else
        {
            _ = action.ScreenLeft.Should().Be(12);
            _ = action.ScreenTop.Should().Be(34);
            _ = _viewModel.Status.Should().Be("[Editor_StatusCapturedRegionTopLeft] 12 34");
        }
    }

    [Fact]
    public async Task CaptureScreenshotRegionStartAsync_StoresStartAndEnablesRegion()
    {
        var action = new EditorAction
        {
            Type = EditorActionType.Screenshot,
            ScreenshotUseRegion = false,
            ScreenshotRegionWidth = string.Empty,
            ScreenshotRegionHeight = "0",
        };
        _viewModel.Actions.Add(action);
        _viewModel.SelectedAction = action;
        _ = _captureService.CaptureMousePositionAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<(int X, int Y)?>((12, 34)));

        await _viewModel.CaptureScreenshotRegionStartAsync();

        _ = action.ScreenshotUseRegion.Should().BeTrue();
        _ = action.ScreenshotRegionX.Should().Be("12");
        _ = action.ScreenshotRegionY.Should().Be("34");
        _ = action.ScreenshotRegionWidth.Should().Be("1");
        _ = action.ScreenshotRegionHeight.Should().Be("1");
        _ = _viewModel.Status.Should().Be("[Editor_StatusCapturedRegionTopLeft] 12 34");
    }

    [Fact]
    public async Task CaptureScreenshotRegionEndAsync_NormalizesRectangleFromStartAndEnd()
    {
        var action = new EditorAction
        {
            Type = EditorActionType.Screenshot,
            ScreenshotUseRegion = true,
            ScreenshotRegionX = "30",
            ScreenshotRegionY = "40",
        };
        _viewModel.Actions.Add(action);
        _viewModel.SelectedAction = action;
        _ = _captureService.CaptureMousePositionAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<(int X, int Y)?>((10, 70)));

        await _viewModel.CaptureScreenshotRegionEndAsync();

        _ = action.ScreenshotRegionX.Should().Be("10");
        _ = action.ScreenshotRegionY.Should().Be("40");
        _ = action.ScreenshotRegionWidth.Should().Be("21");
        _ = action.ScreenshotRegionHeight.Should().Be("31");
        _ = _viewModel.Status.Should().Be("[Editor_StatusCapturedRegionBottomRight] 10 70");
    }

    [Fact]
    public async Task CaptureScreenshotRegionEndAsync_WhenCaptureCancelled_DoesNotUpdateRegion()
    {
        var action = new EditorAction
        {
            Type = EditorActionType.Screenshot,
            ScreenshotUseRegion = false,
            ScreenshotRegionX = "5",
            ScreenshotRegionY = "6",
            ScreenshotRegionWidth = "7",
            ScreenshotRegionHeight = "8",
        };
        _viewModel.Actions.Add(action);
        _viewModel.SelectedAction = action;
        _ = _captureService.CaptureMousePositionAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<(int X, int Y)?>(null));

        await _viewModel.CaptureScreenshotRegionEndAsync();

        _ = action.ScreenshotUseRegion.Should().BeFalse();
        _ = action.ScreenshotRegionX.Should().Be("5");
        _ = action.ScreenshotRegionY.Should().Be("6");
        _ = action.ScreenshotRegionWidth.Should().Be("7");
        _ = action.ScreenshotRegionHeight.Should().Be("8");
        _ = _viewModel.Status.Should().Be("[Editor_StatusCaptureCancelled]");
    }

    [Fact]
    public async Task CaptureScreenshotRegionEndAsync_WhenSamePointCaptured_UsesMinimumSize()
    {
        var action = new EditorAction
        {
            Type = EditorActionType.Screenshot,
            ScreenshotRegionX = "12",
            ScreenshotRegionY = "34",
        };
        _viewModel.Actions.Add(action);
        _viewModel.SelectedAction = action;
        _ = _captureService.CaptureMousePositionAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<(int X, int Y)?>((12, 34)));

        await _viewModel.CaptureScreenshotRegionEndAsync();

        _ = action.ScreenshotUseRegion.Should().BeTrue();
        _ = action.ScreenshotRegionX.Should().Be("12");
        _ = action.ScreenshotRegionY.Should().Be("34");
        _ = action.ScreenshotRegionWidth.Should().Be("1");
        _ = action.ScreenshotRegionHeight.Should().Be("1");
    }

    [Fact]
    public async Task CaptureMouseAsync_WhenSelectedActionUsesCurrentPosition_ConvertsToCapturedAbsolutePosition()
    {
        // Arrange
        var action = new EditorAction
        {
            Type = EditorActionType.MouseClick,
            Button = MacroMouseButton.Left,
            UseCurrentPosition = true,
        };
        _viewModel.Actions.Add(action);
        _viewModel.SelectedAction = action;
        _ = _captureService.CaptureMousePositionAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<(int X, int Y)?>((640, 480)));

        // Act
        await _viewModel.CaptureMouseAsync();

        // Assert
        _ = action.UseCurrentPosition.Should().BeFalse();
        _ = action.IsAbsolute.Should().BeTrue();
        _ = action.X.Should().Be(640);
        _ = action.Y.Should().Be(480);
        _ = _viewModel.ShowCoordinates.Should().BeTrue();
        _ = _viewModel.ShowCoordModeToggle.Should().BeTrue();
    }

    [Fact]
    public async Task CaptureKeyAsync_WhenSelectionChanges_IgnoresCapturedKey()
    {
        // Arrange
        _viewModel.AddAction();
        var firstAction = _viewModel.SelectedAction!;
        _viewModel.AddAction();
        var secondAction = _viewModel.SelectedAction!;
        _viewModel.SelectedAction = firstAction;

        var captureResult = new TaskCompletionSource<int?>(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = _captureService.CaptureKeyCodeAsync(Arg.Any<CancellationToken>()).Returns(_ => captureResult.Task);

        // Act
        var captureTask = _viewModel.CaptureKeyAsync();
        _viewModel.SelectedAction = secondAction;
        captureResult.SetResult(30);
        await captureTask;

        // Assert
        _ = firstAction.KeyCode.Should().Be(0);
        _ = secondAction.KeyCode.Should().Be(0);
        _ = _viewModel.Status.Should().Be("[Editor_StatusCaptureSelectionChanged]");
    }

    [Fact]
    public void LoadMacroSequence_LoadsImageAssetsForImageSearchSelection()
    {
        var sequence = new MacroSequence
        {
            Name = "Image Macro",
            Images = {
                ["Target_1"] = TransparentPngBase64,
            },
        };
        var converted = new List<EditorAction>
        {
            new()
            {
                Type = EditorActionType.ImageSearch,
                ImageAssetName = "Target_1",
                ScreenWidth = 100,
                ScreenHeight = 100,
                ScreenFoundVariableName = "found",
                ScreenFoundXVariableName = "found_x",
                ScreenFoundYVariableName = "found_y",
                ImageSearchSimilarity = 1.0,
                ImageSearchDownsample = 1,
            },
        };
        _ = _converter.FromMacroSequenceWithDiagnostics(sequence)
            .Returns(new EditorActionRestoreResult(converted, new List<EditorActionRestoreWarning>(), restoredFromScriptSteps: true));

        _viewModel.LoadMacroSequence(sequence);

        _ = _viewModel.HasImageAssets.Should().BeTrue();
        _ = _viewModel.ImageAssetNames.Should().Equal("Target_1");
        _ = _viewModel.SelectedAction.Should().BeSameAs(converted[0]);
        _ = _viewModel.ShowImageSearchFields.Should().BeTrue();
    }

    [Fact]
    public async Task SaveMacroAsync_WhenImageSearchAssetImported_PersistsImageAssetsOnGeneratedSequence()
    {
        var pngPath = Path.Combine(Path.GetTempPath(), $"crossmacro-target-{Guid.NewGuid():N}.png");
        await File.WriteAllBytesAsync(pngPath, Convert.FromBase64String(TransparentPngBase64), NonCancelableToken);
        try
        {
            var action = new EditorAction
            {
                Type = EditorActionType.ImageSearch,
                ScreenWidth = 100,
                ScreenHeight = 100,
                ScreenFoundVariableName = "found",
                ScreenFoundXVariableName = "found_x",
                ScreenFoundYVariableName = "found_y",
                ImageSearchSimilarity = 1.0,
                ImageSearchDownsample = 1,
            };
            _viewModel.Actions.Add(action);
            _viewModel.SelectedAction = action;
            _ = _dialogService
                .ShowOpenFileDialogAsync(Arg.Any<string>(), Arg.Any<FileDialogFilter[]>())
                .Returns(pngPath);
            await _viewModel.ImportImageAssetAsync();

            var generatedSequence = new MacroSequence
            {
                Name = "Generated",
                ScriptSteps = { "imagesearch 0 0 100 100 Target_1 found found_x found_y" },
            };
            _ = _converter
                .ToMacroSequence(Arg.Any<EditorMacroProjection>())
                .Returns(generatedSequence);
            _ = _dialogService
                .ShowSaveFileDialogAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<FileDialogFilter[]>())
                .Returns("/tmp/editor-image-search.macro");

            await _viewModel.SaveMacroAsync();

            _ = generatedSequence.Images.Should().ContainKey(action.ImageAssetName);
            _ = generatedSequence.Images[action.ImageAssetName].Should().NotBeNullOrWhiteSpace();
            await _fileManager.Received(1).SaveAsync(generatedSequence, "/tmp/editor-image-search.macro");
        }
        finally
        {
            File.Delete(pngPath);
        }
    }

    [Fact]
    public void AvailableColorVariableNames_WhenScreenReadingActionsExist_ReturnsOnlyPixelColorOutputs()
    {
        var pixelColor = new EditorAction
        {
            Type = EditorActionType.PixelColor,
            ScreenColorVariableName = "sample_color",
        };
        var waitColor = new EditorAction
        {
            Type = EditorActionType.WaitColor,
            ScreenColorVariableName = "wait_ok",
        };
        var pixelSearch = new EditorAction
        {
            Type = EditorActionType.PixelSearch,
            ScreenFoundVariableName = "found",
            ScreenFoundXVariableName = "found_x",
            ScreenFoundYVariableName = "found_y",
        };

        _viewModel.Actions.Add(pixelColor);
        _viewModel.Actions.Add(waitColor);
        _viewModel.Actions.Add(pixelSearch);
        _viewModel.NewActionType = EditorActionType.TextInput;
        _viewModel.AddAction();

        var names = _viewModel.AvailableColorVariableNames;

        _ = names.Should().Contain("sample_color");
        _ = names.Should().NotContain("wait_ok");
        _ = names.Should().NotContain("found");
        _ = names.Should().NotContain("found_x");
        _ = names.Should().NotContain("found_y");
    }

    [Fact]
    public void AvailableVariableNames_WhenPixelSearchFoundVariableChanges_RefreshesSuggestions()
    {
        var pixelSearch = new EditorAction
        {
            Type = EditorActionType.PixelSearch,
            ScreenFoundVariableName = "found",
            ScreenFoundXVariableName = "found_x",
            ScreenFoundYVariableName = "found_y",
        };
        _viewModel.Actions.Add(pixelSearch);

        _ = _viewModel.AvailableVariableNames.Should().Contain("found");

        pixelSearch.ScreenFoundVariableName = "located";

        _ = _viewModel.AvailableVariableNames.Should().Contain("located");
        _ = _viewModel.AvailableVariableNames.Should().NotContain("found");
    }

    [Theory]
    [InlineData(EditorActionType.ImageSearch)]
    [InlineData(EditorActionType.ImageClick)]
    [InlineData(EditorActionType.WaitImage)]
    public void AvailableVariableNames_WhenImageActionProducesOutputs_IncludesAllImageVariables(EditorActionType actionType)
    {
        _viewModel.Actions.Add(new EditorAction
        {
            Type = actionType,
            ScreenFoundVariableName = "image_found",
            ScreenFoundXVariableName = "image_x",
            ScreenFoundYVariableName = "image_y",
        });

        _ = _viewModel.AvailableVariableNames.Should().Contain(["image_found", "image_x", "image_y"]);
    }

    [Fact]
    public void AvailableColorVariableNames_WhenPixelColorVariableChanges_RefreshesSuggestions()
    {
        var pixelColor = new EditorAction
        {
            Type = EditorActionType.PixelColor,
            ScreenColorVariableName = "sample_color",
        };
        _viewModel.Actions.Add(pixelColor);
        _viewModel.NewActionType = EditorActionType.TextInput;
        _viewModel.AddAction();

        _ = _viewModel.AvailableColorVariableNames
            .Should()
            .Contain("sample_color");

        pixelColor.ScreenColorVariableName = "sample_color_next";

        var names = _viewModel.AvailableColorVariableNames;

        _ = names.Should().Contain("sample_color_next");
        _ = names.Should().NotContain("sample_color");
    }

    [Fact]
    public void SelectedScreenTargetColorVariableSuggestion_WritesBackToSelectedAction()
    {
        var pixelColor = new EditorAction
        {
            Type = EditorActionType.PixelColor,
            ScreenColorVariableName = "sample_color",
        };
        _viewModel.Actions.Add(pixelColor);

        _viewModel.NewActionType = EditorActionType.WaitColor;
        _viewModel.AddAction();
        var action = _viewModel.SelectedAction!;

        action.ScreenTargetColorSource = EditorActionScreenTargetColorSource.Variable;

        _viewModel.SelectedScreenTargetColorVariableSuggestion = "sample_color";

        _ = action.ScreenTargetColorVariableName.Should().Be("sample_color");
        _ = _viewModel.SelectedScreenTargetColorVariableSuggestion.Should().BeNull();
    }
}
