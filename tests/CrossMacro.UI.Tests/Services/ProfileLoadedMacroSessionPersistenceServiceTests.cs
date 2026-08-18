namespace CrossMacro.UI.Tests.Services;

public sealed class ProfileLoadedMacroSessionPersistenceServiceTests
{
    [Fact]
    public async Task FlushAndReloadAsync_KeepLoadedMacrosIsolatedPerProfile()
    {
        var loadedMacroSession = new LoadedMacroSession(Substitute.For<ILocalizationService>());
        var store = Substitute.For<IProfileLoadedMacroSessionStore>();
        var macroFileManager = Substitute.For<IMacroFileManager>();
        var firstDirectory = "/profiles/first";
        var secondDirectory = "/profiles/second";
        var secondSessionId = Guid.NewGuid();
        _ = store.LoadAsync(firstDirectory, Arg.Any<CancellationToken>())
            .Returns(LoadedMacroSessionSnapshot.Empty);
        _ = store.LoadAsync(secondDirectory, Arg.Any<CancellationToken>())
            .Returns(new LoadedMacroSessionSnapshot(
            [
                new LoadedMacroSessionItemSnapshot(
                    secondSessionId,
                    CreateMacro("Second profile"),
                    "/tmp/second.macro",
                    2),
            ],
            secondSessionId,
            PlaybackMode: (int)LoadedMacroPlaybackMode.SequentialCycle));
        await using var service = new ProfileLoadedMacroSessionPersistenceService(loadedMacroSession, store, macroFileManager);

        await service.ReloadAsync(firstDirectory, CancellationToken.None);
        var first = loadedMacroSession.AddMacro(CreateMacro("First profile"), "/tmp/first.macro");
        first.SequenceRepeatCount = 3;
        await service.FlushAsync(CancellationToken.None);

        await service.ReloadAsync(secondDirectory, CancellationToken.None);

        await store.Received(1).SaveAsync(
            firstDirectory,
            Arg.Is<LoadedMacroSessionSnapshot>(snapshot =>
                snapshot.Items.Count == 1
                && snapshot.Items[0].SessionId == first.SessionId
                && snapshot.Items[0].Macro.Name == "First profile"
                && snapshot.Items[0].SequenceRepeatCount == 3),
            Arg.Any<CancellationToken>());
        _ = loadedMacroSession.LoadedMacros.Should().ContainSingle();
        _ = loadedMacroSession.SelectedMacroItem!.SessionId.Should().Be(secondSessionId);
        _ = loadedMacroSession.SelectedMacroItem.SequenceRepeatCount.Should().Be(2);
        _ = loadedMacroSession.PlaybackMode.Should().Be(LoadedMacroPlaybackMode.SequentialCycle);
    }

