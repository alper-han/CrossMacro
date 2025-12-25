using System;
using System.Threading.Tasks;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services;
using CrossMacro.UI.Services;
using CrossMacro.UI.ViewModels;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace CrossMacro.UI.Tests.ViewModels;

public class FilesViewModelTests
{
    private readonly IMacroFileManager _fileManager;
    private readonly IDialogService _dialogService;
    private readonly FilesViewModel _viewModel;

    public FilesViewModelTests()
    {
        _fileManager = Substitute.For<IMacroFileManager>();
        _dialogService = Substitute.For<IDialogService>();
        _viewModel = new FilesViewModel(_fileManager, _dialogService);
    }

    [Fact]
    public void Construction_InitializesProperties()
    {
        _viewModel.Status.Should().Be("Ready");
        _viewModel.MacroName.Should().Be("New Macro");
        _viewModel.HasRecordedMacro.Should().BeFalse();
        _viewModel.GetCurrentMacro().Should().BeNull();
    }

    [Fact]
    public void SetMacro_UpdatesProperties()
    {
        // Arrange
        var macro = new MacroSequence { Name = "Test Macro", Events = { new MacroEvent() } };

        // Act
        _viewModel.SetMacro(macro);

        // Assert
        _viewModel.GetCurrentMacro().Should().Be(macro);
        _viewModel.HasRecordedMacro.Should().BeTrue();
        // Note: SetMacro updates macro.Name to ViewModel.MacroName, not vice versa usually.
        // Let's check code: if (_currentMacro != null) _currentMacro.Name = MacroName;
        // So macro.Name becomes "New Macro" (default VM name).
        macro.Name.Should().Be("New Macro"); 
    }

    [Fact]
    public async Task SaveMacroAsync_WhenNoMacro_DoesNothing()
    {
        // Act
        await _viewModel.SaveMacroAsync();

        // Assert
        await _dialogService.DidNotReceive().ShowSaveFileDialogAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<FileDialogFilter[]>());
    }

    [Fact]
    public async Task SaveMacroAsync_WhenCancelled_UpdatesStatus()
    {
        // Arrange
        _viewModel.SetMacro(new MacroSequence { Events = { new MacroEvent() } });
        _dialogService.ShowSaveFileDialogAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<FileDialogFilter[]>())
            .Returns(Task.FromResult<string?>(null));

        // Act
        await _viewModel.SaveMacroAsync();

        // Assert
        _viewModel.Status.Should().Be("Save cancelled");
        await _fileManager.DidNotReceive().SaveAsync(Arg.Any<MacroSequence>(), Arg.Any<string>());
    }

    [Fact]
    public async Task SaveMacroAsync_WhenSuccessful_SavesAndUpdatesStatus()
    {
         // Arrange
        var macro = new MacroSequence { Events = { new MacroEvent() } };
        _viewModel.SetMacro(macro);
        _viewModel.MacroName = "MyMacro";
        
        _dialogService.ShowSaveFileDialogAsync("Save Macro", "MyMacro.macro", Arg.Any<FileDialogFilter[]>())
            .Returns(Task.FromResult<string?>("/path/to/MyMacro.macro"));

        // Act
        await _viewModel.SaveMacroAsync();

        // Assert
        await _fileManager.Received(1).SaveAsync(macro, "/path/to/MyMacro.macro");
        _viewModel.Status.Should().Contain("Saved to");
    }

    [Fact]
    public async Task LoadMacroAsync_WhenCancelled_UpdatesStatus()
    {
        // Arrange
        _dialogService.ShowOpenFileDialogAsync(Arg.Any<string>(), Arg.Any<FileDialogFilter[]>())
            .Returns(Task.FromResult<string?>(null));

        // Act
        await _viewModel.LoadMacroAsync();

        // Assert
        _viewModel.Status.Should().Be("Load cancelled");
        await _fileManager.DidNotReceive().LoadAsync(Arg.Any<string>());
    }

    [Fact]
    public async Task LoadMacroAsync_WhenSuccessful_LoadsAndUpdatesStatus()
    {
        // Arrange
        var macro = new MacroSequence { Name = "LoadedMacro", Events = { new MacroEvent() } };
        _dialogService.ShowOpenFileDialogAsync(Arg.Any<string>(), Arg.Any<FileDialogFilter[]>())
            .Returns(Task.FromResult<string?>("/path/to/file.macro"));
        
        _fileManager.LoadAsync("/path/to/file.macro").Returns(Task.FromResult<MacroSequence?>(macro));

        bool eventFired = false;
        _viewModel.MacroLoaded += (s, m) => {
            eventFired = true;
            m.Should().Be(macro);
        };

        // Act
        await _viewModel.LoadMacroAsync();

        // Assert
        _viewModel.GetCurrentMacro().Should().Be(macro);
        _viewModel.MacroName.Should().Be("LoadedMacro");
        _viewModel.HasRecordedMacro.Should().BeTrue();
        _viewModel.Status.Should().Contain("Loaded");
        eventFired.Should().BeTrue();
    }
}
