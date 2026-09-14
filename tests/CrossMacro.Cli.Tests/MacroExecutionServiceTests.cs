
namespace CrossMacro.Cli.Tests;

public sealed class MacroExecutionServiceTests
{
    private readonly IMacroFileManager _fileManager;
    private readonly IMacroPlayer _player;
    private readonly IKeyCodeMapper _keyCodeMapper;
    private readonly IMacroExecutionService _service;

    public MacroExecutionServiceTests()
    {
        _fileManager = Substitute.For<IMacroFileManager>();
        _player = Substitute.For<IMacroPlayer>();
        _keyCodeMapper = CreateKeyCodeMapper();
        _service = new MacroExecutionService(
            _fileManager,
            () => _player,
            new PlaybackValidator(_keyCodeMapper, new NullMousePositionProvider("CLI Validation Provider")));
    }

    [Fact]
    public async Task ValidateAsync_WhenFileMissing_ReturnsFileError()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".macro");

        var result = await _service.ValidateAsync(path, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ExecutionOutcomeCode.FileError, result.ExitCode);
    }

    [Fact]
    public async Task ValidateAsync_WhenMacroValid_ReturnsSuccess()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            _ = _fileManager.LoadAsync(tempFile).Returns(CreateValidMacro());

            var result = await _service.ValidateAsync(tempFile, CancellationToken.None);

            Assert.True(result.Success);
            Assert.Equal(ExecutionOutcomeCode.Success, result.ExitCode);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public async Task ValidateAsync_WhenMacroInvalid_ReturnsValidationSummary()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            _ = _fileManager.LoadAsync(tempFile).Returns(new MacroSequence { Name = "invalid" });

            var result = await _service.ValidateAsync(tempFile, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal(ExecutionOutcomeCode.ValidationError, result.ExitCode);
            var payload = Assert.IsType<MacroValidationData>(result.Data);
            Assert.Equal(tempFile, payload.MacroPath);
            Assert.Equal(0, payload.EventCount);
            Assert.NotEmpty(result.Errors);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public async Task ValidateAsync_WhenScriptUsesRuntimeMappedKey_ReturnsSuccess()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            _ = _fileManager.LoadAsync(tempFile).Returns(new MacroSequence
            {
                Name = "script",
                ScriptSteps = { "pixelcolor 1 2 sampled", "tap Backspace" },
            });

            var result = await _service.ValidateAsync(tempFile, CancellationToken.None);

            Assert.True(result.Success);
            Assert.Equal(ExecutionOutcomeCode.Success, result.ExitCode);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public async Task GetInfoAsync_WhenMacroValid_ReturnsSuccessWithData()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            _ = _fileManager.LoadAsync(tempFile).Returns(CreateValidMacro());

            var result = await _service.GetInfoAsync(tempFile, CancellationToken.None);

            Assert.True(result.Success);
            Assert.Equal(ExecutionOutcomeCode.Success, result.ExitCode);
            Assert.NotNull(result.Data);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public async Task GetInfoAsync_WhenMacroContainsEventKinds_ReportsEachBreakdownCount()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            _ = _fileManager.LoadAsync(tempFile).Returns(CreateMacroWithEventKinds());

            var result = await _service.GetInfoAsync(tempFile, CancellationToken.None);

            var payload = Assert.IsType<MacroInfoData>(result.Data);
            Assert.Equal(1, payload.EventBreakdown.MouseMove);
            Assert.Equal(1, payload.EventBreakdown.ButtonPress);
            Assert.Equal(1, payload.EventBreakdown.ButtonRelease);
            Assert.Equal(1, payload.EventBreakdown.Click);
            Assert.Equal(1, payload.EventBreakdown.KeyPress);
            Assert.Equal(1, payload.EventBreakdown.KeyRelease);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public async Task GetInfoAsync_WhenMacroHasMixedCoordinateModes_ReportsMixedCoordinateMode()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            _ = _fileManager.LoadAsync(tempFile).Returns(CreateMixedMacro());

            var result = await _service.GetInfoAsync(tempFile, CancellationToken.None);

            Assert.True(result.Success);
            var payload = Assert.IsType<MacroInfoData>(result.Data);
            Assert.Equal("mixed", payload.CoordinateMode);
            Assert.False(payload.IsAbsoluteCoordinates);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public async Task GetInfoAsync_WhenMacroHasMetadata_ReportsMetadataWithoutLoss()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            _ = _fileManager.LoadAsync(tempFile).Returns(CreateMacroWithInfoMetadata());

            var result = await _service.GetInfoAsync(tempFile, CancellationToken.None);

            var payload = Assert.IsType<MacroInfoData>(result.Data);
            Assert.Equal(tempFile, payload.MacroPath);
            Assert.Equal("metadata", payload.MacroName);
            Assert.Equal(new DateTime(2024, 1, 2, 3, 4, 5, DateTimeKind.Utc), payload.CreatedAt);
            Assert.Equal(1, payload.EventCount);
            Assert.Equal(321, payload.TotalDurationMs);
            Assert.Equal("absolute", payload.CoordinateMode);
            Assert.True(payload.IsAbsoluteCoordinates);
            Assert.True(payload.SkipInitialZeroZero);
            Assert.Equal(1_234_567, payload.TrailingDelayMicroseconds);
            Assert.Equal(1_234, payload.TrailingDelayMs);
            Assert.True(payload.HasTrailingRandomDelay);
            Assert.Equal(100, payload.TrailingDelayMinMs);
            Assert.Equal(250, payload.TrailingDelayMaxMs);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public async Task ExecuteAsync_WhenDryRun_DoesNotInvokePlayer()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            _ = _fileManager.LoadAsync(tempFile).Returns(CreateValidMacro());

            var result = await _service.ExecuteAsync(new MacroExecutionRequest
            {
                MacroFilePath = tempFile,
                DryRun = true,
            }, CancellationToken.None);

            Assert.True(result.Success);
            await _player.DidNotReceive().PlayAsync(Arg.Any<MacroSequence>(), Arg.Any<PlaybackOptions>(), Arg.Any<CancellationToken>());
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public async Task ExecuteAsync_WhenDryRun_ReturnsSummaryMetadata()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            var macro = CreateMacroWithInfoMetadata();
            _ = _fileManager.LoadAsync(tempFile).Returns(macro);

            var result = await _service.ExecuteAsync(new MacroExecutionRequest
            {
                MacroFilePath = tempFile,
                DryRun = true,
            }, CancellationToken.None);

            var payload = Assert.IsType<MacroSummaryData>(result.Data);
            Assert.Equal(tempFile, payload.MacroPath);
            Assert.Equal("metadata", payload.MacroName);
            Assert.Equal(1, payload.EventCount);
            Assert.Equal(321, payload.TotalDurationMs);
            Assert.Equal("absolute", payload.CoordinateMode);
            Assert.True(payload.IsAbsoluteCoordinates);
            await _player.DidNotReceive().PlayAsync(Arg.Any<MacroSequence>(), Arg.Any<PlaybackOptions>(), Arg.Any<CancellationToken>());
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public async Task ExecuteAsync_WhenNotDryRun_InvokesPlayer()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            var macro = CreateValidMacro();
            _ = _fileManager.LoadAsync(tempFile).Returns(macro);

            var result = await _service.ExecuteAsync(new MacroExecutionRequest
            {
                MacroFilePath = tempFile,
                SpeedMultiplier = 2.0,
                Loop = true,
                RepeatCount = 3,
                RepeatDelayMs = 100,
                DryRun = false,
            }, CancellationToken.None);

            Assert.True(result.Success);
            await _player.Received(1).PlayAsync(
                macro,
                Arg.Is<PlaybackOptions>(x => x != null
                    && Math.Abs(x.SpeedMultiplier - 2.0) < 0.000001
                    && x.Loop
                    && x.RepeatCount == 3
                    && x.RepeatDelayMs == 100),
                Arg.Any<CancellationToken>());
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public async Task ExecuteAsync_WhenAbsolutePlaybackUnsupported_ReturnsDedicatedMessage()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            var macro = CreateMixedMacro();
            _ = _fileManager.LoadAsync(tempFile).Returns(macro);
            _ = _player.PlayAsync(macro, Arg.Any<PlaybackOptions>(), Arg.Any<CancellationToken>())
                .Returns<Task>(_ => throw new AbsolutePlaybackUnsupportedException("Linux UInput"));

            var result = await _service.ExecuteAsync(new MacroExecutionRequest
            {
                MacroFilePath = tempFile,
                DryRun = false,
            }, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal(ExecutionOutcomeCode.RuntimeError, result.ExitCode);
            Assert.Equal("Absolute coordinate playback is not supported in this session.", result.Message);
            Assert.Contains("active backend cannot play absolute coordinates", result.Errors.Single(), StringComparison.Ordinal);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    private static MacroSequence CreateValidMacro()
    {
        var macro = new MacroSequence
        {
            Name = "test",
        };
        macro.Events.Add(new MacroEvent
        {
            Type = EventType.MouseMove,
            X = 1,
            Y = 1,
            DelayMs = 0,
            Timestamp = 0,
        });
        return macro;
    }

    private static MacroSequence CreateMacroWithEventKinds()
    {
        var macro = new MacroSequence
        {
            Name = "breakdown",
        };

        foreach (var eventType in new[]
        {
            EventType.MouseMove,
            EventType.ButtonPress,
            EventType.ButtonRelease,
            EventType.Click,
            EventType.KeyPress,
            EventType.KeyRelease,
        })
        {
            macro.Events.Add(new MacroEvent
            {
                Type = eventType,
                DelayMs = 0,
                Timestamp = macro.Events.Count,
            });
        }

        return macro;
    }

    private static MacroSequence CreateMacroWithInfoMetadata()
    {
        var macro = new MacroSequence
        {
            Name = "metadata",
            CreatedAt = new DateTime(2024, 1, 2, 3, 4, 5, DateTimeKind.Utc),
            TotalDurationMs = 321,
            IsAbsoluteCoordinates = true,
            SkipInitialZeroZero = true,
            TrailingDelayMicroseconds = 1_234_567,
            HasTrailingRandomDelay = true,
            TrailingDelayMinMs = 100,
            TrailingDelayMaxMs = 250,
        };
        macro.Events.Add(new MacroEvent
        {
            Type = EventType.MouseMove,
            CoordinateMode = MouseCoordinateMode.Absolute,
            X = 10,
            Y = 20,
            Timestamp = 0,
        });
        return macro;
    }

    private static IKeyCodeMapper CreateKeyCodeMapper()
    {
        var keyCodeMapper = Substitute.For<IKeyCodeMapper>();
        _ = keyCodeMapper.GetKeyCode(Arg.Any<string>()).Returns(-1);
        _ = keyCodeMapper.GetKeyCode("Backspace").Returns(InputEventCode.KEY_BACKSPACE);
        _ = keyCodeMapper.IsModifierKeyCode(Arg.Any<int>()).Returns(returnThis: false);
        return keyCodeMapper;
    }

    private static MacroSequence CreateMixedMacro()
    {
        var macro = new MacroSequence
        {
            Name = "mixed",
        };

        macro.Events.Add(new MacroEvent
        {
            Type = EventType.MouseMove,
            X = 1,
            Y = 1,
            CoordinateMode = MouseCoordinateMode.Absolute,
            DelayMs = 0,
            Timestamp = 0,
        });
        macro.Events.Add(new MacroEvent
        {
            Type = EventType.MouseMove,
            X = 2,
            Y = 2,
            CoordinateMode = MouseCoordinateMode.Relative,
            DelayMs = 0,
            Timestamp = 1,
        });

        return macro;
    }

    private sealed class NullMousePositionProvider(string providerName) : IMousePositionProvider
    {
        public string ProviderName { get; } = providerName;

        public bool IsSupported => true;

        public Task<(int X, int Y)?> GetAbsolutePositionAsync()
        {
            return Task.FromResult<(int X, int Y)?>(null);
        }

        public Task<(int Width, int Height)?> GetScreenResolutionAsync()
        {
            return Task.FromResult<(int Width, int Height)?>(null);
        }

        public Task<bool> InitializationTask => Task.FromResult(true);

        public void Dispose() { }
    }
}