    [Fact]
    public async Task ReloadAsync_WhenStoreFails_ClearsTheSessionWithoutOverwritingTheTargetProfile()
    {
        var loadedMacroSession = new LoadedMacroSession(Substitute.For<ILocalizationService>());
        _ = loadedMacroSession.AddMacro(CreateMacro("Stale macro"));
        var store = Substitute.For<IProfileLoadedMacroSessionStore>();
        var macroFileManager = Substitute.For<IMacroFileManager>();
        _ = store.LoadAsync("/profiles/broken", Arg.Any<CancellationToken>())
            .Returns<Task<LoadedMacroSessionSnapshot>>(_ => throw new InvalidDataException("invalid session"));
        var service = new ProfileLoadedMacroSessionPersistenceService(loadedMacroSession, store, macroFileManager);

        await service.ReloadAsync("/profiles/broken", CancellationToken.None);
        _ = loadedMacroSession.LoadedMacros.Should().BeEmpty();
        _ = loadedMacroSession.AddMacro(CreateMacro("New macro"));
        await service.FlushAsync(CancellationToken.None);
        await service.DisposeAsync();

        _ = loadedMacroSession.LoadedMacros.Should().ContainSingle();
        await store.DidNotReceive().SaveAsync(
            "/profiles/broken",
            Arg.Any<LoadedMacroSessionSnapshot>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReloadAsync_PersistsAnEditMadeWhileTheTargetProfileLoads()
    {
        var loadedMacroSession = new LoadedMacroSession(Substitute.For<ILocalizationService>());
        var store = Substitute.For<IProfileLoadedMacroSessionStore>();
        var macroFileManager = Substitute.For<IMacroFileManager>();
        var targetLoad = new TaskCompletionSource<LoadedMacroSessionSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        const string sourceDirectory = "/profiles/source";
        const string targetDirectory = "/profiles/target";
        _ = store.LoadAsync(sourceDirectory, Arg.Any<CancellationToken>()).Returns(LoadedMacroSessionSnapshot.Empty);
        _ = store.LoadAsync(targetDirectory, Arg.Any<CancellationToken>()).Returns(targetLoad.Task);
        await using var service = new ProfileLoadedMacroSessionPersistenceService(loadedMacroSession, store, macroFileManager);

        await service.ReloadAsync(sourceDirectory, CancellationToken.None);
        _ = loadedMacroSession.AddMacro(CreateMacro("Original"));
        await service.FlushAsync(CancellationToken.None);

        var reload = service.ReloadAsync(targetDirectory, CancellationToken.None);
        loadedMacroSession.RenameSelected("Edited while switching");
        _ = targetLoad.TrySetResult(LoadedMacroSessionSnapshot.Empty);
        await reload;

        await store.Received().SaveAsync(
            sourceDirectory,
            Arg.Is<LoadedMacroSessionSnapshot>(snapshot => snapshot.Items.Single().Macro.Name == "Edited while switching"),
            CancellationToken.None);
        _ = loadedMacroSession.LoadedMacros.Should().BeEmpty();
    }

    [Fact]
    public async Task FlushAsync_WritesAProfileMacroFileForAnUnsavedSessionMacro()
    {
        var profileDirectory = Path.Combine(Path.GetTempPath(), $"CrossMacroProfileMacroTests_{Guid.NewGuid():N}");
        try
        {
            var loadedMacroSession = new LoadedMacroSession(Substitute.For<ILocalizationService>());
            var store = Substitute.For<IProfileLoadedMacroSessionStore>();
            var macroFileManager = Substitute.For<IMacroFileManager>();
            _ = store.LoadAsync(profileDirectory, CancellationToken.None).Returns(LoadedMacroSessionSnapshot.Empty);
            await using var service = new ProfileLoadedMacroSessionPersistenceService(loadedMacroSession, store, macroFileManager);

            await service.ReloadAsync(profileDirectory, CancellationToken.None);
            var item = loadedMacroSession.AddMacro(CreateMacro("Recorded"));
            await service.FlushAsync(CancellationToken.None);

            var expectedPath = Path.Combine(profileDirectory, "macros", $"{item.SessionId:N}.macro");
            await macroFileManager.Received(1).SaveAsync(item.Macro, expectedPath);
            _ = item.SourcePath.Should().Be(expectedPath);
            await store.Received(1).SaveAsync(
                profileDirectory,
                Arg.Is<LoadedMacroSessionSnapshot>(snapshot => snapshot.Items.Single().SourcePath == expectedPath),
                CancellationToken.None);
        }
        finally
        {
            if (Directory.Exists(profileDirectory))
            {
                Directory.Delete(profileDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task FlushAsync_DoesNotOverwriteAnExternallyLoadedMacroFile()
    {
        var profileDirectory = Path.Combine(Path.GetTempPath(), $"CrossMacroProfileMacroTests_{Guid.NewGuid():N}");
        var externalPath = Path.Combine(Path.GetTempPath(), $"CrossMacroExternalMacro_{Guid.NewGuid():N}.macro");
        try
        {
            await File.WriteAllTextAsync(externalPath, "macro");
            var loadedMacroSession = new LoadedMacroSession(Substitute.For<ILocalizationService>());
            var store = Substitute.For<IProfileLoadedMacroSessionStore>();
            var macroFileManager = Substitute.For<IMacroFileManager>();
            _ = store.LoadAsync(profileDirectory, CancellationToken.None).Returns(LoadedMacroSessionSnapshot.Empty);
            await using var service = new ProfileLoadedMacroSessionPersistenceService(loadedMacroSession, store, macroFileManager);

            await service.ReloadAsync(profileDirectory, CancellationToken.None);
            _ = loadedMacroSession.AddMacro(CreateMacro("External"), externalPath);
            await service.FlushAsync(CancellationToken.None);

            await macroFileManager.DidNotReceive().SaveAsync(Arg.Any<MacroSequence>(), externalPath);
        }
        finally
        {
            if (File.Exists(externalPath))
            {
                File.Delete(externalPath);
            }

            if (Directory.Exists(profileDirectory))
            {
                Directory.Delete(profileDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task FlushAsync_AfterSoftRemove_LeavesTheProfileMacroFileOnDisk()
    {
        var profileDirectory = Path.Combine(Path.GetTempPath(), $"CrossMacroProfileMacroTests_{Guid.NewGuid():N}");
        try
        {
            var sourcePath = Path.Combine(profileDirectory, "macros", "recorded.macro");
            _ = Directory.CreateDirectory(Path.GetDirectoryName(sourcePath)!);
            await File.WriteAllTextAsync(sourcePath, "macro");
            var loadedMacroSession = new LoadedMacroSession(Substitute.For<ILocalizationService>());
            var store = Substitute.For<IProfileLoadedMacroSessionStore>();
            var macroFileManager = Substitute.For<IMacroFileManager>();
            _ = store.LoadAsync(profileDirectory, CancellationToken.None).Returns(LoadedMacroSessionSnapshot.Empty);
            await using var service = new ProfileLoadedMacroSessionPersistenceService(loadedMacroSession, store, macroFileManager);

            await service.ReloadAsync(profileDirectory, CancellationToken.None);
            var item = loadedMacroSession.AddMacro(CreateMacro("Recorded"), sourcePath);
            _ = loadedMacroSession.RemoveMacro(item);
            await service.FlushAsync(CancellationToken.None);

            _ = File.Exists(sourcePath).Should().BeTrue();
            await store.Received(1).SaveAsync(
                profileDirectory,
                Arg.Is<LoadedMacroSessionSnapshot>(snapshot => snapshot.Items.Count == 0),
                CancellationToken.None);
        }
        finally
        {
            if (Directory.Exists(profileDirectory))
            {
                Directory.Delete(profileDirectory, recursive: true);
            }
        }
    }

    private static MacroSequence CreateMacro(string name)
    {
        return new MacroSequence
        {
            Name = name,
            Events = { new MacroEvent { Type = EventType.Click, X = 10, Y = 20 } },
        };
    }
}
