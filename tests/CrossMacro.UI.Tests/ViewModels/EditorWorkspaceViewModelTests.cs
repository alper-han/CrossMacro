namespace CrossMacro.UI.Tests.ViewModels;

public sealed class EditorWorkspaceViewModelTests : IDisposable
{
    private readonly IEditorActionConverter _converter = Substitute.For<IEditorActionConverter>();
    private readonly IEditorActionValidator _validator = Substitute.For<IEditorActionValidator>();
    private readonly IMacroFileManager _fileManager = Substitute.For<IMacroFileManager>();
    private readonly IDialogService _dialogService = Substitute.For<IDialogService>();
    private readonly ILocalizationService _localizationService = Substitute.For<ILocalizationService>();
    private readonly EditorWorkspaceViewModel _workspace;

    public EditorWorkspaceViewModelTests()
    {
        _ = _validator.ValidateAll(Arg.Any<IEnumerable<EditorAction>>()).Returns((true, new List<string>()));
        _ = _localizationService.CurrentCulture.Returns(CultureInfo.InvariantCulture);
        _ = _localizationService[Arg.Any<string>()].Returns(call => call.Arg<string>() switch
        {
            "Editor_DefaultMacroName" => "Manual Macro",
            "Editor_StatusReady" => "Ready",
            "Editor_ActionType_MouseClick" => "Mouse Click",
            "Editor_ActionGroup_Mouse" => "Mouse",
            _ => call.Arg<string>(),
        });

        var initialDocument = new EditorViewModel(
            _converter,
            _validator,
            Substitute.For<ICoordinateCaptureService>(),
            _fileManager,
            _dialogService,
            Substitute.For<IKeyCodeMapper>(),
            Substitute.For<IMacroPlayer>(),
            _localizationService,
            new EditorActionDisplayFormatter(_localizationService));
        _workspace = new EditorWorkspaceViewModel(initialDocument, _dialogService, _localizationService);
    }

    [Fact]
    public void Construction_CreatesAndSelectsOneCleanUntitledDocument()
    {
        _ = _workspace.Documents.Should().ContainSingle();
        _ = _workspace.ActiveDocument.Should().BeSameAs(_workspace.Documents[0]);
        _ = _workspace.ActiveDocument.TabTitle.Should().Be("Untitled 1");
        _ = _workspace.ActiveDocument.IsEmptyAndClean.Should().BeTrue();
    }

    [Fact]
    public void NewTab_CreatesAnIndependentActiveDocument()
    {
        var first = _workspace.ActiveDocument!;
        first.AddAction();

        _workspace.NewTab();

        _ = _workspace.Documents.Should().HaveCount(2);
        _ = _workspace.ActiveDocument.Should().NotBeSameAs(first);
        _ = _workspace.ActiveDocument.TabTitle.Should().Be("Untitled 2");
        _ = _workspace.ActiveDocument.Actions.Should().BeEmpty();
        _ = first.Actions.Should().ContainSingle();
    }

    [Fact]
    public void MoveDocument_ReordersOpenDocuments()
    {
        var first = _workspace.ActiveDocument!;
        _workspace.NewTab();
        var second = _workspace.ActiveDocument!;
        _workspace.NewTab();
        var third = _workspace.ActiveDocument!;

        _workspace.MoveDocument(third, 0);

        _ = _workspace.Documents.Should().Equal(third, first, second);
    }

    [Fact]
    public void CommitTabRename_UpdatesTheMacroNameAndMarksTheDocumentDirty()
    {
        var document = _workspace.ActiveDocument!;

        document.BeginTabRename();
        document.TabRenameText = "Renamed Macro";
        document.CommitTabRename();

        _ = document.MacroName.Should().Be("Renamed Macro");
        _ = document.TabTitle.Should().Be("Renamed Macro");
        _ = document.IsDirty.Should().BeTrue();
        _ = document.IsTabRenameInProgress.Should().BeFalse();
    }

