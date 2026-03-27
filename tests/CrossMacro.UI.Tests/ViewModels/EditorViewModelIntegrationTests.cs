using System.Collections.Generic;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services;
using CrossMacro.Infrastructure.Services;
using CrossMacro.UI.Services;
using CrossMacro.UI.ViewModels;
using FluentAssertions;
using NSubstitute;

namespace CrossMacro.UI.Tests.ViewModels;

public class EditorViewModelIntegrationTests
{
    [Fact]
    public void ActionListItems_WhenUsingRuntimeConverterAndValidator_RendersContextualEndsAndIndentation()
    {
        // Arrange
        var keyCodeMapper = BuildKeyCodeMapper();
        var converter = new EditorActionConverter(keyCodeMapper);
        var validator = new EditorActionValidator(converter);
        var viewModel = new EditorViewModel(
            converter,
            validator,
            Substitute.For<ICoordinateCaptureService>(),
            Substitute.For<IMacroFileManager>(),
            Substitute.For<IDialogService>(),
            keyCodeMapper);

        // Act
        viewModel.NewActionType = EditorActionType.RepeatBlockStart;
        viewModel.AddAction();

        viewModel.SelectedAction = viewModel.Actions[0];
        viewModel.NewActionType = EditorActionType.IfBlockStart;
        viewModel.AddAction();

        // Assert
        viewModel.ActionListItems.Should().HaveCount(4);
        viewModel.ActionListItems[0].IndentLevel.Should().Be(0);
        viewModel.ActionListItems[1].IndentLevel.Should().Be(1);
        viewModel.ActionListItems[2].DisplayName.Should().Be("End If");
        viewModel.ActionListItems[2].IndentLevel.Should().Be(1);
        viewModel.ActionListItems[3].DisplayName.Should().Be("End Repeat");
        viewModel.ActionListItems[3].IndentLevel.Should().Be(0);
    }

    private static IKeyCodeMapper BuildKeyCodeMapper()
    {
        var mapper = Substitute.For<IKeyCodeMapper>();
        mapper.GetKeyCode(Arg.Any<string>()).Returns(-1);
        mapper.GetKeyCode("Shift").Returns(42);
        mapper.GetKeyCode("AltGr").Returns(100);
        mapper.GetKeyCodeForCharacter(Arg.Any<char>()).Returns(call => call.Arg<char>());
        mapper.RequiresShift(Arg.Any<char>()).Returns(call => char.IsUpper(call.Arg<char>()));
        mapper.RequiresAltGr(Arg.Any<char>()).Returns(false);
        mapper.GetKeyName(Arg.Any<int>()).Returns("A");
        return mapper;
    }
}
