using System.Linq;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services;
using CrossMacro.UI.Services;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace CrossMacro.UI.Tests.Services;

public class LoadedMacroSessionTests
{
    [Fact]
    public void RenameSelected_WhenOnlyNameChanges_DoesNotRaiseSelectedMacroChanged()
    {
        var session = new LoadedMacroSession(Substitute.For<ILocalizationService>());
        var item = session.AddMacro(CreateMacro("Before"));
        var eventRaised = false;
        session.SelectedMacroChanged += (_, _) => eventRaised = true;

        session.RenameSelected("After");

        item.Name.Should().Be("After");
        eventRaised.Should().BeFalse();
    }

    [Fact]
    public void CreateSequentialCycleSnapshot_ReturnsStableCopiesInSelectedOrder()
    {
        var session = new LoadedMacroSession(Substitute.For<ILocalizationService>());
        var first = session.AddMacro(CreateMacro("First"));
        var second = session.AddMacro(CreateMacro("Second"));
        second.SequenceRepeatCount = 3;
        session.SelectedMacroItem = second;
        var originalSecondMacroId = second.Macro.Id;

        var snapshot = session.CreateSequentialCycleSnapshot();
        var updatedMacro = CreateMacro("Second Updated");
        session.UpdateSelectedMacro(updatedMacro);

        snapshot.Should().HaveCount(2);
        snapshot.Select(item => item.Name).Should().ContainInOrder("Second", "First");
        snapshot.Select(item => item.SequenceRepeatCount).Should().ContainInOrder(3, 1);
        snapshot[0].Should().NotBeSameAs(second);
        snapshot[0].SessionId.Should().Be(second.SessionId);
        snapshot[0].Macro.Should().NotBeSameAs(second.Macro);
        snapshot[0].Macro.Id.Should().Be(originalSecondMacroId);
        snapshot[0].Name.Should().Be("Second");
        snapshot[0].Macro.Name.Should().Be("Second");
        second.Name.Should().Be("Second Updated");
    }


    [Fact]
    public void UpdateSelectedMacro_WhenPayloadChanges_RaisesSelectedMacroUpdatedOnly()
    {
        var session = new LoadedMacroSession(Substitute.For<ILocalizationService>());
        session.AddMacro(CreateMacro("Original"));
        var selectionChanged = false;
        var selectedMacroUpdated = false;

        session.SelectedMacroChanged += (_, _) => selectionChanged = true;
        session.SelectedMacroUpdated += (_, _) => selectedMacroUpdated = true;

        session.UpdateSelectedMacro(CreateMacro("Updated"));

        selectionChanged.Should().BeFalse();
        selectedMacroUpdated.Should().BeTrue();
    }

    [Fact]
    public void SelectedMacroItem_WhenSelectionChanges_RaisesSelectedMacroChangedOnly()
    {
        var session = new LoadedMacroSession(Substitute.For<ILocalizationService>());
        var first = session.AddMacro(CreateMacro("First"));
        session.AddMacro(CreateMacro("Second"));
        var selectionChangedCount = 0;
        var selectedMacroUpdated = false;

        session.SelectedMacroChanged += (_, _) => selectionChangedCount++;
        session.SelectedMacroUpdated += (_, _) => selectedMacroUpdated = true;

        session.SelectedMacroItem = first;

        selectionChangedCount.Should().Be(1);
        selectedMacroUpdated.Should().BeFalse();
    }

    private static MacroSequence CreateMacro(string name)
    {
        return new MacroSequence
        {
            Name = name,
            Events = { new MacroEvent { Type = EventType.MouseMove } }
        };
    }
}
