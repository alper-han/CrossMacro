using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services;
using CrossMacro.Infrastructure.Services;
using CrossMacro.UI.Services;
using CrossMacro.UI.ViewModels;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace CrossMacro.UI.Tests.ViewModels;

public class TextExpansionViewModelTests
{
    private readonly ITextExpansionStorageService _storageService;
    private readonly IDialogService _dialogService;
    private readonly IEnvironmentInfoProvider _environmentInfoProvider;
    private readonly TextExpansionViewModel _viewModel;

    public TextExpansionViewModelTests()
    {
        _storageService = Substitute.For<ITextExpansionStorageService>();
        _dialogService = Substitute.For<IDialogService>();
        _environmentInfoProvider = Substitute.For<IEnvironmentInfoProvider>();
        
        // Setup initial load
        _storageService.LoadAsync().Returns(new List<TextExpansion>());

        _viewModel = new TextExpansionViewModel(_storageService, _dialogService, _environmentInfoProvider);
    }

    [Fact]
    public async Task Construction_LoadsExpansions()
    {
        // Arrange
        var list = new List<TextExpansion> { new TextExpansion(":test", "result") };
        _storageService.LoadAsync().Returns(list);
        
        // Re-create VM to trigger constructor load
        var vm = new TextExpansionViewModel(_storageService, _dialogService, _environmentInfoProvider);
        
        // Wait for async load deterministically
        await vm.InitializationTask; 

        // Assert
        vm.Expansions.Should().HaveCount(1);
        vm.Expansions[0].Trigger.Should().Be(":test");
    }

    [Fact]
    public async Task AddExpansion_AddsToListAndSaves()
    {
        // Arrange
        _viewModel.TriggerInput = ":new";
        _viewModel.ReplacementInput = "value";

        // Act
        // Execute the command directly
        if (_viewModel.AddExpansionCommand.CanExecute(null))
        {
            await _viewModel.AddExpansionCommand.ExecuteAsync(null);
        }

        // Assert
        _viewModel.Expansions.Should().HaveCount(1);
        _viewModel.Expansions[0].Trigger.Should().Be(":new");
        _viewModel.TriggerInput.Should().BeEmpty(); // Should clear input
        
        await _storageService.Received(1).SaveAsync(Arg.Any<IEnumerable<TextExpansion>>());
    }

    [Fact]
    public void AddExpansion_CanExecute_ValidatesInput()
    {
         // Arrange
        _viewModel.TriggerInput = "";
        _viewModel.ReplacementInput = "val";
        _viewModel.AddExpansionCommand.CanExecute(null).Should().BeFalse();

        _viewModel.TriggerInput = ":key";
        _viewModel.ReplacementInput = "";
        _viewModel.AddExpansionCommand.CanExecute(null).Should().BeFalse();

        _viewModel.TriggerInput = ":key";
        _viewModel.ReplacementInput = "val";
        _viewModel.AddExpansionCommand.CanExecute(null).Should().BeTrue();
    }

    [Fact]
    public async Task RemoveExpansion_WhenConfirmed_RemovesAndSaves()
    {
        // Arrange
        var expansion = new TextExpansion(":del", "value");
        _viewModel.Expansions.Add(expansion);
        
        _dialogService.ShowConfirmationAsync(Arg.Any<string>(), Arg.Any<string>(), "Yes", "No")
            .Returns(Task.FromResult(true));

        // Act
        await _viewModel.RemoveExpansionCommand.ExecuteAsync(expansion);

        // Assert
        _viewModel.Expansions.Should().BeEmpty();
        await _storageService.Received(1).SaveAsync(Arg.Any<IEnumerable<TextExpansion>>());
    }

    [Fact]
    public async Task RemoveExpansion_WhenCancelled_DoesNotRemove()
    {
        // Arrange
        var expansion = new TextExpansion(":keep", "value");
        _viewModel.Expansions.Add(expansion);
        
        _dialogService.ShowConfirmationAsync(Arg.Any<string>(), Arg.Any<string>(), "Yes", "No")
            .Returns(Task.FromResult(false));

        // Act
        await _viewModel.RemoveExpansionCommand.ExecuteAsync(expansion);

        // Assert
        _viewModel.Expansions.Should().HaveCount(1);
        await _storageService.DidNotReceive().SaveAsync(Arg.Any<IEnumerable<TextExpansion>>());
    }

    [Fact]
    public async Task ToggleExpansion_SavesStart()
    {
        // Arrange
        var expansion = new TextExpansion(":toggle", "val");
        _viewModel.Expansions.Add(expansion);

        // Act
        await _viewModel.ToggleExpansionCommand.ExecuteAsync(expansion);

        // Assert
        // We just verify it saves the current state
        await _storageService.Received(1).SaveAsync(Arg.Any<IEnumerable<TextExpansion>>());
    }

    [Theory]
    [InlineData(DisplayEnvironment.LinuxX11, true)]
    [InlineData(DisplayEnvironment.LinuxWayland, true)]
    [InlineData(DisplayEnvironment.Windows, false)]
    [InlineData(DisplayEnvironment.MacOS, false)]
    public void IsPasteMethodVisible_ReflectsEnvironment(DisplayEnvironment environment, bool expected)
    {
        // Arrange
        var envProvider = Substitute.For<IEnvironmentInfoProvider>();
        envProvider.CurrentEnvironment.Returns(environment);
        var vm = new TextExpansionViewModel(_storageService, _dialogService, envProvider);

        // Assert
        vm.IsPasteMethodVisible.Should().Be(expected);
    }

    [Fact]
    public async Task AddExpansion_ResetsSelectedPasteMethodToDefault()
    {
        // Arrange
        _viewModel.SelectedPasteMethod = PasteMethod.ShiftInsert;
        _viewModel.TriggerInput = ":x";
        _viewModel.ReplacementInput = "value";

        // Act
        await _viewModel.AddExpansionCommand.ExecuteAsync(null);

        // Assert
        _viewModel.SelectedPasteMethod.Should().Be(PasteMethod.CtrlV);
    }

    [Fact]
    public async Task ToggleExpansion_WhenExpansionIsNull_DoesNotSave()
    {
        // Act
        await _viewModel.ToggleExpansionCommand.ExecuteAsync(null);

        // Assert
        await _storageService.DidNotReceive().SaveAsync(Arg.Any<IEnumerable<TextExpansion>>());
    }
}
