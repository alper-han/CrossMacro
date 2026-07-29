
namespace CrossMacro.UI.Tests.Services;

public sealed class LoadedMacroSessionTests
{
    [Fact]
    public void RenameSelected_WhenOnlyNameChanges_DoesNotRaiseSelectedMacroChanged()
    {
        var session = new LoadedMacroSession(Substitute.For<ILocalizationService>());
        var item = session.AddMacro(CreateMacro("Before"));
        var eventRaised = false;
        session.SelectedMacroChanged += (_, _) => eventRaised = true;

        session.RenameSelected("After");

        _ = item.Name.Should().Be("After");
        _ = eventRaised.Should().BeFalse();
    }

    [Fact]
    public void CreateSequentialCycleSnapshot_ReturnsStableCopiesInSelectedOrder()
    {
        var session = new LoadedMacroSession(Substitute.For<ILocalizationService>());
        _ = session.AddMacro(CreateMacro("First"));
        var second = session.AddMacro(CreateMacro("Second"));
        second.SequenceRepeatCount = 3;
        session.SelectedMacroItem = second;
        var originalSecondMacroId = second.Macro.Id;

        var snapshot = session.CreateSequentialCycleSnapshot();
        var updatedMacro = CreateMacro("Second Updated");
        _ = session.UpdateSelectedMacro(updatedMacro);

        _ = snapshot.Should().HaveCount(2);
        _ = snapshot.Select(item => item.Name).Should().ContainInOrder("Second", "First");
        _ = snapshot.Select(item => item.SequenceRepeatCount).Should().ContainInOrder(3, 1);
        _ = snapshot[0].Should().NotBeSameAs(second);
        _ = snapshot[0].SessionId.Should().Be(second.SessionId);
        _ = snapshot[0].Macro.Should().NotBeSameAs(second.Macro);
        _ = snapshot[0].Macro.Id.Should().Be(originalSecondMacroId);
        _ = snapshot[0].Name.Should().Be("Second");
        _ = snapshot[0].Macro.Name.Should().Be("Second");
        _ = second.Name.Should().Be("Second Updated");
    }


    [Fact]
    public void UpdateSelectedMacro_WhenPayloadChanges_RaisesSelectedMacroUpdatedOnly()
    {
        var session = new LoadedMacroSession(Substitute.For<ILocalizationService>());
        _ = session.AddMacro(CreateMacro("Original"));
        var selectionChanged = false;
        var selectedMacroUpdated = false;

        session.SelectedMacroChanged += (_, _) => selectionChanged = true;
        session.SelectedMacroUpdated += (_, _) => selectedMacroUpdated = true;

        _ = session.UpdateSelectedMacro(CreateMacro("Updated"));

        _ = selectionChanged.Should().BeFalse();
        _ = selectedMacroUpdated.Should().BeTrue();
    }

    [Fact]
    public void SelectedMacroItem_WhenSelectionChanges_RaisesSelectedMacroChangedOnly()
    {
        var session = new LoadedMacroSession(Substitute.For<ILocalizationService>());
        var first = session.AddMacro(CreateMacro("First"));
        _ = session.AddMacro(CreateMacro("Second"));
        var selectionChangedCount = 0;
        var selectedMacroUpdated = false;

        session.SelectedMacroChanged += (_, _) => selectionChangedCount++;
        session.SelectedMacroUpdated += (_, _) => selectedMacroUpdated = true;

        session.SelectedMacroItem = first;

        _ = selectionChangedCount.Should().Be(1);
        _ = selectedMacroUpdated.Should().BeFalse();
    }

    private static MacroSequence CreateMacro(string name)
    {
        return new MacroSequence
        {
            Name = name,
            Events = { new MacroEvent { Type = EventType.MouseMove } },
        };
    }
}
