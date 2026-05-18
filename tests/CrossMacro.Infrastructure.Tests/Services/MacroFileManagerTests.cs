namespace CrossMacro.Infrastructure.Tests.Services;

using CrossMacro.Core.Models;
using CrossMacro.Infrastructure.Services;
using FluentAssertions;

public class MacroFileManagerTests : IDisposable
{
    private readonly MacroFileManager _manager;
    private readonly List<string> _tempFiles = new();

    public MacroFileManagerTests()
    {
        _manager = new MacroFileManager();
    }

    public void Dispose()
    {
        foreach (var file in _tempFiles)
        {
            try
            {
                if (File.Exists(file))
                    File.Delete(file);
            }
            catch { /* Ignore cleanup errors */ }
        }
    }

    private string GetTempFilePath()
    {
        var path = Path.Combine(Path.GetTempPath(), $"test_macro_{Guid.NewGuid()}.macro");
        _tempFiles.Add(path);
        return path;
    }

    private MacroSequence CreateValidMacro(string name = "Test Macro")
    {
        return new MacroSequence
        {
            Name = name,
            IsAbsoluteCoordinates = true,
            Events = new List<MacroEvent>
            {
                new() { Type = EventType.MouseMove, X = 100, Y = 200, Timestamp = 0, DelayMs = 0 },
                new() { Type = EventType.ButtonPress, X = 100, Y = 200, Button = MouseButton.Left, Timestamp = 100, DelayMs = 100 },
                new() { Type = EventType.ButtonRelease, X = 100, Y = 200, Button = MouseButton.Left, Timestamp = 150, DelayMs = 50 }
            }
        };
    }

