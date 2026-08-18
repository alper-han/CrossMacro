namespace CrossMacro.Infrastructure.Tests.Services;

public sealed class ProfileLoadedMacroSessionStoreTests : IDisposable
{
    private readonly string _profileDirectory;

    public ProfileLoadedMacroSessionStoreTests()
    {
        _profileDirectory = Path.Combine(Path.GetTempPath(), $"CrossMacroLoadedMacroSessionStoreTests_{Guid.NewGuid():N}");
    }

    public void Dispose()
    {
        if (Directory.Exists(_profileDirectory))
        {
            Directory.Delete(_profileDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task SaveAndLoadAsync_RoundTripsCompleteProfileSessionState()
    {
        var fileManager = Substitute.For<IMacroFileManager>();
        var firstSessionId = Guid.NewGuid();
        var secondSessionId = Guid.NewGuid();
        var firstPath = CreateMacroFile("first");
        var secondPath = CreateMacroFile("second");
        _ = fileManager.LoadAsync(firstPath).Returns(Task.FromResult<MacroSequence?>(CreateMacro("From first file", EventType.Click)));
        _ = fileManager.LoadAsync(secondPath).Returns(Task.FromResult<MacroSequence?>(CreateMacro("From second file", EventType.KeyPress)));
        using var store = new ProfileLoadedMacroSessionStore(fileManager);
        var snapshot = new LoadedMacroSessionSnapshot(
        [
            new LoadedMacroSessionItemSnapshot(firstSessionId, CreateMacro("First", EventType.Click), firstPath, 3),
            new LoadedMacroSessionItemSnapshot(secondSessionId, CreateMacro("Second", EventType.KeyPress), secondPath, 1),
        ],
        secondSessionId,
        PlaybackMode: 2);

        await store.SaveAsync(_profileDirectory, snapshot);
        var restored = await store.LoadAsync(_profileDirectory);

        _ = restored.PlaybackMode.Should().Be(2);
        _ = restored.SelectedSessionId.Should().Be(secondSessionId);
        _ = restored.Items.Should().HaveCount(2);
        _ = restored.Items[0].SessionId.Should().Be(firstSessionId);
        _ = restored.Items[0].SourcePath.Should().Be(firstPath);
        _ = restored.Items[0].SequenceRepeatCount.Should().Be(3);
        _ = restored.Items[0].Macro.Name.Should().Be("From first file");
        _ = restored.Items[1].Macro.Name.Should().Be("From second file");
        _ = File.Exists(Path.Combine(_profileDirectory, ConfigFileNames.LoadedMacros)).Should().BeTrue();
        var sessionJson = await File.ReadAllTextAsync(Path.Combine(_profileDirectory, ConfigFileNames.LoadedMacros));
        _ = sessionJson.Contains("\"macro\"", StringComparison.OrdinalIgnoreCase).Should().BeFalse();
    }

    [Fact]
    public async Task LoadAsync_WhenFileDoesNotExist_ReturnsEmptySnapshot()
    {
        using var store = new ProfileLoadedMacroSessionStore(Substitute.For<IMacroFileManager>());

        var snapshot = await store.LoadAsync(_profileDirectory);

        _ = snapshot.Should().BeSameAs(LoadedMacroSessionSnapshot.Empty);
    }

    [Fact]
    public async Task LoadAsync_WhenMacroFileIsMissing_SkipsTheSessionEntry()
    {
        using var store = new ProfileLoadedMacroSessionStore(Substitute.For<IMacroFileManager>());
        _ = Directory.CreateDirectory(_profileDirectory);
        var persisted = new PersistedLoadedMacroSession
        {
            Items =
            [
                new PersistedLoadedMacroSessionItem
                {
                    SessionId = Guid.NewGuid(),
                    SourcePath = Path.Combine(_profileDirectory, "missing.macro"),
                },
            ],
        };
        await File.WriteAllTextAsync(
            Path.Combine(_profileDirectory, ConfigFileNames.LoadedMacros),
            JsonSerializer.Serialize(persisted, CrossMacroJsonContext.Default.PersistedLoadedMacroSession));

        var snapshot = await store.LoadAsync(_profileDirectory);

        _ = snapshot.Should().BeEquivalentTo(LoadedMacroSessionSnapshot.Empty);
    }

    [Fact]
    public async Task SaveAsync_WhenMacroFileIsMissing_LeavesItOutOfTheSessionIndex()
    {
        using var store = new ProfileLoadedMacroSessionStore(Substitute.For<IMacroFileManager>());
        var snapshot = new LoadedMacroSessionSnapshot(
        [
            new LoadedMacroSessionItemSnapshot(
                Guid.NewGuid(),
                CreateMacro("Missing", EventType.Click),
                Path.Combine(_profileDirectory, "missing.macro"),
                1),
        ],
        null,
        PlaybackMode: 0);

        await store.SaveAsync(_profileDirectory, snapshot);
        var persisted = JsonSerializer.Deserialize(
            await File.ReadAllTextAsync(Path.Combine(_profileDirectory, ConfigFileNames.LoadedMacros)),
            CrossMacroJsonContext.Default.PersistedLoadedMacroSession);

        _ = persisted!.Items.Should().BeEmpty();
        _ = persisted.SelectedSessionId.Should().BeNull();
    }

    [Fact]
    public async Task LoadAsync_WhenSelectedMacroFileIsMissing_ClearsSelection()
    {
        var fileManager = Substitute.For<IMacroFileManager>();
        _ = fileManager.LoadAsync(Arg.Any<string>()).Returns(Task.FromResult<MacroSequence?>(CreateMacro("Available", EventType.Click)));
        using var store = new ProfileLoadedMacroSessionStore(fileManager);
        var availableSessionId = Guid.NewGuid();
        var missingSessionId = Guid.NewGuid();
        _ = Directory.CreateDirectory(_profileDirectory);
        var persisted = new PersistedLoadedMacroSession
        {
            Items =
            [
                new PersistedLoadedMacroSessionItem { SessionId = availableSessionId, SourcePath = CreateMacroFile("available") },
                new PersistedLoadedMacroSessionItem { SessionId = missingSessionId, SourcePath = Path.Combine(_profileDirectory, "missing.macro") },
            ],
            SelectedSessionId = missingSessionId,
        };
        await File.WriteAllTextAsync(
            Path.Combine(_profileDirectory, ConfigFileNames.LoadedMacros),
            JsonSerializer.Serialize(persisted, CrossMacroJsonContext.Default.PersistedLoadedMacroSession));

        var snapshot = await store.LoadAsync(_profileDirectory);

        _ = snapshot.Items.Should().ContainSingle().Which.SessionId.Should().Be(availableSessionId);
        _ = snapshot.SelectedSessionId.Should().BeNull();
    }

    [Fact]
    public async Task LoadAsync_WhenSelectedSessionIsMissing_ClearsSelection()
    {
        var fileManager = Substitute.For<IMacroFileManager>();
        _ = fileManager.LoadAsync(Arg.Any<string>()).Returns(Task.FromResult<MacroSequence?>(CreateMacro("Macro", EventType.Click)));
        using var store = new ProfileLoadedMacroSessionStore(fileManager);
        var sessionId = Guid.NewGuid();
        var macroPath = CreateMacroFile("macro");
        _ = Directory.CreateDirectory(_profileDirectory);
        var persisted = new PersistedLoadedMacroSession
        {
            Items =
            [
                new PersistedLoadedMacroSessionItem
                {
                    SessionId = sessionId,
                    SourcePath = macroPath,
                },
            ],
            SelectedSessionId = Guid.NewGuid(),
        };
        await File.WriteAllTextAsync(
            Path.Combine(_profileDirectory, ConfigFileNames.LoadedMacros),
            JsonSerializer.Serialize(persisted, CrossMacroJsonContext.Default.PersistedLoadedMacroSession));

        var snapshot = await store.LoadAsync(_profileDirectory);

        _ = snapshot.SelectedSessionId.Should().BeNull();
        _ = snapshot.Items.Should().ContainSingle().Which.SessionId.Should().Be(sessionId);
    }

    [Fact]
    public async Task LoadAsync_WhenSessionContainsDuplicateIds_RejectsCorruptedState()
    {
        using var store = new ProfileLoadedMacroSessionStore(Substitute.For<IMacroFileManager>());
        var sessionId = Guid.NewGuid();
        _ = Directory.CreateDirectory(_profileDirectory);
        var persisted = new PersistedLoadedMacroSession
        {
            Items =
            [
                new PersistedLoadedMacroSessionItem
                {
                    SessionId = sessionId,
                },
                new PersistedLoadedMacroSessionItem
                {
                    SessionId = sessionId,
                },
            ],
        };
        await File.WriteAllTextAsync(
            Path.Combine(_profileDirectory, ConfigFileNames.LoadedMacros),
            JsonSerializer.Serialize(persisted, CrossMacroJsonContext.Default.PersistedLoadedMacroSession));

        var load = () => store.LoadAsync(_profileDirectory);

        _ = await load.Should().ThrowAsync<InvalidDataException>()
            .WithMessage("*duplicate session id*");
    }

    [Fact]
    public async Task SaveAsync_WhenSessionContainsDuplicateIds_RejectsInvalidState()
    {
        using var store = new ProfileLoadedMacroSessionStore(Substitute.For<IMacroFileManager>());
        var sessionId = Guid.NewGuid();
        var snapshot = new LoadedMacroSessionSnapshot(
        [
            new LoadedMacroSessionItemSnapshot(sessionId, CreateMacro("First", EventType.Click), CreateMacroFile("first"), 1),
            new LoadedMacroSessionItemSnapshot(sessionId, CreateMacro("Second", EventType.KeyPress), CreateMacroFile("second"), 1),
        ],
        null,
        PlaybackMode: 0);

        var save = () => store.SaveAsync(_profileDirectory, snapshot);

        _ = await save.Should().ThrowAsync<InvalidDataException>()
            .WithMessage("*duplicate session id*");
    }

    private static MacroSequence CreateMacro(string name, EventType eventType)
    {
        return new MacroSequence
        {
            Name = name,
            Events = { new MacroEvent { Type = eventType, X = 10, Y = 20, DelayMs = 25 } },
        };
    }

    private string CreateMacroFile(string name)
    {
        _ = Directory.CreateDirectory(_profileDirectory);
        var filePath = Path.Combine(_profileDirectory, $"{name}.macro");
        File.WriteAllText(filePath, "macro");
        return filePath;
    }
}
