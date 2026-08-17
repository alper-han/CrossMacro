namespace CrossMacro.Infrastructure.Tests.Services;


public sealed class MacroFileManagerTests : IDisposable
{
    private const string TransparentPngBase64 = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+ip1sAAAAASUVORK5CYII=";
    private const string BlackPngBase64 = TransparentPngBase64;
    private static readonly CancellationToken NonCancelableToken = new(canceled: false);

    private readonly MacroFileManager _manager;
    private readonly List<string> _tempFiles = new();

    public MacroFileManagerTests()
    {
        _manager = CreateManager();
    }

    public void Dispose()
    {
        foreach (var file in _tempFiles)
        {
            try
            {
                if (File.Exists(file))
                {
                    File.Delete(file);
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // Best-effort test cleanup tolerates expected filesystem failures.
            }
        }
    }

    private string GetTempFilePath()
    {
        var path = Path.Combine(Path.GetTempPath(), $"test_macro_{Guid.NewGuid()}.macro");
        _tempFiles.Add(path);
        return path;
    }

    [Theory]
    [InlineData("current.macro", false)]
    [InlineData("legacy.macro", false)]
    [InlineData("malformed-metadata.macro", false)]
    public async Task GoldenFixture_LoadSaveLoad_PreservesSemanticFields(string fixtureName, bool expectedAbsolute)
    {
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Macros", fixtureName);
        var first = await _manager.LoadAsync(fixturePath);

        _ = first.Should().NotBeNull();
        _ = first!.IsAbsoluteCoordinates.Should().Be(expectedAbsolute);
        _ = first.Events.Should().NotBeEmpty();
        var savedPath = GetTempFilePath();
        await _manager.SaveAsync(first, savedPath);
        var second = await _manager.LoadAsync(savedPath);

        _ = second.Should().NotBeNull();
        var expectedEvents = first.Events.Select(static ev =>
        {
            if (ev.CoordinateMode is MouseCoordinateMode.Relative && ev.CoordinateSpace is null)
            {
                ev.CoordinateSpace = MouseCoordinateSpace.RawDevice;
            }

            return ev;
        });
        _ = second!.Events.Should().BeEquivalentTo(expectedEvents);
        _ = second.ScriptSteps.Should().Equal(first.ScriptSteps);
        _ = second.TextInputBoundaries.Should().Equal(first.TextInputBoundaries);
        _ = second.TrailingDelayMs.Should().Be(first.TrailingDelayMs);
        _ = second.HasTrailingRandomDelay.Should().Be(first.HasTrailingRandomDelay);
    }

    private static MacroFileManager CreateManager()
    {
        return new MacroFileManager(() => new KeyCodeMapper(new TestKeyboardLayoutService()));
    }

    [Fact]
    public async Task LoadAsync_DoesNotResolveKeyCodeMapperFactory()
    {
        // Arrange
        var manager = new MacroFileManager(() =>
            throw new InvalidOperationException("Key mapper should not be resolved while loading."));
        var filePath = GetTempFilePath();
        string content = "# Name: Load Without Key Mapper\n# Created: 2024-01-01T00:00:00Z\n# DurationMs: 0\n# IsAbsolute: True\n# Format: CrossMacroFormatV2\n[Events]\nM,0,0" + '\n';

        await File.WriteAllTextAsync(filePath, content, NonCancelableToken);

        // Act
        var loaded = await manager.LoadAsync(filePath);

        // Assert
        _ = loaded.Should().NotBeNull();
        _ = loaded!.Events.Should().ContainSingle();
    }

    private static MacroSequence CreateValidMacro(string name = "Test Macro")
    {
        return new MacroSequence
        {
            Name = name,
            IsAbsoluteCoordinates = true,
            Events = {
                new() { Type = EventType.MouseMove, X = 100, Y = 200, Timestamp = 0, DelayMs = 0 },
                new() { Type = EventType.ButtonPress, X = 100, Y = 200, Button = MacroMouseButton.Left, Timestamp = 100, DelayMs = 100 },
                new() { Type = EventType.ButtonRelease, X = 100, Y = 200, Button = MacroMouseButton.Left, Timestamp = 150, DelayMs = 50 },
            },
        };
    }

    [Fact]
    public void PersistedMacroDocument_RoundTrip_MapsEveryRuntimeField()
    {
        var macro = CreateValidMacro("Document boundary");
        var firstEvent = macro.Events[0];
        firstEvent.TimestampMicroseconds = 0;
        firstEvent.DelayMicroseconds = 0;
        macro.Events[0] = firstEvent;
        var secondEvent = macro.Events[1];
        secondEvent.TimestampMicroseconds = 100_400;
        secondEvent.DelayMicroseconds = 100_400;
        macro.Events[1] = secondEvent;
        macro.Id = Guid.NewGuid();
        macro.ReplaceScriptSteps(["click left"]);
        macro.ReplaceTextInputBoundaries([new TextInputBoundary(0, 1, "hello")]);
        macro.ReplaceImages(new Dictionary<string, string>(StringComparer.Ordinal) { ["Target"] = TransparentPngBase64 });
        macro.RecordedAt = new DateTime(2024, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        macro.ActualDuration = TimeSpan.FromMilliseconds(321);
        macro.MouseMoveCount = 4;
        macro.ClickCount = 5;
        macro.EventsPerSecond = 6.5;
        macro.IsAbsoluteCoordinates = true;
        macro.SkipInitialZeroZero = true;
        macro.TrailingDelayMs = 7;
        macro.HasTrailingRandomDelay = true;
        macro.TrailingDelayMinMs = 8;
        macro.TrailingDelayMaxMs = 9;

        var restored = PersistedMacroDocument.FromRuntime(macro).ToRuntime();

        _ = restored.Should().BeEquivalentTo(macro);
    }

    [Fact]
    public async Task SaveAndLoad_RoundTrip_UsesRealMacroFileForAllFormatMetadata()
    {
        var createdAt = new DateTime(2024, 1, 2, 3, 4, 5, DateTimeKind.Unspecified);
        var macro = new MacroSequence
        {
            Id = Guid.NewGuid(),
            Name = "Complete file metadata",
            CreatedAt = createdAt,
            TotalDurationMs = 45,
            IsAbsoluteCoordinates = false,
            SkipInitialZeroZero = true,
            TrailingDelayMs = 17,
            HasTrailingRandomDelay = true,
            TrailingDelayMinMs = 23,
            TrailingDelayMaxMs = 41,
            ScriptSteps = { "click left" },
            TextInputBoundaries = { new TextInputBoundary(0, 1, "hello") },
            Images = {
                ["Target"] = TransparentPngBase64,
            },
            Events =
            {
                new MacroEvent
                {
                    Type = EventType.MouseMove,
                    X = 10,
                    Y = 20,
                    Timestamp = 0,
                    DelayMs = 0,
                    CoordinateMode = MouseCoordinateMode.Relative,
                    CoordinateSpace = MouseCoordinateSpace.RawDevice,
                },
                new MacroEvent
                {
                    Type = EventType.Click,
                    X = 10,
                    Y = 20,
                    Button = MacroMouseButton.Left,
                    Timestamp = 45,
                    DelayMs = 40,
                    HasRandomDelay = true,
                    RandomDelayMinMs = 5,
                    RandomDelayMaxMs = 15,
                    UseCurrentPosition = true,
                },
            },
            RecordedAt = new DateTime(2024, 2, 3, 4, 5, 6, DateTimeKind.Utc),
            ActualDuration = TimeSpan.FromMilliseconds(321),
            MouseMoveCount = 9,
            ClickCount = 8,
            EventsPerSecond = 7.5,
        };
        var filePath = GetTempFilePath();

        await _manager.SaveAsync(macro, filePath);
        var saved = await File.ReadAllTextAsync(filePath, NonCancelableToken);
        var loaded = await _manager.LoadAsync(filePath);

        _ = saved.Should().Contain("# Name: Complete file metadata");
        _ = saved.Should().Contain("# Created: 2024-01-02T03:04:05.0000000");
        _ = saved.Should().Contain("# DurationMs: 45");
        _ = saved.Should().Contain("# IsAbsolute: False");
        _ = saved.Should().Contain("# SkipInitialZero: True");
        _ = saved.Should().Contain("# TrailingDelayUs: 17000");
        _ = saved.Should().Contain("# TrailingRandomDelayMs: 23,41");
        _ = saved.Should().Contain("# TextInputBoundaryBase64: ");
        _ = saved.Should().Contain($"# Image: Target = {TransparentPngBase64}");
        _ = saved.Should().Contain("# Format: CrossMacroFormatV4");

        _ = loaded.Should().NotBeNull();
        _ = loaded!.Name.Should().Be(macro.Name);
        _ = loaded.CreatedAt.Should().Be(createdAt);
        _ = loaded.TotalDurationMs.Should().Be(45);
        _ = loaded.IsAbsoluteCoordinates.Should().BeFalse();
        _ = loaded.SkipInitialZeroZero.Should().BeTrue();
        _ = loaded.TrailingDelayMs.Should().Be(17);
        _ = loaded.TrailingDelayMicroseconds.Should().Be(17_000);
        _ = loaded.HasTrailingRandomDelay.Should().BeTrue();
        _ = loaded.TrailingDelayMinMs.Should().Be(23);
        _ = loaded.TrailingDelayMaxMs.Should().Be(41);
        _ = loaded.ScriptSteps.Should().Equal(macro.ScriptSteps);
        _ = loaded.TextInputBoundaries.Should().Equal(macro.TextInputBoundaries);
        _ = loaded.Images.Should().Equal(macro.Images);
        _ = loaded.Events.Should().BeEquivalentTo(macro.Events);

        // The text grammar intentionally does not serialize runtime identity/statistics.
        _ = loaded.Id.Should().NotBe(macro.Id);
        _ = loaded.RecordedAt.Should().NotBe(macro.RecordedAt);
        _ = loaded.ActualDuration.Should().Be(TimeSpan.Zero);
        _ = loaded.MouseMoveCount.Should().Be(1);
        _ = loaded.ClickCount.Should().Be(1);
        _ = loaded.EventsPerSecond.Should().Be(0);
    }

    [Fact]
    public async Task SaveAsync_NullMacro_ThrowsArgumentNullException()
    {
        // Arrange
        var filePath = GetTempFilePath();

        // Act
        var act = async () => await _manager.SaveAsync(null!, filePath);

        // Assert
        _ = await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task SaveAsync_EmptyFilePath_ThrowsArgumentException()
    {
        // Arrange
        var macro = CreateValidMacro();

        // Act
        var act = async () => await _manager.SaveAsync(macro, "");

        // Assert
        _ = await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task SaveAsync_WhitespaceFilePath_ThrowsArgumentException()
    {
        // Arrange
        var macro = CreateValidMacro();

        // Act
        var act = async () => await _manager.SaveAsync(macro, "   ");

        // Assert
        _ = await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task SaveAsync_InvalidMacro_ThrowsInvalidOperationException()
    {
        // Arrange - Empty events = invalid
        var macro = new MacroSequence { Name = "Invalid" };
        var filePath = GetTempFilePath();

        // Act
        var act = async () => await _manager.SaveAsync(macro, filePath);

        // Assert
        _ = await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task SaveAsync_ValidMacro_CreatesFile()
    {
        // Arrange
        var macro = CreateValidMacro();
        var filePath = GetTempFilePath();

        // Act
        await _manager.SaveAsync(macro, filePath);

        // Assert
        _ = File.Exists(filePath).Should().BeTrue();
    }

    [Fact]
    public async Task SaveAsync_CreatesDirectoryIfNotExists()
    {
        // Arrange
        var macro = CreateValidMacro();
        var tempDir = Path.Combine(Path.GetTempPath(), $"test_dir_{Guid.NewGuid()}");
        var filePath = Path.Combine(tempDir, "macro.macro");
        _tempFiles.Add(filePath);

        try
        {
            // Act
            await _manager.SaveAsync(macro, filePath);

            // Assert
            _ = Directory.Exists(tempDir).Should().BeTrue();
            _ = File.Exists(filePath).Should().BeTrue();
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task LoadAsync_EmptyFilePath_ThrowsArgumentException()
    {
        // Act
        var act = async () => await _manager.LoadAsync("");

        // Assert
        _ = await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task LoadAsync_NonExistentFile_ThrowsFileNotFoundException()
    {
        // Act
        var act = async () => await _manager.LoadAsync("/nonexistent/path/macro.macro");

        // Assert
        _ = await act.Should().ThrowAsync<FileNotFoundException>();
    }

    [Fact]
    public async Task SaveAndLoad_RoundTrip_PreservesName()
    {
        // Arrange
        var macro = CreateValidMacro("Round Trip Test");
        var filePath = GetTempFilePath();

        // Act
        await _manager.SaveAsync(macro, filePath);
        var loaded = await _manager.LoadAsync(filePath);

        // Assert
        _ = loaded.Should().NotBeNull();
        _ = loaded!.Name.Should().Be("Round Trip Test");
    }

    [Fact]
    public async Task SaveAndLoad_RoundTrip_PreservesAbsoluteCoordinateModeInFile()
    {
        var macro = CreateValidMacro("Absolute Coordinate File Round Trip");
        macro.IsAbsoluteCoordinates = true;
        var filePath = GetTempFilePath();

        await _manager.SaveAsync(macro, filePath);
        var saved = await File.ReadAllTextAsync(filePath, NonCancelableToken);
        var loaded = await _manager.LoadAsync(filePath);

        _ = saved.Should().Contain("# IsAbsolute: True");
        _ = loaded.Should().NotBeNull();
        _ = loaded!.IsAbsoluteCoordinates.Should().BeTrue();
    }

    [Fact]
    public async Task SaveAndLoad_RoundTrip_PreservesEventOnlyMacro()
    {
        // Arrange
        var macro = CreateValidMacro();
        var filePath = GetTempFilePath();

        // Act
        await _manager.SaveAsync(macro, filePath);
        var saved = await File.ReadAllTextAsync(filePath, NonCancelableToken);
        var loaded = await _manager.LoadAsync(filePath);

        // Assert
        _ = saved.Should().Contain("# Format: CrossMacroFormatV4");
        _ = saved.Should().Contain("[Events]");
        _ = loaded.Should().NotBeNull();
        _ = loaded!.Events.Should().HaveCount(macro.Events.Count);
    }

    [Fact]
    public async Task SaveAndLoad_RoundTrip_PreservesMouseMoveEvents()
    {
        // Arrange
        var macro = new MacroSequence
        {
            Name = "Move Test",
            Events = {
                new() { Type = EventType.MouseMove, X = 500, Y = 600, Timestamp = 0, DelayMs = 0 },
            },
        };
        var filePath = GetTempFilePath();

        // Act
        await _manager.SaveAsync(macro, filePath);
        var loaded = await _manager.LoadAsync(filePath);

        // Assert
        _ = loaded!.Events.Should().HaveCount(1);
        _ = loaded.Events[0].Type.Should().Be(EventType.MouseMove);
        _ = loaded.Events[0].X.Should().Be(500);
        _ = loaded.Events[0].Y.Should().Be(600);
    }

    [Fact]
    public async Task SaveAndLoad_RoundTrip_PreservesKeyboardEvents()
    {
        // Arrange
        var macro = new MacroSequence
        {
            Name = "Keyboard Test",
            Events = {
                new() { Type = EventType.KeyPress, KeyCode = 30, Timestamp = 0, DelayMs = 0 },
                new() { Type = EventType.KeyRelease, KeyCode = 30, Timestamp = 50, DelayMs = 50 },
            },
        };
        var filePath = GetTempFilePath();

        // Act
        await _manager.SaveAsync(macro, filePath);
        var loaded = await _manager.LoadAsync(filePath);

        // Assert
        _ = loaded!.Events.Should().HaveCount(2);
        _ = loaded.Events[0].Type.Should().Be(EventType.KeyPress);
        _ = loaded.Events[0].KeyCode.Should().Be(30);
        _ = loaded.Events[1].Type.Should().Be(EventType.KeyRelease);
        _ = loaded.Events[1].KeyCode.Should().Be(30);
    }

    [Fact]
    public async Task SaveAndLoad_RoundTrip_PreservesDelays()
    {
        // Arrange
        var macro = new MacroSequence
        {
            Name = "Delay Test",
            Events = {
                new() { Type = EventType.MouseMove, X = 0, Y = 0, Timestamp = 0, DelayMs = 0 },
                new() { Type = EventType.MouseMove, X = 100, Y = 100, Timestamp = 500, DelayMs = 500 },
                new() { Type = EventType.MouseMove, X = 200, Y = 200, Timestamp = 1500, DelayMs = 1000 },
            },
        };
        var filePath = GetTempFilePath();

        // Act
        await _manager.SaveAsync(macro, filePath);
        var loaded = await _manager.LoadAsync(filePath);

        // Assert
        _ = loaded!.Events[1].DelayMs.Should().Be(500);
        _ = loaded.Events[2].DelayMs.Should().Be(1000);
    }

    [Fact]
    public async Task SaveAndLoad_RoundTrip_PreservesSubMillisecondDelays()
    {
        var macro = new MacroSequence
        {
            Name = "Microsecond delay round trip",
            Events =
            {
                new()
                {
                    Type = EventType.MouseMove,
                    X = 10,
                    Y = 10,
                    Timestamp = 0,
                    DelayMs = 0,
                },
                new()
                {
                    Type = EventType.MouseMove,
                    X = 20,
                    Y = 20,
                    Timestamp = 0,
                    TimestampMicroseconds = 400,
                    DelayMs = 0,
                    DelayMicroseconds = 400,
                },
                new()
                {
                    Type = EventType.MouseMove,
                    X = 30,
                    Y = 30,
                    Timestamp = 1,
                    TimestampMicroseconds = 1_000,
                    DelayMs = 0,
                    DelayMicroseconds = 600,
                },
            },
        };
        var filePath = GetTempFilePath();

        await _manager.SaveAsync(macro, filePath);
        var saved = await File.ReadAllTextAsync(filePath, NonCancelableToken);
        var loaded = await _manager.LoadAsync(filePath);

        _ = saved.Should().Contain("WU,400");
        _ = saved.Should().Contain("WU,600");
        _ = loaded.Should().NotBeNull();
        _ = loaded!.Events.Should().BeEquivalentTo(macro.Events);
    }

    [Fact]
    public async Task SaveAndLoad_RoundTrip_PreservesButtonEvents()
    {
        // Arrange
        var macro = new MacroSequence
        {
            Name = "Button Test",
            Events = {
                new() { Type = EventType.ButtonPress, X = 100, Y = 200, Button = MacroMouseButton.Right, Timestamp = 0, DelayMs = 0 },
                new() { Type = EventType.ButtonRelease, X = 100, Y = 200, Button = MacroMouseButton.Right, Timestamp = 100, DelayMs = 100 },
            },
        };
        var filePath = GetTempFilePath();

        // Act
        await _manager.SaveAsync(macro, filePath);
        var loaded = await _manager.LoadAsync(filePath);

        // Assert
        _ = loaded!.Events[0].Button.Should().Be(MacroMouseButton.Right);
        _ = loaded.Events[1].Button.Should().Be(MacroMouseButton.Right);
    }

    [Fact]
    public async Task SaveAndLoad_RoundTrip_PreservesCurrentPositionFlag()
    {
        // Arrange
        var macro = new MacroSequence
        {
            Name = "Current Position Test",
            IsAbsoluteCoordinates = false,
            SkipInitialZeroZero = true,
            Events = {
                new()
                {
                    Type = EventType.Click,
                    X = 0,
                    Y = 0,
                    Button = MacroMouseButton.Left,
                    UseCurrentPosition = true,
                },
            },
        };
        var filePath = GetTempFilePath();

        // Act
        await _manager.SaveAsync(macro, filePath);
        var loaded = await _manager.LoadAsync(filePath);

        // Assert
        _ = loaded.Should().NotBeNull();
        _ = loaded!.Events.Should().ContainSingle();
        _ = loaded.Events[0].UseCurrentPosition.Should().BeTrue();
    }

    [Fact]
    public async Task Load_ParsesWaitCommands()
    {
        // Arrange - Manual file with WAIT command
        var filePath = GetTempFilePath();
        string content = "# Name: Wait Test\n# Created: 2024-01-01T00:00:00Z\n# DurationMs: 1000\n# IsAbsolute: True\n# Format: CrossMacroFormatV2\n[Events]\nM,0,0\nW,500\nM,100,100" + '\n';

        await File.WriteAllTextAsync(filePath, content, NonCancelableToken);

        // Act
        var loaded = await _manager.LoadAsync(filePath);

        // Assert
        _ = loaded!.Events.Should().HaveCount(2);
        _ = loaded.Events[1].DelayMs.Should().Be(500);
    }

    [Fact]
    public async Task Load_WhenEventLinesHaveNoSections_StillParsesLegacyEventLines()
    {
        // Arrange
        var filePath = GetTempFilePath();
        string content = "# Name: Sectionless Events\n# Created: 2024-01-01T00:00:00Z\n# DurationMs: 1000\n# IsAbsolute: True\nM,0,0\nW,500\nKP,65" + '\n';

        await File.WriteAllTextAsync(filePath, content, NonCancelableToken);

        // Act
        var loaded = await _manager.LoadAsync(filePath);

        // Assert
        _ = loaded.Should().NotBeNull();
        _ = loaded!.ScriptSteps.Should().BeEmpty();
        _ = loaded.Events.Should().HaveCount(2);
        _ = loaded.Events[0].Type.Should().Be(EventType.MouseMove);
        _ = loaded.Events[1].Type.Should().Be(EventType.KeyPress);
        _ = loaded.Events[1].DelayMs.Should().Be(500);
    }

    [Fact]
    public async Task Load_WhenMalformedEventAppears_DoesNotLeakDelayToNextValidEvent()
    {
        // Arrange
        var filePath = GetTempFilePath();
        string content = "# Name: Delay Leak Test\n# Created: 2024-01-01T00:00:00Z\n# DurationMs: 1000\n# IsAbsolute: True\n# Format: CrossMacroFormatV2\n[Events]\nM,0,0\nW,500\nP,invalid,10,Left\nM,100,100" + '\n';

        await File.WriteAllTextAsync(filePath, content, NonCancelableToken);

        // Act
        var loaded = await _manager.LoadAsync(filePath);

        // Assert
        _ = loaded.Should().NotBeNull();
        _ = loaded!.Events.Should().HaveCount(2);
        _ = loaded.Events[0].DelayMs.Should().Be(0);
        _ = loaded.Events[1].DelayMs.Should().Be(0);
    }

    [Fact]
    public async Task Load_WhenLegacyCurrentPositionMacro_IsUpgradedToExplicitFlag()
    {
        // Arrange
        var filePath = GetTempFilePath();
        string content = "# Name: Legacy Current Position Test\n# Created: 2024-01-01T00:00:00Z\n# DurationMs: 0\n# IsAbsolute: False\n# SkipInitialZero: True\n# Format: CrossMacroFormatV2\n[Events]\nC,0,0,Left" + '\n';

        await File.WriteAllTextAsync(filePath, content, NonCancelableToken);

        // Act
        var loaded = await _manager.LoadAsync(filePath);

        // Assert
        _ = loaded.Should().NotBeNull();
        _ = loaded!.Events.Should().ContainSingle();
        _ = loaded.Events[0].UseCurrentPosition.Should().BeTrue();
    }

    [Fact]
    public async Task Load_WhenLegacyCurrentPositionMacroHasLaterRelativeMove_UpgradesLeadingClickOnly()
    {
        // Arrange
        var filePath = GetTempFilePath();
        string content = "# Name: Legacy Current Position Followed By Move\n# Created: 2024-01-01T00:00:00Z\n# DurationMs: 0\n# IsAbsolute: False\n# SkipInitialZero: True\n# Format: CrossMacroFormatV2\n[Events]\nC,0,0,Left\nM,15,5\nC,0,0,Left" + '\n';

        await File.WriteAllTextAsync(filePath, content, NonCancelableToken);

        // Act
        var loaded = await _manager.LoadAsync(filePath);

        // Assert
        _ = loaded.Should().NotBeNull();
        _ = loaded!.Events.Should().HaveCount(3);
        _ = loaded.Events[0].UseCurrentPosition.Should().BeTrue();
        _ = loaded.Events[1].Type.Should().Be(EventType.MouseMove);
        _ = loaded.Events[2].UseCurrentPosition.Should().BeFalse();
    }

    [Fact]
    public async Task Load_WhenExplicitRelativeZeroButtonEvent_DoesNotUpgradeToCurrentPosition()
    {
        // Arrange
        var filePath = GetTempFilePath();
        string content = "# Name: Explicit Relative Zero Click\n# Created: 2024-01-01T00:00:00Z\n# DurationMs: 0\n# IsAbsolute: False\n# SkipInitialZero: True\n# Format: CrossMacroFormatV2\n[Events]\nC,rel,0,0,Left" + '\n';

        await File.WriteAllTextAsync(filePath, content, NonCancelableToken);

        // Act
        var loaded = await _manager.LoadAsync(filePath);

        // Assert
        _ = loaded.Should().NotBeNull();
        _ = loaded!.Events.Should().ContainSingle();
        _ = loaded.Events[0].UseCurrentPosition.Should().BeFalse();
        _ = loaded.Events[0].CoordinateMode.Should().Be(MouseCoordinateMode.Relative);
    }

    [Fact]
    public async Task SaveAndLoad_RoundTrip_PreservesRandomDelayMetadata()
    {
        // Arrange
        var macro = new MacroSequence
        {
            Name = "Random Delay Test",
            Events = {
                new() { Type = EventType.MouseMove, X = 0, Y = 0, Timestamp = 0, DelayMs = 0 },
                new()
                {
                    Type = EventType.MouseMove,
                    X = 10,
                    Y = 10,
                    Timestamp = 100,
                    DelayMs = 40,
                    HasRandomDelay = true,
                    RandomDelayMinMs = 60,
                    RandomDelayMaxMs = 120,
                },
            },
        };
        var filePath = GetTempFilePath();

        // Act
        await _manager.SaveAsync(macro, filePath);
        var loaded = await _manager.LoadAsync(filePath);

        // Assert
        _ = loaded.Should().NotBeNull();
        _ = loaded!.Events.Should().HaveCount(2);
        _ = loaded.Events[1].DelayMs.Should().Be(40);
        _ = loaded.Events[1].HasRandomDelay.Should().BeTrue();
        _ = loaded.Events[1].RandomDelayMinMs.Should().Be(60);
        _ = loaded.Events[1].RandomDelayMaxMs.Should().Be(120);
    }

    [Fact]
    public async Task SaveAndLoad_RoundTrip_PreservesTrailingRandomDelayMetadata()
    {
        // Arrange
        var macro = new MacroSequence
        {
            Name = "Trailing Random Delay Test",
            HasTrailingRandomDelay = true,
            TrailingDelayMinMs = 25,
            TrailingDelayMaxMs = 75,
            Events = {
                new() { Type = EventType.MouseMove, X = 0, Y = 0, Timestamp = 0, DelayMs = 0 },
            },
        };
        var filePath = GetTempFilePath();

        // Act
        await _manager.SaveAsync(macro, filePath);
        var loaded = await _manager.LoadAsync(filePath);

        // Assert
        _ = loaded.Should().NotBeNull();
        _ = loaded!.HasTrailingRandomDelay.Should().BeTrue();
        _ = loaded.TrailingDelayMinMs.Should().Be(25);
        _ = loaded.TrailingDelayMaxMs.Should().Be(75);
    }

    [Fact]
    public async Task Load_ParsesRandomWaitCommands()
    {
        // Arrange
        var filePath = GetTempFilePath();
        string content = "# Name: Wait Random Test\n# Created: 2024-01-01T00:00:00Z\n# DurationMs: 1000\n# IsAbsolute: True\n# Format: CrossMacroFormatV2\n[Events]\nM,0,0\nWR,100,250\nM,100,100" + '\n';

        await File.WriteAllTextAsync(filePath, content, NonCancelableToken);

        // Act
        var loaded = await _manager.LoadAsync(filePath);

        // Assert
        _ = loaded.Should().NotBeNull();
        _ = loaded!.Events.Should().HaveCount(2);
        _ = loaded.Events[1].DelayMs.Should().Be(0);
        _ = loaded.Events[1].HasRandomDelay.Should().BeTrue();
        _ = loaded.Events[1].RandomDelayMinMs.Should().Be(100);
        _ = loaded.Events[1].RandomDelayMaxMs.Should().Be(250);
    }

    [Fact]
    public async Task SaveAndLoad_RoundTrip_PreservesScriptSteps()
    {
        // Arrange
        var macro = new MacroSequence
        {
            Name = "Script Step Round Trip",
            ScriptSteps =
            {
                "set i 0",
                "for i from 1 to 10 {",
                "click left",
                "}",
            },
            Events = {
                new() { Type = EventType.Click, Button = MacroMouseButton.Left, DelayMs = 0 },
            },
        };
        var filePath = GetTempFilePath();

        // Act
        await _manager.SaveAsync(macro, filePath);
        var saved = await File.ReadAllTextAsync(filePath, NonCancelableToken);
        var loaded = await _manager.LoadAsync(filePath);

        // Assert
        _ = saved.Should().Contain("# Format: CrossMacroFormatV4");
        _ = saved.Should().Contain("[Script]");
        _ = saved.Should().Contain("set i 0");
        _ = saved.Should().Contain("for i from 1 to 10 {");
        _ = saved.Should().Contain("click left");
        _ = saved.Should().Contain("}");
        _ = saved.Should().Contain("[Events]");
        _ = loaded.Should().NotBeNull();
        _ = loaded!.Events.Should().HaveCount(1);
        _ = loaded.Events[0].Type.Should().Be(EventType.Click);
        _ = loaded!.ScriptSteps.Should().Equal(macro.ScriptSteps);
    }

    [Fact]
    public async Task SaveAsync_WhenScriptReferencesMissingImage_RejectsTheMacro()
    {
        var macro = CreateValidMacro("Dangling Image Reference");
        macro.ReplaceScriptSteps(["imagesearch Missing found found_x found_y"]);
        var filePath = GetTempFilePath();

        var act = async () => await _manager.SaveAsync(macro, filePath);

        _ = await act.Should().ThrowAsync<InvalidDataException>()
            .WithMessage("*image asset 'Missing' is not defined*");
        _ = File.Exists(filePath).Should().BeFalse();
    }

    [Fact]
    public async Task LoadAsync_WhenScriptReferencesMissingImage_RejectsTheMacro()
    {
        var filePath = GetTempFilePath();
        await File.WriteAllLinesAsync(filePath,
        [
            "# Name: Dangling Image Reference",
            "# Format: CrossMacroFormatV2",
            "[Script]",
            "imagesearch Missing found found_x found_y",
            "[Events]",
            "M,0,0",
        ], NonCancelableToken);

        var act = async () => await _manager.LoadAsync(filePath);

        _ = await act.Should().ThrowAsync<InvalidDataException>()
            .WithMessage("*image asset 'Missing' is not defined*");
    }

    [Fact]
    public async Task SaveAndLoad_RoundTrip_PreservesImageAssets()
    {
        // Arrange
        var macro = new MacroSequence
        {
            Name = "Image Asset Round Trip",
            Images = {
                ["Target_1"] = TransparentPngBase64,
            },
            ScriptSteps = { "click left" },
        };
        var filePath = GetTempFilePath();

        // Act
        await _manager.SaveAsync(macro, filePath);
        var saved = await File.ReadAllTextAsync(filePath, NonCancelableToken);
        var loaded = await _manager.LoadAsync(filePath);

        // Assert
        _ = saved.Should().Contain($"# Image: Target_1 = {TransparentPngBase64}");
        _ = saved.IndexOf("# Image: Target_1", StringComparison.Ordinal)
            .Should().BeLessThan(saved.IndexOf("# Format: CrossMacroFormatV4", StringComparison.Ordinal));
        _ = loaded.Should().NotBeNull();
        _ = loaded!.Images.Should().Equal(macro.Images);
        _ = loaded.ScriptSteps.Should().Equal(macro.ScriptSteps);
    }

    [Fact]
    public async Task SaveAndLoad_RoundTrip_PreservesMultipleImagesDeterministically()
    {
        // Arrange
        var macro = new MacroSequence
        {
            Name = "Multiple Image Asset Round Trip",
            Images = {
                ["Zeta"] = TransparentPngBase64,
                ["Alpha_2"] = BlackPngBase64,
            },
            Events = {
                new() { Type = EventType.MouseMove, X = 0, Y = 0 },
            },
        };
        var filePath = GetTempFilePath();

        // Act
        await _manager.SaveAsync(macro, filePath);
        var savedLines = await File.ReadAllLinesAsync(filePath, NonCancelableToken);
        var loaded = await _manager.LoadAsync(filePath);

        // Assert
        _ = savedLines.Should().ContainInOrder(
            $"# Image: Alpha_2 = {BlackPngBase64}",
            $"# Image: Zeta = {TransparentPngBase64}",
            "# Format: CrossMacroFormatV4");
        _ = loaded.Should().NotBeNull();
        _ = loaded!.Images.Should().Equal(macro.Images);
    }

    [Fact]
    public async Task SaveAndLoad_RoundTrip_WithNoImages_DoesNotWriteImageHeaders()
    {
        // Arrange
        var macro = CreateValidMacro("No Images Round Trip");
        var filePath = GetTempFilePath();

        // Act
        await _manager.SaveAsync(macro, filePath);
        var saved = await File.ReadAllTextAsync(filePath, NonCancelableToken);
        var loaded = await _manager.LoadAsync(filePath);

        // Assert
        _ = saved.Should().NotContain("# Image:");
        _ = loaded.Should().NotBeNull();
        _ = loaded!.Images.Should().BeEmpty();
        _ = loaded.Events.Should().HaveCount(macro.Events.Count);
    }

    [Fact]
    public async Task SaveAsync_WhenImageBase64IsInvalid_ThrowsAndDoesNotCreateFile()
    {
        // Arrange
        var macro = new MacroSequence
        {
            Name = "Invalid Image Base64 Save",
            Images = {
                ["ValidTarget"] = TransparentPngBase64,
                ["InvalidTarget"] = "not-base64",
                ["WrappedTarget"] = $"{BlackPngBase64[..12]}\n{BlackPngBase64[12..]}",
            },
            Events = {
                new() { Type = EventType.MouseMove, X = 0, Y = 0 },
            },
        };
        var filePath = GetTempFilePath();

        // Act
        var act = async () => await _manager.SaveAsync(macro, filePath);

        // Assert
        _ = await act.Should().ThrowAsync<InvalidDataException>();
        _ = File.Exists(filePath).Should().BeFalse();
    }

    [Fact]
    public async Task LoadAsync_WhenImageMetadataIsMalformed_ThrowsBeforeLoadingEvents()
    {
        // Arrange
        var filePath = GetTempFilePath();
        await File.WriteAllLinesAsync(filePath,
        [
            "# Name: Malformed Image Metadata",
            "# Image: MissingSeparator",
            "# Image: Invalid-Name = iVBORw0KGgo=",
            "# Image: ValidName = not-base64",
            "# Format: CrossMacroFormatV4",
            "[Events]",
            "M,0,0",
        ], NonCancelableToken);

        // Act
        var act = async () => await _manager.LoadAsync(filePath);

        // Assert
        _ = await act.Should().ThrowAsync<InvalidDataException>()
            .WithMessage("Malformed image metadata: missing '=' separator.");
    }

    [Fact]
    public async Task LoadAsync_WhenImageMetadataIsOversized_ThrowsBeforeLoadingEvents()
    {
        // Arrange
        var filePath = GetTempFilePath();
        await File.WriteAllLinesAsync(filePath,
        [
            "# Name: Oversized Image Metadata",
            $"# Image: Oversized = {Convert.ToBase64String(CreateOversizedPngBytes())}",
            "# Format: CrossMacroFormatV2",
            "[Events]",
            "M,0,0",
        ], NonCancelableToken);

        // Act
        var act = async () => await _manager.LoadAsync(filePath);

        // Assert
        _ = await act.Should().ThrowAsync<InvalidDataException>();
    }

    [Fact]
    public async Task SaveAsync_WhenImagePreflightFails_PreservesExistingDestination()
    {
        var filePath = GetTempFilePath();
        const string original = "existing macro content";
        await File.WriteAllTextAsync(filePath, original, NonCancelableToken);
        var macro = CreateValidMacro();
        macro.ReplaceImages(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["InvalidTarget"] = "not-base64",
        });

        var act = async () => await _manager.SaveAsync(macro, filePath);

        _ = await act.Should().ThrowAsync<InvalidDataException>();
        _ = (await File.ReadAllTextAsync(filePath, NonCancelableToken)).Should().Be(original);
        _ = Directory.GetFiles(Path.GetDirectoryName(filePath)!, $"{Path.GetFileName(filePath)}.*.tmp")
            .Should().BeEmpty();
    }

    [Fact]
    public async Task SaveAsync_WhenDestinationExists_ReplacesItAtomically()
    {
        var filePath = GetTempFilePath();
        await File.WriteAllTextAsync(filePath, "old content", NonCancelableToken);

        await _manager.SaveAsync(CreateValidMacro("replacement"), filePath);

        _ = (await File.ReadAllTextAsync(filePath, NonCancelableToken)).Should().Contain("# Name: replacement");
        _ = Directory.GetFiles(Path.GetDirectoryName(filePath)!, $"{Path.GetFileName(filePath)}.*.tmp")
            .Should().BeEmpty();
    }

    [Fact]
    public async Task SaveAsync_WhenImagesPresentAndScriptInvalid_ThrowsAndDoesNotCreateFile()
    {
        // Arrange
        var macro = new MacroSequence
        {
            Name = "Invalid Script With Images",
            Images = {
                ["Target_1"] = TransparentPngBase64,
            },
            ScriptSteps = { "pixelcolor 1" },
        };
        var filePath = GetTempFilePath();

        // Act
        var act = async () => await _manager.SaveAsync(macro, filePath);

        // Assert
        _ = await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Invalid pixelcolor syntax*");
        _ = File.Exists(filePath).Should().BeFalse();
    }

    [Fact]
    public async Task Load_WhenReadableScriptSectionPresent_RestoresScriptSteps()
    {
        // Arrange
        var filePath = GetTempFilePath();
        string content = "# Name: Readable Script Step Macro\n# Created: 2024-01-01T00:00:00Z\n# DurationMs: 0\n# IsAbsolute: True\n# Format: CrossMacroFormatV2\n[Script]\npixelcolor 10 20 color\nwaitcolor 11 22 00FFAA 2500 wait_ok\npixelsearch 0 0 3 3 123456 found x y\n[Events]" + '\n';

        await File.WriteAllTextAsync(filePath, content, NonCancelableToken);

        // Act
        var loaded = await _manager.LoadAsync(filePath);

        // Assert
        _ = loaded.Should().NotBeNull();
        _ = loaded!.Events.Should().BeEmpty();
        _ = loaded.ScriptSteps.Should().Equal(
            "pixelcolor 10 20 color",
            "waitcolor 11 22 00FFAA 2500 wait_ok",
            "pixelsearch 0 0 3 3 123456 found x y");
    }

    [Fact]
    public async Task Load_IgnoresBlankAndCommentLines_InReadableSections()
    {
        // Arrange
        var filePath = GetTempFilePath();
        string content = "# Name: Readable Section Noise\n# Format: CrossMacroFormatV2\n[Script]\n\n# ignore me\npixelcolor 10 20 color\n\n# another comment\nclick left\n[Events]\n\n# comment in events\nM,10,20\n\nKP,65" + '\n';

        await File.WriteAllTextAsync(filePath, content, NonCancelableToken);

        // Act
        var loaded = await _manager.LoadAsync(filePath);

        // Assert
        _ = loaded.Should().NotBeNull();
        _ = loaded!.ScriptSteps.Should().Equal("pixelcolor 10 20 color", "click left");
        _ = loaded.Events.Should().HaveCount(2);
    }

    [Fact]
    public async Task SaveAndLoad_WhenScriptStepContainsEmbeddedNewline_PreservesContinuationPrefix()
    {
        // Arrange
        var macro = new MacroSequence
        {
            Name = "Embedded Newline Script Step Round Trip",
            ScriptSteps = { "type first line\npath C:\\Users\\me" },
        };
        var filePath = GetTempFilePath();

        // Act
        await _manager.SaveAsync(macro, filePath);
        var savedLines = await File.ReadAllLinesAsync(filePath, NonCancelableToken);
        var loaded = await _manager.LoadAsync(filePath);

        // Assert
        _ = savedLines.Should().ContainInOrder(
            "# Format: CrossMacroFormatV4",
            "[Script]",
            "type first line",
            "| path C:\\Users\\me");
        _ = loaded.Should().NotBeNull();
        _ = loaded!.ScriptSteps.Should().Equal(macro.ScriptSteps);
    }

    [Fact]
    public async Task SaveAndLoad_RoundTrip_PreservesScriptOnlyMacro()
    {
        // Arrange
        var macro = new MacroSequence
        {
            Name = "Script Only Round Trip",
            ScriptSteps =
            {
                "pixelcolor 10 20 color",
                "pixelsearch 0 0 3 3 123456 x y",
            },
        };
        var filePath = GetTempFilePath();

        // Act
        await _manager.SaveAsync(macro, filePath);
        var saved = await File.ReadAllTextAsync(filePath, NonCancelableToken);
        var loaded = await _manager.LoadAsync(filePath);

        // Assert
        _ = saved.Should().Contain("# Format: CrossMacroFormatV4");
        _ = saved.Should().Contain("[Script]");
        _ = saved.Should().Contain("pixelcolor 10 20 color");
        _ = saved.Should().Contain("pixelsearch 0 0 3 3 123456 x y");
        _ = loaded.Should().NotBeNull();
        _ = loaded!.Events.Should().BeEmpty();
        _ = loaded.ScriptSteps.Should().Equal(macro.ScriptSteps);
    }

    [Fact]
    public async Task SaveAndLoad_RoundTrip_PreservesScriptSpecialCharacters()
    {
        // Arrange
        var macro = new MacroSequence
        {
            Name = "Script Special Characters Round Trip",
            ScriptSteps =
            {
                "type [demo], #1, C:\\Temp\\macro.txt",
            },
        };
        var filePath = GetTempFilePath();

        // Act
        await _manager.SaveAsync(macro, filePath);
        var saved = await File.ReadAllTextAsync(filePath, NonCancelableToken);
        var loaded = await _manager.LoadAsync(filePath);

        // Assert
        _ = saved.Should().Contain("type [demo], #1, C:\\Temp\\macro.txt");
        _ = saved.Should().Contain("# Format: CrossMacroFormatV4");
        _ = loaded.Should().NotBeNull();
        _ = loaded!.Events.Should().BeEmpty();
        _ = loaded.ScriptSteps.Should().Equal(macro.ScriptSteps);
    }

    [Fact]
    public async Task SaveAsync_WhenScriptOnlyMacroHasInvalidScript_ThrowsAndDoesNotCreateFile()
    {
        // Arrange
        var macro = new MacroSequence
        {
            Name = "Invalid Script Step Macro",
            ScriptSteps = { "pixelcolor 1" },
        };
        var filePath = GetTempFilePath();

        // Act
        var act = async () => await _manager.SaveAsync(macro, filePath);

        // Assert
        _ = await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Invalid pixelcolor syntax*");
        _ = File.Exists(filePath).Should().BeFalse();
    }

    [Theory]
    [InlineData("tap Backspace")]
    [InlineData("tap F13")]
    [InlineData("tap NumpadPlus")]
    public async Task SaveAndLoad_WhenScriptUsesRuntimeMappedKey_PreservesScriptStep(string scriptStep)
    {
        // Arrange
        var macro = new MacroSequence
        {
            Name = "Runtime Mapped Key Script",
            ScriptSteps = { scriptStep },
        };
        var filePath = GetTempFilePath();

        // Act
        await _manager.SaveAsync(macro, filePath);
        var loaded = await _manager.LoadAsync(filePath);

        // Assert
        _ = loaded.Should().NotBeNull();
        _ = loaded!.ScriptSteps.Should().Equal(scriptStep);
    }

    [Fact]
    public async Task SaveAndLoad_RoundTrip_PreservesTextInputBoundaries()
    {
        // Arrange
        var macro = new MacroSequence
        {
            Name = "Text Boundary Round Trip",
            Events = {
                new() { Type = EventType.KeyPress, KeyCode = 65 },
                new() { Type = EventType.KeyRelease, KeyCode = 65 },
                new() { Type = EventType.KeyPress, KeyCode = 66 },
                new() { Type = EventType.KeyRelease, KeyCode = 66 },
            },
            TextInputBoundaries =
            {
                new TextInputBoundary(0, 2, "a,b $1"),
                new TextInputBoundary(2, 2, "çok satırlı\nmetin"),
            },
        };
        var filePath = GetTempFilePath();

        // Act
        await _manager.SaveAsync(macro, filePath);
        var saved = await File.ReadAllTextAsync(filePath, NonCancelableToken);
        var loaded = await _manager.LoadAsync(filePath);

        // Assert
        _ = saved.Should().Contain("# Format: CrossMacroFormatV4");
        _ = saved.Should().Contain("# TextInputBoundaryBase64:");
        _ = saved.Should().Contain("[Events]");
        _ = loaded.Should().NotBeNull();
        _ = loaded!.TextInputBoundaries.Should().Equal(macro.TextInputBoundaries);
    }

    [Fact]
    public async Task Load_WhenLegacyRelativeEventsHaveNoModeTokens_UsesHeaderFallback()
    {
        // Arrange
        var filePath = GetTempFilePath();
        const string content = "# Name: Legacy Relative\n# IsAbsolute: False\n# SkipInitialZero: False\n# Format: CrossMacroFormatV2\n[Events]\nM,5,6\nC,5,6,Right";

        await File.WriteAllTextAsync(filePath, content, NonCancelableToken);

        // Act
        var loaded = await _manager.LoadAsync(filePath);

        // Assert
        _ = loaded.Should().NotBeNull();
        _ = loaded!.IsAbsoluteCoordinates.Should().BeFalse();
        _ = loaded.Events.Should().HaveCount(2);
        _ = loaded.Events[0].CoordinateMode.Should().BeNull();
        _ = loaded.Events[1].CoordinateMode.Should().BeNull();
        _ = MacroPositionSemantics.ResolveCoordinateMode(loaded.Events[0], loaded.IsAbsoluteCoordinates)
            .Should().Be(MouseCoordinateMode.Relative);
        _ = MacroPositionSemantics.ResolveCoordinateMode(loaded.Events[1], loaded.IsAbsoluteCoordinates)
            .Should().Be(MouseCoordinateMode.Relative);
    }

    [Fact]
    public async Task SaveAndLoad_WhenMixedExplicitCoordinateModes_PreservesEventModesAndHeaderUsesFirstExplicitMode()
    {
        // Arrange
        var macro = new MacroSequence
        {
            Name = "Mixed Explicit Modes",
            IsAbsoluteCoordinates = false,
            Events = {
                new() { Type = EventType.MouseMove, X = 100, Y = 200, CoordinateMode = MouseCoordinateMode.Absolute },
                new()
                {
                    Type = EventType.MouseMove,
                    X = 10,
                    Y = 20,
                    CoordinateMode = MouseCoordinateMode.Relative,
                    CoordinateSpace = MouseCoordinateSpace.RawDevice,
                },
            },
        };
        var filePath = GetTempFilePath();

        // Act
        await _manager.SaveAsync(macro, filePath);
        var saved = await File.ReadAllTextAsync(filePath, NonCancelableToken);
        var loaded = await _manager.LoadAsync(filePath);

        // Assert
        _ = saved.Should().Contain("M,abs,100,200");
        _ = saved.Should().Contain("M,rel-raw,10,20");
        _ = loaded.Should().NotBeNull();
        _ = loaded.Events.Select(ev => ev.CoordinateMode).Should().Equal(
            MouseCoordinateMode.Absolute,
            MouseCoordinateMode.Relative);
        _ = loaded.Events.Select(ev => ev.CoordinateSpace).Should().Equal(
            MouseCoordinateSpace.LogicalDesktop,
            MouseCoordinateSpace.RawDevice);
    }

    [Fact]
    public async Task SaveAndLoad_WhenExplicitAndLegacyFallbackModesAreMixed_PreservesLegacyFallbackHeader()
    {
        // Arrange
        var macro = new MacroSequence
        {
            Name = "Mixed Explicit And Legacy Fallback",
            IsAbsoluteCoordinates = false,
            Events = {
                new() { Type = EventType.MouseMove, X = 100, Y = 200, CoordinateMode = MouseCoordinateMode.Absolute },
                new() { Type = EventType.MouseMove, X = 10, Y = 20 },
                new() { Type = EventType.Click, X = 5, Y = 6, Button = MacroMouseButton.Left },
            },
        };
        var filePath = GetTempFilePath();

        // Act
        await _manager.SaveAsync(macro, filePath);
        var saved = await File.ReadAllTextAsync(filePath, NonCancelableToken);
        var loaded = await _manager.LoadAsync(filePath);

        // Assert
        _ = saved.Should().Contain("M,abs,100,200");
        _ = saved.Should().Contain("M,10,20");
        _ = saved.Should().Contain("C,5,6,Left");
        _ = loaded.Should().NotBeNull();
        _ = loaded!.IsAbsoluteCoordinates.Should().BeFalse();
        _ = MacroPositionSemantics.ResolveCoordinateMode(loaded.Events[0], loaded.IsAbsoluteCoordinates)
            .Should().Be(MouseCoordinateMode.Absolute);
        _ = MacroPositionSemantics.ResolveCoordinateMode(loaded.Events[1], loaded.IsAbsoluteCoordinates)
            .Should().Be(MouseCoordinateMode.Relative);
        _ = MacroPositionSemantics.ResolveCoordinateMode(loaded.Events[2], loaded.IsAbsoluteCoordinates)
            .Should().Be(MouseCoordinateMode.Relative);
    }

    [Fact]
    public async Task SaveAndLoad_WhenExplicitButtonCoordinateModes_PreservesModeTokens()
    {
        // Arrange
        var macro = new MacroSequence
        {
            Name = "Explicit Button Modes",
            IsAbsoluteCoordinates = true,
            Events = {
                new() { Type = EventType.ButtonPress, X = 1, Y = 2, Button = MacroMouseButton.Left, CoordinateMode = MouseCoordinateMode.Absolute },
                new()
                {
                    Type = EventType.ButtonRelease,
                    X = 3,
                    Y = 4,
                    Button = MacroMouseButton.Right,
                    CoordinateMode = MouseCoordinateMode.Relative,
                    CoordinateSpace = MouseCoordinateSpace.RawDevice,
                },
                new()
                {
                    Type = EventType.Click,
                    X = 5,
                    Y = 6,
                    Button = MacroMouseButton.Middle,
                    CoordinateMode = MouseCoordinateMode.Relative,
                    CoordinateSpace = MouseCoordinateSpace.LogicalDesktop,
                },
            },
        };
        var filePath = GetTempFilePath();

        // Act
        await _manager.SaveAsync(macro, filePath);
        var saved = await File.ReadAllTextAsync(filePath, NonCancelableToken);
        var loaded = await _manager.LoadAsync(filePath);

        // Assert
        _ = saved.Should().Contain("P,abs,1,2,Left");
        _ = saved.Should().Contain("R,rel-raw,3,4,Right");
        _ = saved.Should().Contain("C,rel-logical,5,6,Middle");
        _ = loaded.Should().NotBeNull();
        _ = loaded!.Events.Select(ev => ev.CoordinateMode).Should().Equal(
            MouseCoordinateMode.Absolute,
            MouseCoordinateMode.Relative,
            MouseCoordinateMode.Relative);
        _ = loaded.Events.Select(ev => ev.CoordinateSpace).Should().Equal(
            MouseCoordinateSpace.LogicalDesktop,
            MouseCoordinateSpace.RawDevice,
            MouseCoordinateSpace.LogicalDesktop);
    }

    [Fact]
    public async Task SaveAndLoad_WhenCurrentPositionHasCoordinateMode_DoesNotWriteOrRestoreModeToken()
    {
        // Arrange
        var macro = new MacroSequence
        {
            Name = "Current Position No Mode",
            IsAbsoluteCoordinates = false,
            SkipInitialZeroZero = true,
            Events = {
                new()
                {
                    Type = EventType.Click,
                    X = 0,
                    Y = 0,
                    Button = MacroMouseButton.Left,
                    UseCurrentPosition = true,
                    CoordinateMode = MouseCoordinateMode.Relative,
                },
            },
        };
        var filePath = GetTempFilePath();

        // Act
        await _manager.SaveAsync(macro, filePath);
        var saved = await File.ReadAllTextAsync(filePath, NonCancelableToken);
        var loaded = await _manager.LoadAsync(filePath);

        // Assert
        _ = saved.Should().Contain("C,0,0,Left,CurrentPosition");
        _ = saved.Should().NotContain("C,rel,0,0,Left");
        _ = loaded.Should().NotBeNull();
        _ = loaded!.Events.Should().ContainSingle();
        _ = loaded.Events[0].UseCurrentPosition.Should().BeTrue();
        _ = loaded.Events[0].CoordinateMode.Should().BeNull();
    }

    [Fact]
    public async Task SaveAndLoad_WhenScrollHasCoordinateMode_DoesNotWriteOrRestoreModeToken()
    {
        // Arrange
        var macro = new MacroSequence
        {
            Name = "Scroll No Mode",
            IsAbsoluteCoordinates = false,
            Events = {
                new()
                {
                    Type = EventType.Click,
                    X = 0,
                    Y = 0,
                    Button = MacroMouseButton.ScrollDown,
                    CoordinateMode = MouseCoordinateMode.Absolute,
                },
            },
        };
        var filePath = GetTempFilePath();

        // Act
        await _manager.SaveAsync(macro, filePath);
        var saved = await File.ReadAllTextAsync(filePath, NonCancelableToken);
        var loaded = await _manager.LoadAsync(filePath);

        // Assert
        _ = saved.Should().Contain("C,0,0,ScrollDown");
        _ = saved.Should().NotContain("C,abs,0,0,ScrollDown");
        _ = loaded.Should().NotBeNull();
        _ = loaded!.Events.Should().ContainSingle();
        _ = loaded.Events[0].Button.Should().Be(MacroMouseButton.ScrollDown);
        _ = loaded.Events[0].CoordinateMode.Should().BeNull();
    }

    [Fact]
    public async Task Load_WhenMalformedCoordinateModeTokenAppears_IgnoresLineAndContinues()
    {
        // Arrange
        var filePath = GetTempFilePath();
        const string content = "# Name: Invalid Mode\n# IsAbsolute: True\n# Format: CrossMacroFormatV2\n[Events]\nM,foo,1,2\nP,bar,3,4,Left\nM,abs,10,20";

        await File.WriteAllTextAsync(filePath, content, NonCancelableToken);

        // Act
        var loaded = await _manager.LoadAsync(filePath);

        // Assert
        _ = loaded.Should().NotBeNull();
        _ = loaded!.Events.Should().ContainSingle();
        _ = loaded.Events[0].Type.Should().Be(EventType.MouseMove);
        _ = loaded.Events[0].CoordinateMode.Should().Be(MouseCoordinateMode.Absolute);
        _ = loaded.Events[0].X.Should().Be(10);
        _ = loaded.Events[0].Y.Should().Be(20);
    }

    [Fact]
    public async Task LoadAsync_WhenTextInputBoundaryMetadataIsMalformed_IgnoresBoundaryAndLoadsEvents()
    {
        // Arrange
        var filePath = GetTempFilePath();
        await File.WriteAllLinesAsync(filePath,
        [
            "# Name: Malformed Boundary",
            "# TextInputBoundaryBase64: not-base64",
            "# Format: CrossMacroFormatV2",
            "[Events]",
            "KP,65",
            "KR,65",
        ], NonCancelableToken);

        // Act
        var loaded = await _manager.LoadAsync(filePath);

        // Assert
        _ = loaded.Should().NotBeNull();
        _ = loaded!.TextInputBoundaries.Should().BeEmpty();
        _ = loaded.Events.Should().HaveCount(2);
    }

    [Fact]
    public async Task LoadAsync_WhenTextInputBoundaryMetadataUsesLegacyPascalCaseJson_LoadsBoundary()
    {
        // Arrange
        const string boundaryJson = "{\"StartEventIndex\":0,\"EventCount\":2,\"Text\":\"legacy text\"}";
        var encodedBoundary = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(boundaryJson));
        var filePath = GetTempFilePath();
        await File.WriteAllLinesAsync(filePath,
        [
            "# Name: Legacy Boundary",
            $"# TextInputBoundaryBase64: {encodedBoundary}",
            "# Format: CrossMacroFormatV2",
            "[Events]",
            "KP,65",
            "KR,65",
        ], NonCancelableToken);

        // Act
        var loaded = await _manager.LoadAsync(filePath);

        // Assert
        _ = loaded.Should().NotBeNull();
        _ = loaded!.TextInputBoundaries.Should().Equal(new TextInputBoundary(0, 2, "legacy text"));
        _ = loaded.Events.Should().HaveCount(2);
    }

    private static byte[] CreateOversizedPngBytes()
    {
        return
        [
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
            0x00, 0x00, 0x00, 0x0D,
            0x49, 0x48, 0x44, 0x52,
            0x00, 0x00, 0x1E, 0x01,
            0x00, 0x00, 0x00, 0x01,
            0x08,
            0x02,
            0x00,
            0x00,
            0x00,
            0x6C, 0xF7, 0xBC, 0x13,
        ];
    }

    private sealed class TestKeyboardLayoutService : IKeyboardLayoutService
    {
        public string GetKeyName(int keyCode)
        {
            return keyCode.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        public int GetKeyCode(string keyName)
        {
            return -1;
        }

        public char? GetCharFromKeyCode(
            int keyCode,
            bool leftShift,
            bool rightShift,
            bool rightAlt,
            bool leftAlt,
            bool leftCtrl,
            bool capsLock)
        {
            return keyCode is >= char.MinValue and <= char.MaxValue ? (char)keyCode : null;
        }

        public (int KeyCode, bool Shift, bool AltGr)? GetInputForChar(char c)
        {
            return (char.ToUpperInvariant(c), char.IsUpper(c), false);
        }
    }
}