    [Fact]
    public async Task SaveAsync_NullMacro_ThrowsArgumentNullException()
    {
        // Arrange
        var filePath = GetTempFilePath();

        // Act
        var act = async () => await _manager.SaveAsync(null!, filePath);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task SaveAsync_EmptyFilePath_ThrowsArgumentException()
    {
        // Arrange
        var macro = CreateValidMacro();

        // Act
        var act = async () => await _manager.SaveAsync(macro, "");

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task SaveAsync_WhitespaceFilePath_ThrowsArgumentException()
    {
        // Arrange
        var macro = CreateValidMacro();

        // Act
        var act = async () => await _manager.SaveAsync(macro, "   ");

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
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
        await act.Should().ThrowAsync<InvalidOperationException>();
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
        File.Exists(filePath).Should().BeTrue();
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
            Directory.Exists(tempDir).Should().BeTrue();
            File.Exists(filePath).Should().BeTrue();
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task LoadAsync_EmptyFilePath_ThrowsArgumentException()
    {
        // Act
        var act = async () => await _manager.LoadAsync("");

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task LoadAsync_NonExistentFile_ThrowsFileNotFoundException()
    {
        // Act
        var act = async () => await _manager.LoadAsync("/nonexistent/path/macro.macro");

        // Assert
        await act.Should().ThrowAsync<FileNotFoundException>();
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
        loaded.Should().NotBeNull();
        loaded!.Name.Should().Be("Round Trip Test");
    }

    [Fact]
    public async Task SaveAndLoad_RoundTrip_PreservesEventCount()
    {
        // Arrange
        var macro = CreateValidMacro();
        var filePath = GetTempFilePath();

        // Act
        await _manager.SaveAsync(macro, filePath);
        var loaded = await _manager.LoadAsync(filePath);

        // Assert
        loaded.Should().NotBeNull();
        loaded!.Events.Should().HaveCount(macro.Events.Count);
    }

    [Fact]
    public async Task SaveAndLoad_RoundTrip_PreservesIsAbsoluteCoordinates()
    {
        // Arrange
        var macro = CreateValidMacro();
        macro.IsAbsoluteCoordinates = true;
        var filePath = GetTempFilePath();

        // Act
        await _manager.SaveAsync(macro, filePath);
        var loaded = await _manager.LoadAsync(filePath);

        // Assert
        loaded!.IsAbsoluteCoordinates.Should().BeTrue();
    }

    [Fact]
    public async Task SaveAndLoad_RoundTrip_PreservesMouseMoveEvents()
    {
        // Arrange
        var macro = new MacroSequence
        {
            Name = "Move Test",
            Events = new List<MacroEvent>
            {
                new() { Type = EventType.MouseMove, X = 500, Y = 600, Timestamp = 0, DelayMs = 0 }
            }
        };
        var filePath = GetTempFilePath();

        // Act
        await _manager.SaveAsync(macro, filePath);
        var loaded = await _manager.LoadAsync(filePath);

        // Assert
        loaded!.Events.Should().HaveCount(1);
        loaded.Events[0].Type.Should().Be(EventType.MouseMove);
        loaded.Events[0].X.Should().Be(500);
        loaded.Events[0].Y.Should().Be(600);
    }

    [Fact]
    public async Task SaveAndLoad_RoundTrip_PreservesKeyboardEvents()
    {
        // Arrange
        var macro = new MacroSequence
        {
            Name = "Keyboard Test",
            Events = new List<MacroEvent>
            {
                new() { Type = EventType.KeyPress, KeyCode = 30, Timestamp = 0, DelayMs = 0 },
                new() { Type = EventType.KeyRelease, KeyCode = 30, Timestamp = 50, DelayMs = 50 }
            }
        };
        var filePath = GetTempFilePath();

        // Act
        await _manager.SaveAsync(macro, filePath);
        var loaded = await _manager.LoadAsync(filePath);

        // Assert
        loaded!.Events.Should().HaveCount(2);
        loaded.Events[0].Type.Should().Be(EventType.KeyPress);
        loaded.Events[0].KeyCode.Should().Be(30);
        loaded.Events[1].Type.Should().Be(EventType.KeyRelease);
        loaded.Events[1].KeyCode.Should().Be(30);
    }

    [Fact]
    public async Task SaveAndLoad_RoundTrip_PreservesDelays()
    {
        // Arrange
        var macro = new MacroSequence
        {
            Name = "Delay Test",
            Events = new List<MacroEvent>
            {
                new() { Type = EventType.MouseMove, X = 0, Y = 0, Timestamp = 0, DelayMs = 0 },
                new() { Type = EventType.MouseMove, X = 100, Y = 100, Timestamp = 500, DelayMs = 500 },
                new() { Type = EventType.MouseMove, X = 200, Y = 200, Timestamp = 1500, DelayMs = 1000 }
            }
        };
        var filePath = GetTempFilePath();

        // Act
        await _manager.SaveAsync(macro, filePath);
        var loaded = await _manager.LoadAsync(filePath);

        // Assert
        loaded!.Events[1].DelayMs.Should().Be(500);
        loaded.Events[2].DelayMs.Should().Be(1000);
    }

    [Fact]
    public async Task SaveAndLoad_RoundTrip_PreservesButtonEvents()
    {
        // Arrange
        var macro = new MacroSequence
        {
            Name = "Button Test",
            Events = new List<MacroEvent>
            {
                new() { Type = EventType.ButtonPress, X = 100, Y = 200, Button = MouseButton.Right, Timestamp = 0, DelayMs = 0 },
                new() { Type = EventType.ButtonRelease, X = 100, Y = 200, Button = MouseButton.Right, Timestamp = 100, DelayMs = 100 }
            }
        };
        var filePath = GetTempFilePath();

        // Act
        await _manager.SaveAsync(macro, filePath);
        var loaded = await _manager.LoadAsync(filePath);

        // Assert
        loaded!.Events[0].Button.Should().Be(MouseButton.Right);
        loaded.Events[1].Button.Should().Be(MouseButton.Right);
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
            Events = new List<MacroEvent>
            {
                new()
                {
                    Type = EventType.Click,
                    X = 0,
                    Y = 0,
                    Button = MouseButton.Left,
                    UseCurrentPosition = true
                }
            }
        };
        var filePath = GetTempFilePath();

        // Act
        await _manager.SaveAsync(macro, filePath);
        var loaded = await _manager.LoadAsync(filePath);

        // Assert
        loaded.Should().NotBeNull();
        loaded!.Events.Should().ContainSingle();
        loaded.Events[0].UseCurrentPosition.Should().BeTrue();
    }

    [Fact]
    public async Task Load_ParsesWaitCommands()
    {
        // Arrange - Manual file with WAIT command
        var filePath = GetTempFilePath();
        var content = @"# Name: Wait Test
# Created: 2024-01-01T00:00:00Z
# DurationMs: 1000
# IsAbsolute: True
# Format: Cmd,Args...
M,0,0
W,500
M,100,100";

        await File.WriteAllTextAsync(filePath, content);

        // Act
        var loaded = await _manager.LoadAsync(filePath);

        // Assert
        loaded!.Events.Should().HaveCount(2);
        loaded.Events[1].DelayMs.Should().Be(500);
    }

    [Fact]
    public async Task Load_WhenMalformedEventAppears_DoesNotLeakDelayToNextValidEvent()
    {
        // Arrange
        var filePath = GetTempFilePath();
        var content = @"# Name: Delay Leak Test
# Created: 2024-01-01T00:00:00Z
# DurationMs: 1000
# IsAbsolute: True
# Format: Cmd,Args...
M,0,0
W,500
P,invalid,10,Left
M,100,100";

        await File.WriteAllTextAsync(filePath, content);

        // Act
        var loaded = await _manager.LoadAsync(filePath);

        // Assert
        loaded.Should().NotBeNull();
        loaded!.Events.Should().HaveCount(2);
        loaded.Events[0].DelayMs.Should().Be(0);
        loaded.Events[1].DelayMs.Should().Be(0);
    }

    [Fact]
    public async Task Load_WhenLegacyCurrentPositionMacro_IsUpgradedToExplicitFlag()
    {
        // Arrange
        var filePath = GetTempFilePath();
        var content = @"# Name: Legacy Current Position Test
# Created: 2024-01-01T00:00:00Z
# DurationMs: 0
# IsAbsolute: False
# SkipInitialZero: True
# Format: Cmd,Args...
C,0,0,Left";

        await File.WriteAllTextAsync(filePath, content);

        // Act
        var loaded = await _manager.LoadAsync(filePath);

        // Assert
        loaded.Should().NotBeNull();
        loaded!.Events.Should().ContainSingle();
        loaded.Events[0].UseCurrentPosition.Should().BeTrue();
    }

    [Fact]
    public async Task Load_WhenLegacyCurrentPositionMacroHasLaterRelativeMove_UpgradesLeadingClickOnly()
    {
        // Arrange
        var filePath = GetTempFilePath();
        var content = @"# Name: Legacy Current Position Followed By Move
# Created: 2024-01-01T00:00:00Z
# DurationMs: 0
# IsAbsolute: False
# SkipInitialZero: True
# Format: Cmd,Args...
C,0,0,Left
M,15,5
C,0,0,Left";

        await File.WriteAllTextAsync(filePath, content);

        // Act
        var loaded = await _manager.LoadAsync(filePath);

        // Assert
        loaded.Should().NotBeNull();
        loaded!.Events.Should().HaveCount(3);
        loaded.Events[0].UseCurrentPosition.Should().BeTrue();
        loaded.Events[1].Type.Should().Be(EventType.MouseMove);
        loaded.Events[2].UseCurrentPosition.Should().BeFalse();
    }

    [Fact]
    public async Task Load_WhenExplicitRelativeZeroButtonEvent_DoesNotUpgradeToCurrentPosition()
    {
        // Arrange
        var filePath = GetTempFilePath();
        var content = @"# Name: Explicit Relative Zero Click
# Created: 2024-01-01T00:00:00Z
# DurationMs: 0
# IsAbsolute: False
# SkipInitialZero: True
# Format: Cmd,Args...
C,rel,0,0,Left";

        await File.WriteAllTextAsync(filePath, content);

        // Act
        var loaded = await _manager.LoadAsync(filePath);

        // Assert
        loaded.Should().NotBeNull();
        loaded!.Events.Should().ContainSingle();
        loaded.Events[0].UseCurrentPosition.Should().BeFalse();
        loaded.Events[0].CoordinateMode.Should().Be(MouseCoordinateMode.Relative);
    }

    [Fact]
    public async Task SaveAndLoad_RoundTrip_PreservesRandomDelayMetadata()
    {
        // Arrange
        var macro = new MacroSequence
        {
            Name = "Random Delay Test",
            Events = new List<MacroEvent>
            {
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
                    RandomDelayMaxMs = 120
                }
            }
        };
        var filePath = GetTempFilePath();

        // Act
        await _manager.SaveAsync(macro, filePath);
        var loaded = await _manager.LoadAsync(filePath);

        // Assert
        loaded.Should().NotBeNull();
        loaded!.Events.Should().HaveCount(2);
        loaded.Events[1].DelayMs.Should().Be(40);
        loaded.Events[1].HasRandomDelay.Should().BeTrue();
        loaded.Events[1].RandomDelayMinMs.Should().Be(60);
        loaded.Events[1].RandomDelayMaxMs.Should().Be(120);
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
            Events = new List<MacroEvent>
            {
                new() { Type = EventType.MouseMove, X = 0, Y = 0, Timestamp = 0, DelayMs = 0 }
            }
        };
        var filePath = GetTempFilePath();

        // Act
        await _manager.SaveAsync(macro, filePath);
        var loaded = await _manager.LoadAsync(filePath);

        // Assert
        loaded.Should().NotBeNull();
        loaded!.HasTrailingRandomDelay.Should().BeTrue();
        loaded.TrailingDelayMinMs.Should().Be(25);
        loaded.TrailingDelayMaxMs.Should().Be(75);
    }

    [Fact]
    public async Task Load_ParsesRandomWaitCommands()
    {
        // Arrange
        var filePath = GetTempFilePath();
        var content = @"# Name: Wait Random Test
# Created: 2024-01-01T00:00:00Z
# DurationMs: 1000
# IsAbsolute: True
# Format: Cmd,Args...
M,0,0
WR,100,250
M,100,100";

        await File.WriteAllTextAsync(filePath, content);

        // Act
        var loaded = await _manager.LoadAsync(filePath);

        // Assert
        loaded.Should().NotBeNull();
        loaded!.Events.Should().HaveCount(2);
        loaded.Events[1].DelayMs.Should().Be(0);
        loaded.Events[1].HasRandomDelay.Should().BeTrue();
        loaded.Events[1].RandomDelayMinMs.Should().Be(100);
        loaded.Events[1].RandomDelayMaxMs.Should().Be(250);
    }

    [Fact]
    public async Task SaveAndLoad_RoundTrip_PreservesScriptSteps()
    {
        // Arrange
        var macro = new MacroSequence
        {
            Name = "Script Step Round Trip",
            ScriptSteps =
            [
                "set i 0",
                "for i from 1 to 10 {",
                "click left",
                "}"
            ],
            Events = new List<MacroEvent>
            {
                new() { Type = EventType.Click, Button = MouseButton.Left, DelayMs = 0 }
            }
        };
        var filePath = GetTempFilePath();

        // Act
        await _manager.SaveAsync(macro, filePath);
        var loaded = await _manager.LoadAsync(filePath);

        // Assert
        loaded.Should().NotBeNull();
        loaded!.ScriptSteps.Should().Equal(macro.ScriptSteps);
    }

    [Fact]
    public async Task SaveAndLoad_RoundTrip_PreservesTextInputBoundaries()
    {
        // Arrange
        var macro = new MacroSequence
        {
            Name = "Text Boundary Round Trip",
            Events = new List<MacroEvent>
            {
                new() { Type = EventType.KeyPress, KeyCode = 65 },
                new() { Type = EventType.KeyRelease, KeyCode = 65 },
                new() { Type = EventType.KeyPress, KeyCode = 66 },
                new() { Type = EventType.KeyRelease, KeyCode = 66 }
            },
            TextInputBoundaries =
            [
                new TextInputBoundary(0, 2, "a,b $1"),
                new TextInputBoundary(2, 2, "çok satırlı\nmetin")
            ]
        };
        var filePath = GetTempFilePath();

        // Act
        await _manager.SaveAsync(macro, filePath);
        var loaded = await _manager.LoadAsync(filePath);

        // Assert
        loaded.Should().NotBeNull();
        loaded!.TextInputBoundaries.Should().Equal(macro.TextInputBoundaries);
    }

    [Fact]
    public async Task Load_WhenLegacyAbsoluteEventsHaveNoModeTokens_UsesHeaderFallback()
    {
        // Arrange
        var filePath = GetTempFilePath();
        var content = @"# Name: Legacy Absolute
# IsAbsolute: True
# Format: Cmd,Args...
M,10,20
P,10,20,Left";

        await File.WriteAllTextAsync(filePath, content);

        // Act
        var loaded = await _manager.LoadAsync(filePath);

        // Assert
        loaded.Should().NotBeNull();
        loaded!.IsAbsoluteCoordinates.Should().BeTrue();
        loaded.Events.Should().HaveCount(2);
        loaded.Events[0].CoordinateMode.Should().BeNull();
        loaded.Events[1].CoordinateMode.Should().BeNull();
        MacroPositionSemantics.ResolveCoordinateMode(loaded.Events[0], loaded.IsAbsoluteCoordinates)
            .Should().Be(MouseCoordinateMode.Absolute);
        MacroPositionSemantics.ResolveCoordinateMode(loaded.Events[1], loaded.IsAbsoluteCoordinates)
            .Should().Be(MouseCoordinateMode.Absolute);
    }

    [Fact]
    public async Task Load_WhenLegacyRelativeEventsHaveNoModeTokens_UsesHeaderFallback()
    {
        // Arrange
        var filePath = GetTempFilePath();
        var content = @"# Name: Legacy Relative
# IsAbsolute: False
# SkipInitialZero: False
# Format: Cmd,Args...
M,5,6
C,5,6,Right";

        await File.WriteAllTextAsync(filePath, content);

        // Act
        var loaded = await _manager.LoadAsync(filePath);

        // Assert
        loaded.Should().NotBeNull();
        loaded!.IsAbsoluteCoordinates.Should().BeFalse();
        loaded.Events.Should().HaveCount(2);
        loaded.Events[0].CoordinateMode.Should().BeNull();
        loaded.Events[1].CoordinateMode.Should().BeNull();
        MacroPositionSemantics.ResolveCoordinateMode(loaded.Events[0], loaded.IsAbsoluteCoordinates)
            .Should().Be(MouseCoordinateMode.Relative);
        MacroPositionSemantics.ResolveCoordinateMode(loaded.Events[1], loaded.IsAbsoluteCoordinates)
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
            Events = new List<MacroEvent>
            {
                new() { Type = EventType.MouseMove, X = 100, Y = 200, CoordinateMode = MouseCoordinateMode.Absolute },
                new() { Type = EventType.MouseMove, X = 10, Y = 20, CoordinateMode = MouseCoordinateMode.Relative }
            }
        };
        var filePath = GetTempFilePath();

        // Act
        await _manager.SaveAsync(macro, filePath);
        var saved = await File.ReadAllTextAsync(filePath);
        var loaded = await _manager.LoadAsync(filePath);

        // Assert
        saved.Should().Contain("# IsAbsolute: True");
        saved.Should().Contain("M,abs,100,200");
        saved.Should().Contain("M,rel,10,20");
        loaded.Should().NotBeNull();
        loaded!.IsAbsoluteCoordinates.Should().BeTrue();
        loaded.Events.Select(ev => ev.CoordinateMode).Should().Equal(
            MouseCoordinateMode.Absolute,
            MouseCoordinateMode.Relative);
    }

