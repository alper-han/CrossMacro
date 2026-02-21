using CrossMacro.Core.Models;
using CrossMacro.Core.Services;
using CrossMacro.UI.Services;
using CrossMacro.UI.ViewModels;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace CrossMacro.UI.Tests.ViewModels;

public class EditorViewModelTests
{
    private readonly IEditorActionConverter _converter;
    private readonly IEditorActionValidator _validator;
    private readonly ICoordinateCaptureService _captureService;
    private readonly IMacroFileManager _fileManager;
    private readonly IDialogService _dialogService;
    private readonly IKeyCodeMapper _keyCodeMapper;
    private readonly EditorViewModel _viewModel;

    public EditorViewModelTests()
    {
        _converter = Substitute.For<IEditorActionConverter>();
        _validator = Substitute.For<IEditorActionValidator>();
        _captureService = Substitute.For<ICoordinateCaptureService>();
        _fileManager = Substitute.For<IMacroFileManager>();
        _dialogService = Substitute.For<IDialogService>();
        _keyCodeMapper = Substitute.For<IKeyCodeMapper>();
        _keyCodeMapper.GetKeyName(Arg.Any<int>()).Returns("A");

        _validator.ValidateAll(Arg.Any<IEnumerable<EditorAction>>()).Returns((true, new List<string>()));

        _viewModel = new EditorViewModel(
            _converter,
            _validator,
            _captureService,
            _fileManager,
            _dialogService,
            _keyCodeMapper);
    }

    [Fact]
    public void AddAction_AddsActionAndSelectsIt()
    {
        // Act
        _viewModel.AddAction();

        // Assert
        _viewModel.Actions.Should().HaveCount(1);
        _viewModel.SelectedAction.Should().NotBeNull();
        _viewModel.HasActions.Should().BeTrue();
        _viewModel.Status.Should().Contain("Added");
    }

    [Fact]
    public void RemoveAction_WhenSelected_RemovesAndClearsSelection()
    {
        // Arrange
        _viewModel.AddAction();
        _viewModel.Actions.Should().HaveCount(1);

        // Act
        _viewModel.RemoveAction();

        // Assert
        _viewModel.Actions.Should().BeEmpty();
        _viewModel.SelectedAction.Should().BeNull();
        _viewModel.HasActions.Should().BeFalse();
        _viewModel.Status.Should().Be("Removed action");
    }

    [Fact]
    public void UndoAndRedo_RestorePreviousStates()
    {
        // Arrange
        _viewModel.AddAction();
        _viewModel.AddAction();
        _viewModel.Actions.Should().HaveCount(2);

        // Act
        _viewModel.Undo();

        // Assert
        _viewModel.Actions.Should().HaveCount(1);
        _viewModel.Status.Should().Be("Undone");

        // Act
        _viewModel.Redo();

        // Assert
        _viewModel.Actions.Should().HaveCount(2);
        _viewModel.Status.Should().Be("Redone");
    }

    [Fact]
    public async Task SaveMacroAsync_WhenNoActions_ShowsMessage()
    {
        // Act
        await _viewModel.SaveMacroAsync();

        // Assert
        await _dialogService.Received(1).ShowMessageAsync(
            "No Actions",
            Arg.Is<string>(m => m.Contains("Please add at least one action", StringComparison.Ordinal)),
            "OK");
    }

    [Fact]
    public async Task SaveMacroAsync_WhenValidationFails_ShowsValidationMessage()
    {
        // Arrange
        _viewModel.AddAction();
        _validator.ValidateAll(Arg.Any<IEnumerable<EditorAction>>())
            .Returns((false, new List<string> { "Error A", "Error B" }));

        // Act
        await _viewModel.SaveMacroAsync();

        // Assert
        await _dialogService.Received(1).ShowMessageAsync(
            "Validation Errors",
            Arg.Is<string>(m => m.Contains("Error A", StringComparison.Ordinal)),
            "OK");
        _viewModel.Status.Should().Contain("Validation failed");
    }

    [Fact]
    public void LoadMacroSequence_LoadsConvertedActionsAndName()
    {
        // Arrange
        var sequence = new MacroSequence { Name = "Loaded Macro", SkipInitialZeroZero = true };
        var converted = new List<EditorAction>
        {
            new() { Type = EditorActionType.MouseMove, X = 10, Y = 20 }
        };
        _converter.FromMacroSequence(sequence).Returns(converted);

        // Act
        _viewModel.LoadMacroSequence(sequence);

        // Assert
        _viewModel.MacroName.Should().Be("Loaded Macro");
        _viewModel.Actions.Should().HaveCount(1);
        _viewModel.SelectedAction.Should().NotBeNull();
        _viewModel.HasActions.Should().BeTrue();
    }

    [Fact]
    public void DelayVisibility_TogglesBetweenFixedAndRandomInputs()
    {
        // Arrange
        _viewModel.NewActionType = EditorActionType.Delay;
        _viewModel.AddAction();

        // Assert
        _viewModel.ShowDelay.Should().BeTrue();
        _viewModel.ShowFixedDelayInput.Should().BeTrue();
        _viewModel.ShowRandomDelayOptions.Should().BeFalse();

        // Act
        _viewModel.SelectedAction!.UseRandomDelay = true;

        // Assert
        _viewModel.ShowFixedDelayInput.Should().BeFalse();
        _viewModel.ShowRandomDelayOptions.Should().BeTrue();
    }
}
