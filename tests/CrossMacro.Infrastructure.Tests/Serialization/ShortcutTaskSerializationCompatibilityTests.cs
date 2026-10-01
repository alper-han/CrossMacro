namespace CrossMacro.Infrastructure.Tests.Serialization;

public sealed class ShortcutTaskSerializationCompatibilityTests
{
    [Fact]
    public void JsonRoundTrip_PreservesPersistedFields()
    {
        var task = new ShortcutTask
        {
            Name = "Shortcut",
            MacroFilePath = "macro.macro",
            Hotkeys = { "Ctrl+F9", "Shift+Space", "Alt+F9" },
            PlaybackSpeed = 1.25,
            LoopEnabled = true,
            RepeatCount = 4,
            RepeatDelayMs = 30,
            UseRandomRepeatDelay = true,
            RepeatDelayMinMs = 10,
            RepeatDelayMaxMs = 50,
            LastStatus = "Success",
            LastTriggeredTime = DateTime.UtcNow,
        };
        task.WindowRules.Add(new ShortcutWindowRule
        {
            Field = TriggerField.WindowTitle,
            MatchMode = TriggerMatchMode.Contains,
            Value = "Firefox",
        });

        var json = JsonSerializer.Serialize(task, CrossMacroJsonContext.Default.ShortcutTask);
        var roundTrip = JsonSerializer.Deserialize(json, CrossMacroJsonContext.Default.ShortcutTask);

        _ = roundTrip.Should().NotBeNull();
        _ = roundTrip.Name.Should().Be(task.Name);
        _ = roundTrip.Hotkeys.Should().Equal(task.Hotkeys);
        _ = roundTrip.RepeatDelayMaxMs.Should().Be(task.RepeatDelayMaxMs);
        _ = roundTrip.LastStatus.Should().Be(task.LastStatus);
        _ = roundTrip.WindowRules.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(task.WindowRules.Single());
    }

    [Fact]
    public void Deserialize_WhenWindowRulesIsMissing_UsesAnEmptyCollection()
    {
        const string json = "{}";

        var task = JsonSerializer.Deserialize(json, CrossMacroJsonContext.Default.ShortcutTask);

        _ = task.Should().NotBeNull();
        _ = task.WindowRules.Should().BeEmpty();
    }
    [Fact]
    public async Task LoadAsync_MigratesLegacyHotkeyStringAndPreservesEnabledState()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"crossmacro-shortcut-{Guid.NewGuid():N}.json");
        try
        {
            await File.WriteAllTextAsync(filePath, """
                [{"id":"11111111-1111-1111-1111-111111111111","name":"Legacy","macroFilePath":"legacy.macro","hotkeyString":" Ctrl+A ","isEnabled":true}]
                """, TestContext.Current.CancellationToken);


            var repository = new JsonShortcutTaskRepository(filePath, CreateNormalizer());
            var task = Assert.Single((await repository.LoadAsync(TestContext.Current.CancellationToken))!);

            _ = task.Hotkeys.Should().Equal("Ctrl+A");
            _ = task.IsEnabled.Should().BeTrue();
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public async Task LoadAsync_NormalizesPluralHotkeysPreservingOrderAndRemovingDuplicates()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"crossmacro-shortcut-{Guid.NewGuid():N}.json");
        try
        {
            await File.WriteAllTextAsync(filePath, """
                [{"name":"Plural","macroFilePath":"plural.macro","hotkeys":[" Shift+Space ","ctrl+a","Ctrl+A","Alt+F9"," "],"isEnabled":true}]
                """, TestContext.Current.CancellationToken);


            var repository = new JsonShortcutTaskRepository(filePath, CreateNormalizer());
            var task = Assert.Single((await repository.LoadAsync(TestContext.Current.CancellationToken))!);

            _ = task.Hotkeys.Should().Equal("Shift+Space", "Ctrl+A", "Alt+F9");
            _ = task.IsEnabled.Should().BeTrue();
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public async Task LoadAsync_WhenPersistedHotkeyIsInvalid_ThrowsJsonException()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"crossmacro-shortcut-{Guid.NewGuid():N}.json");
        try
        {
            await File.WriteAllTextAsync(filePath, """
                [{"name":"Invalid","macroFilePath":"invalid.macro","hotkeys":["bad"]}]
                """, TestContext.Current.CancellationToken);


            var repository = new JsonShortcutTaskRepository(filePath, new RejectingShortcutHotkeyNormalizer());

            _ = await Assert.ThrowsAsync<JsonException>(() => repository.LoadAsync(TestContext.Current.CancellationToken));
        }
        finally
        {
            File.Delete(filePath);
        }
    }
    [Fact]
    public void Constructor_RequiresStrictHotkeyNormalizer()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"crossmacro-shortcut-{Guid.NewGuid():N}.json");

        _ = Assert.Throws<ArgumentNullException>(
            () => new JsonShortcutTaskRepository(filePath, hotkeyNormalizer: null!));
    }
    private static IShortcutHotkeyNormalizer CreateNormalizer()
    {
        var layoutService = Substitute.For<IKeyboardLayoutService>();
        layoutService.GetKeyCode(Arg.Any<string>()).Returns(-1);
        layoutService.GetKeyName(30).Returns("A");
        layoutService.GetKeyName(57).Returns("Space");
        layoutService.GetKeyName(67).Returns("F9");
        var keyCodeMapper = new KeyCodeMapper(layoutService);
        return new ShortcutHotkeyNormalizer(
            keyCodeMapper,
            new HotkeyStringBuilder(keyCodeMapper),
            new MouseButtonMapper());
    }

    private sealed class RejectingShortcutHotkeyNormalizer : IShortcutHotkeyNormalizer
    {
        public bool TryNormalize(string? hotkey, out string? normalized, out string? validationMessage)
        {
            normalized = null;
            validationMessage = $"Invalid persisted shortcut hotkey: {hotkey}.";
            return false;
        }
    }
}