    [Fact]
    public async Task SaveAndLoad_WhenExplicitAndLegacyFallbackModesAreMixed_PreservesLegacyFallbackHeader()
    {
        // Arrange
        var macro = new MacroSequence
        {
            Name = "Mixed Explicit And Legacy Fallback",
            IsAbsoluteCoordinates = false,
            Events = new List<MacroEvent>
            {
                new() { Type = EventType.MouseMove, X = 100, Y = 200, CoordinateMode = MouseCoordinateMode.Absolute },
                new() { Type = EventType.MouseMove, X = 10, Y = 20 },
                new() { Type = EventType.Click, X = 5, Y = 6, Button = MouseButton.Left }
            }
        };
        var filePath = GetTempFilePath();

        // Act
        await _manager.SaveAsync(macro, filePath);
        var saved = await File.ReadAllTextAsync(filePath);
        var loaded = await _manager.LoadAsync(filePath);

        // Assert
        saved.Should().Contain("# IsAbsolute: False");
        saved.Should().Contain("M,abs,100,200");
        saved.Should().Contain("M,10,20");
        saved.Should().Contain("C,5,6,Left");
        loaded.Should().NotBeNull();
        loaded!.IsAbsoluteCoordinates.Should().BeFalse();
        MacroPositionSemantics.ResolveCoordinateMode(loaded.Events[0], loaded.IsAbsoluteCoordinates)
            .Should().Be(MouseCoordinateMode.Absolute);
        MacroPositionSemantics.ResolveCoordinateMode(loaded.Events[1], loaded.IsAbsoluteCoordinates)
            .Should().Be(MouseCoordinateMode.Relative);
        MacroPositionSemantics.ResolveCoordinateMode(loaded.Events[2], loaded.IsAbsoluteCoordinates)
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
            Events = new List<MacroEvent>
            {
                new() { Type = EventType.ButtonPress, X = 1, Y = 2, Button = MouseButton.Left, CoordinateMode = MouseCoordinateMode.Absolute },
                new() { Type = EventType.ButtonRelease, X = 3, Y = 4, Button = MouseButton.Right, CoordinateMode = MouseCoordinateMode.Relative },
                new() { Type = EventType.Click, X = 5, Y = 6, Button = MouseButton.Middle, CoordinateMode = MouseCoordinateMode.Relative }
            }
        };
        var filePath = GetTempFilePath();

