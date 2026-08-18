
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

    [Fact]
    public void CreateAndRestoreSnapshot_PreservesSessionItemsSelectionAndPlaybackMode()
    {
        var localization = Substitute.For<ILocalizationService>();
        var source = new LoadedMacroSession(localization);
        var first = source.AddMacro(CreateMacro("First"), "/tmp/first.macro");
        first.SequenceRepeatCount = 3;
        var second = source.AddMacro(CreateMacro("Second"));
        source.SelectedMacroItem = first;
        source.PlaybackMode = LoadedMacroPlaybackMode.SequentialCycle;

        var snapshot = source.CreateSnapshot();
        var restored = new LoadedMacroSession(localization);
        restored.RestoreSnapshot(snapshot);

        _ = restored.LoadedMacros.Select(item => item.Name).Should().ContainInOrder("First", "Second");
        _ = restored.LoadedMacros[0].SessionId.Should().Be(first.SessionId);
        _ = restored.LoadedMacros[0].SourcePath.Should().Be("/tmp/first.macro");
        _ = restored.LoadedMacros[0].SequenceRepeatCount.Should().Be(3);
        _ = restored.SelectedMacroItem!.SessionId.Should().Be(first.SessionId);
        _ = restored.PlaybackMode.Should().Be(LoadedMacroPlaybackMode.SequentialCycle);
        _ = restored.LoadedMacros[1].Macro.Should().NotBeSameAs(second.Macro);
        _ = restored.LoadedMacros[1].Macro.Should().BeEquivalentTo(second.Macro);
    }

    [Fact]
    public void RestoreSnapshot_WhenPlaybackModeIsUnknown_UsesSelectedOnly()
    {
        var session = new LoadedMacroSession(Substitute.For<ILocalizationService>());

        session.RestoreSnapshot(new LoadedMacroSessionSnapshot([], null, PlaybackMode: 99));

        _ = session.PlaybackMode.Should().Be(LoadedMacroPlaybackMode.SelectedOnly);
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