    [Fact]
    public async Task CloseDocumentAsync_WhenDirtyAndDiscarded_RemovesDocument()
    {
        var document = _workspace.ActiveDocument!;
        document.AddAction();
        _ = document.IsDirty.Should().BeTrue();
        _ = _dialogService.ShowUnsavedChangesAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(UnsavedChangesChoice.Discard);

        await _workspace.CloseDocumentAsync(document);

        _ = _workspace.Documents.Should().BeEmpty();
        _ = _workspace.ActiveDocument.Should().BeNull();
    }

    [Fact]
    public async Task CloseDocumentAsync_WhenDirtyAndCancelled_KeepsDocument()
    {
        var document = _workspace.ActiveDocument!;
        document.AddAction();
        _ = _dialogService.ShowUnsavedChangesAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(UnsavedChangesChoice.Cancel);

        await _workspace.CloseDocumentAsync(document);

        _ = _workspace.Documents.Should().ContainSingle().Which.Should().BeSameAs(document);
    }

    [Fact]
    public async Task ConfirmApplicationCloseAsync_WhenAnyDirtyDocumentCancels_ReturnsFalseWithoutRemovingDocuments()
    {
        _workspace.ActiveDocument!.AddAction();
        _workspace.NewTab();
        _workspace.ActiveDocument.AddAction();
        _ = _dialogService.ShowUnsavedChangesAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(UnsavedChangesChoice.Discard, UnsavedChangesChoice.Cancel);

        var canClose = await _workspace.ConfirmApplicationCloseAsync();

        _ = canClose.Should().BeFalse();
        _ = _workspace.Documents.Should().HaveCount(2);
    }

    [Fact]
    public async Task CloseAllDocumentsAsync_WhenLaterDocumentCancels_DoesNotPartiallyCloseWorkspace()
    {
        var first = _workspace.ActiveDocument!;
        first.AddAction();
        _workspace.NewTab();
        var second = _workspace.ActiveDocument!;
        second.AddAction();
        _ = _dialogService.ShowUnsavedChangesAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(UnsavedChangesChoice.Discard, UnsavedChangesChoice.Cancel);

        await _workspace.CloseAllDocumentsAsync();

        _ = _workspace.Documents.Should().Equal(first, second);
        _ = _workspace.ActiveDocument.Should().BeSameAs(second);
    }

    [Fact]
    public async Task OpenMacroFileAsync_ReusesCleanDocumentAndSelectsExistingFileTab()
    {
        var sourcePath = Path.Combine(Path.GetTempPath(), "workspace-load.macro");
        var sequence = new MacroSequence { Name = "Loaded Macro" };
        var action = new EditorAction { Type = EditorActionType.MouseClick };
        _ = _fileManager.LoadAsync(sourcePath).Returns(sequence);
        _ = _converter.FromMacroSequenceWithDiagnostics(sequence)
            .Returns(new EditorActionRestoreResult([action], [], restoredFromScriptSteps: true));

        await _workspace.OpenMacroFileAsync(sourcePath);
        var loadedDocument = _workspace.ActiveDocument!;
        _workspace.NewTab();

        await _workspace.OpenMacroFileAsync(sourcePath);

        _ = _workspace.Documents.Should().HaveCount(2);
        _ = _workspace.ActiveDocument.Should().BeSameAs(loadedDocument);
        _ = loadedDocument.SourcePath.Should().Be(Path.GetFullPath(sourcePath));
        _ = loadedDocument.Actions.Should().ContainSingle();
        _ = await _fileManager.Received(1).LoadAsync(Path.GetFullPath(sourcePath));
    }

    [Fact]
    public async Task OpenMacroFileAsync_WithDefaultMacroName_UsesFileNameForTabTitle()
    {
        var sourcePath = Path.Combine(Path.GetTempPath(), "workspace-default-name.macro");
        var sequence = new MacroSequence { Name = "Manual Macro" };
        _ = _fileManager.LoadAsync(sourcePath).Returns(sequence);
        _ = _converter.FromMacroSequenceWithDiagnostics(sequence)
            .Returns(new EditorActionRestoreResult([], [], restoredFromScriptSteps: true));

        await _workspace.OpenMacroFileAsync(sourcePath);

        _ = _workspace.ActiveDocument!.TabTitle.Should().Be("workspace-default-name");
    }

    public void Dispose()
    {
        _workspace.Dispose();
    }
}