        // Act
        await _manager.SaveAsync(macro, filePath);
        var saved = await File.ReadAllTextAsync(filePath);
        var loaded = await _manager.LoadAsync(filePath);

        // Assert
        saved.Should().Contain("P,abs,1,2,Left");
        saved.Should().Contain("R,rel,3,4,Right");
        saved.Should().Contain("C,rel,5,6,Middle");
        loaded.Should().NotBeNull();
        loaded!.Events.Select(ev => ev.CoordinateMode).Should().Equal(
            MouseCoordinateMode.Absolute,
            MouseCoordinateMode.Relative,
            MouseCoordinateMode.Relative);
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
            Events = new List<MacroEvent>
            {
                new()
                {
                    Type = EventType.Click,
                    X = 0,
                    Y = 0,
                    Button = MouseButton.Left,
                    UseCurrentPosition = true,
                    CoordinateMode = MouseCoordinateMode.Relative
                }
            }
        };
        var filePath = GetTempFilePath();

        // Act
        await _manager.SaveAsync(macro, filePath);
        var saved = await File.ReadAllTextAsync(filePath);
        var loaded = await _manager.LoadAsync(filePath);

        // Assert
        saved.Should().Contain("C,0,0,Left,CurrentPosition");
        saved.Should().NotContain("C,rel,0,0,Left");
        loaded.Should().NotBeNull();
        loaded!.Events.Should().ContainSingle();
        loaded.Events[0].UseCurrentPosition.Should().BeTrue();
        loaded.Events[0].CoordinateMode.Should().BeNull();
    }

    [Fact]
    public async Task SaveAndLoad_WhenScrollHasCoordinateMode_DoesNotWriteOrRestoreModeToken()
    {
        // Arrange
        var macro = new MacroSequence
        {
            Name = "Scroll No Mode",
            IsAbsoluteCoordinates = false,
            Events = new List<MacroEvent>
            {
                new()
                {
                    Type = EventType.Click,
                    X = 0,
                    Y = 0,
                    Button = MouseButton.ScrollDown,
                    CoordinateMode = MouseCoordinateMode.Absolute
                }
            }
        };
        var filePath = GetTempFilePath();

        // Act
        await _manager.SaveAsync(macro, filePath);
        var saved = await File.ReadAllTextAsync(filePath);
        var loaded = await _manager.LoadAsync(filePath);

        // Assert
        saved.Should().Contain("C,0,0,ScrollDown");
        saved.Should().NotContain("C,abs,0,0,ScrollDown");
        loaded.Should().NotBeNull();
        loaded!.Events.Should().ContainSingle();
        loaded.Events[0].Button.Should().Be(MouseButton.ScrollDown);
        loaded.Events[0].CoordinateMode.Should().BeNull();
    }

    [Fact]
    public async Task Load_WhenMalformedCoordinateModeTokenAppears_IgnoresLineAndContinues()
    {
        // Arrange
        var filePath = GetTempFilePath();
        var content = @"# Name: Invalid Mode
# IsAbsolute: True
# Format: Cmd,Args...
M,foo,1,2
P,bar,3,4,Left
M,abs,10,20";

        await File.WriteAllTextAsync(filePath, content);

        // Act
        var loaded = await _manager.LoadAsync(filePath);

        // Assert
        loaded.Should().NotBeNull();
        loaded!.Events.Should().ContainSingle();
        loaded.Events[0].Type.Should().Be(EventType.MouseMove);
        loaded.Events[0].CoordinateMode.Should().Be(MouseCoordinateMode.Absolute);
        loaded.Events[0].X.Should().Be(10);
        loaded.Events[0].Y.Should().Be(20);
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
            "# Format: Cmd,Args...",
            "KP,65",
            "KR,65"
        ]);

        // Act
        var loaded = await _manager.LoadAsync(filePath);

        // Assert
        loaded.Should().NotBeNull();
        loaded!.TextInputBoundaries.Should().BeEmpty();
        loaded.Events.Should().HaveCount(2);
    }

    [Fact]
    public async Task LoadAsync_WhenTextInputBoundaryMetadataUsesLegacyPascalCaseJson_LoadsBoundary()
    {
        // Arrange
        var boundaryJson = "{\"StartEventIndex\":0,\"EventCount\":2,\"Text\":\"legacy text\"}";
        var encodedBoundary = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(boundaryJson));
        var filePath = GetTempFilePath();
        await File.WriteAllLinesAsync(filePath,
        [
            "# Name: Legacy Boundary",
            $"# TextInputBoundaryBase64: {encodedBoundary}",
            "# Format: Cmd,Args...",
            "KP,65",
            "KR,65"
        ]);

        // Act
        var loaded = await _manager.LoadAsync(filePath);

        // Assert
        loaded.Should().NotBeNull();
        loaded!.TextInputBoundaries.Should().Equal(new TextInputBoundary(0, 2, "legacy text"));
        loaded.Events.Should().HaveCount(2);
    }
}
