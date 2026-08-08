// Backward/forward compatibility pins for the script-arithmetic feature (mul/div +
// block-argument expressions). No migration code exists anywhere: the .macro file
// layer stores script steps as raw text, and the editor's RawScriptStep fallback
// (EditorActionConverter) round-trips unknown tokens verbatim. These fixtures pin
// both guarantees against regressions.
namespace CrossMacro.Infrastructure.Tests.Services;

public sealed class ScriptArithmeticCompatibilityTests : IDisposable
{
    private static readonly CancellationToken NonCancelableToken = new(canceled: false);

    private static readonly string[] LegacyFixtureScriptSteps =
    [
        "set count 3",
        "inc count 2",
        "dec count",
        "repeat $count {",
        "move abs 100 200",
        "click left",
        "}",
    ];

    private static readonly string[] ForwardFixtureScriptSteps =
    [
        "set a 10",
        "mul a 2",
        "div a 4",
        "repeat $a / 2 {",
        "click left",
        "}",
    ];

    private readonly MacroFileManager _manager = new(() => new KeyCodeMapper(new TestKeyboardLayoutService()));
    private readonly EditorActionConverter _converter = new(new KeyCodeMapper(new TestKeyboardLayoutService()));
    private readonly List<string> _tempFiles = new();

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

    private static string GetFixturePath(string fixtureName)
    {
        return Path.Combine(AppContext.BaseDirectory, "Fixtures", "Macros", fixtureName);
    }

    private static IReadOnlyList<string> ExtractScriptSectionLines(string fileText)
    {
        var lines = fileText.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        return lines
            .SkipWhile(line => line.Trim() != "[Script]")
            .Skip(1)
            .TakeWhile(line => line.Trim() != "[Events]")
            .ToList();
    }

    [Fact]
    public async Task LegacyFixture_LoadsOnCurrentBuild_WithZeroWarningsAndTypedActionsOnly()
    {
        var loaded = await _manager.LoadAsync(GetFixturePath("script-arithmetic-legacy.macro"));

        _ = loaded.Should().NotBeNull();
        _ = loaded!.ScriptSteps.Should().Equal(LegacyFixtureScriptSteps);
        _ = loaded.Events.Should().HaveCount(2);

        var restore = _converter.FromMacroSequenceWithDiagnostics(loaded);

        _ = restore.RestoredFromScriptSteps.Should().BeTrue();
        _ = restore.HasWarnings.Should().BeFalse();
        _ = restore.Actions.Select(action => action.Type).Should().Equal(
            EditorActionType.SetVariable,
            EditorActionType.IncrementVariable,
            EditorActionType.DecrementVariable,
            EditorActionType.RepeatBlockStart,
            EditorActionType.MouseMove,
            EditorActionType.MouseClick,
            EditorActionType.BlockEnd);
        _ = restore.Actions.Should().NotContain(action => action.Type == EditorActionType.RawScriptStep);

        var set = restore.Actions[0];
        _ = set.ScriptVariableName.Should().Be("count");
        _ = set.ScriptValue.Should().Be("3");

        var increment = restore.Actions[1];
        _ = increment.ScriptVariableName.Should().Be("count");
        _ = increment.ScriptNumericValue.Should().Be("2");
        _ = increment.ScriptNumericSourceType.Should().Be(ScriptNumericSourceType.Number);

        var decrement = restore.Actions[2];
        _ = decrement.ScriptVariableName.Should().Be("count");
        _ = decrement.ScriptNumericValue.Should().Be("1");

        var repeat = restore.Actions[3];
        _ = repeat.ScriptNumericSourceType.Should().Be(ScriptNumericSourceType.VariableReference);
        _ = repeat.ScriptNumericValue.Should().Be("count");
        _ = repeat.PreferLegacyScriptText.Should().BeFalse();
    }

    [Fact]
    public async Task ForwardFixture_FileLayer_PreservesNewSyntaxStepsByteIdentical()
    {
        var fixturePath = GetFixturePath("script-arithmetic-forward.macro");

        var loaded = await _manager.LoadAsync(fixturePath);

        _ = loaded.Should().NotBeNull();
        _ = loaded!.ScriptSteps.Should().Equal(ForwardFixtureScriptSteps);
        _ = loaded.Events.Should().BeEmpty();

        var fixtureText = await File.ReadAllTextAsync(fixturePath, NonCancelableToken);
        _ = ExtractScriptSectionLines(fixtureText).Should().Equal(ForwardFixtureScriptSteps);
    }

    [Fact]
    public async Task ForwardFixture_OldReaderRawFallbackRoute_SurvivesLoadSaveReloadByteIdentical()
    {
        var fixturePath = GetFixturePath("script-arithmetic-forward.macro");
        var loaded = await _manager.LoadAsync(fixturePath);
        _ = loaded.Should().NotBeNull();

        // Old-reader simulation: the pre-feature converter restored unknown tokens
        // (mul/div/expression repeat) as RawScriptStep actions whose Text is the
        // verbatim script line (see EditorActionConverter.CreateRawScriptStepAction).
        var oldReaderActions = loaded!.ScriptSteps
            .Select(step => new EditorAction
            {
                Type = EditorActionType.RawScriptStep,
                Text = step,
            })
            .ToList();

        var resaved = _converter.ToMacroSequence(
            oldReaderActions,
            loaded.Name,
            loaded.IsAbsoluteCoordinates,
            loaded.SkipInitialZeroZero);
        var savedPath = GetTempFilePath();
        await _manager.SaveAsync(resaved, savedPath);

        var fixtureText = await File.ReadAllTextAsync(fixturePath, NonCancelableToken);
        var savedText = await File.ReadAllTextAsync(savedPath, NonCancelableToken);
        _ = ExtractScriptSectionLines(savedText).Should().Equal(ExtractScriptSectionLines(fixtureText));

        var reloaded = await _manager.LoadAsync(savedPath);
        _ = reloaded.Should().NotBeNull();
        _ = reloaded!.ScriptSteps.Should().Equal(ForwardFixtureScriptSteps);
    }

    [Fact]
    public async Task ForwardFixture_CurrentReader_MaterializesNewSyntaxAsTypedActions()
    {
        var loaded = await _manager.LoadAsync(GetFixturePath("script-arithmetic-forward.macro"));
        _ = loaded.Should().NotBeNull();

        var restore = _converter.FromMacroSequenceWithDiagnostics(loaded!);

        _ = restore.HasWarnings.Should().BeFalse();
        _ = restore.Actions.Select(action => action.Type).Should().Equal(
            EditorActionType.SetVariable,
            EditorActionType.MultiplyVariable,
            EditorActionType.DivideVariable,
            EditorActionType.RepeatBlockStart,
            EditorActionType.MouseClick,
            EditorActionType.BlockEnd);

        var multiply = restore.Actions[1];
        _ = multiply.ScriptVariableName.Should().Be("a");
        _ = multiply.ScriptNumericValue.Should().Be("2");
        _ = multiply.ScriptNumericSourceType.Should().Be(ScriptNumericSourceType.Number);

        var divide = restore.Actions[2];
        _ = divide.ScriptVariableName.Should().Be("a");
        _ = divide.ScriptNumericValue.Should().Be("4");
        _ = divide.ScriptNumericSourceType.Should().Be(ScriptNumericSourceType.Number);

        var repeat = restore.Actions[3];
        _ = repeat.ScriptNumericValue.Should().Be("$a / 2");
        _ = repeat.ScriptNumericSourceType.Should().Be(ScriptNumericSourceType.VariableReference);
        _ = repeat.PreferLegacyScriptText.Should().BeFalse();
    }

    [Fact]
    public async Task MixedLegacyAndNewSyntax_RoundTripsLosslessly_ThroughCurrentBuild()
    {
        // The body uses the canonical "click current left" spelling: the converter
        // canonicalizes current-position clicks on rebuild (pre-existing behavior,
        // unrelated to arithmetic), so a lossless round-trip pin starts canonical.
        string[] mixedScriptSteps =
        [
            "set a 10",
            "repeat 3 {",
            "mul a 2",
            "}",
            "repeat $a / 2 {",
            "click current left",
            "}",
        ];
        var filePath = GetTempFilePath();
        var content = "# Name: Mixed Era Macro\n"
            + "# Created: 2026-08-08T09:30:00.0000000Z\n"
            + "# DurationMs: 0\n"
            + "# IsAbsolute: True\n"
            + "# SkipInitialZero: False\n"
            + "# Format: CrossMacroFormatV3\n"
            + "[Script]\n"
            + string.Join('\n', mixedScriptSteps) + "\n"
            + "[Events]\n";
        await File.WriteAllTextAsync(filePath, content, NonCancelableToken);

        var loaded = await _manager.LoadAsync(filePath);
        _ = loaded.Should().NotBeNull();
        _ = loaded!.ScriptSteps.Should().Equal(mixedScriptSteps);

        var restore = _converter.FromMacroSequenceWithDiagnostics(loaded);
        _ = restore.HasWarnings.Should().BeFalse();
        _ = restore.Actions.Select(action => action.Type).Should().Equal(
            EditorActionType.SetVariable,
            EditorActionType.RepeatBlockStart,
            EditorActionType.MultiplyVariable,
            EditorActionType.BlockEnd,
            EditorActionType.RepeatBlockStart,
            EditorActionType.MouseClick,
            EditorActionType.BlockEnd);
        _ = restore.Actions[1].ScriptNumericValue.Should().Be("3");
        _ = restore.Actions[1].ScriptNumericSourceType.Should().Be(ScriptNumericSourceType.Number);
        _ = restore.Actions[4].ScriptNumericValue.Should().Be("$a / 2");

        var rebuilt = _converter.ToMacroSequence(restore.Actions, loaded.Name, loaded.IsAbsoluteCoordinates);
        _ = rebuilt.ScriptSteps.Should().Equal(mixedScriptSteps);

        var savedPath = GetTempFilePath();
        await _manager.SaveAsync(rebuilt, savedPath);
        var reloaded = await _manager.LoadAsync(savedPath);

        _ = reloaded.Should().NotBeNull();
        _ = reloaded!.ScriptSteps.Should().Equal(mixedScriptSteps);

        var reloadedRestore = _converter.FromMacroSequenceWithDiagnostics(reloaded);
        _ = reloadedRestore.HasWarnings.Should().BeFalse();
        _ = reloadedRestore.Actions.Select(action => action.Type).Should().Equal(
            restore.Actions.Select(action => action.Type));
    }

    [Fact]
    public void UnknownToken_RawFallback_RemainsLiveAndPreservesTextVerbatim()
    {
        var sequence = new MacroSequence
        {
            ScriptSteps = { "frobulate x 2" },
        };

        var restore = _converter.FromMacroSequenceWithDiagnostics(sequence);

        _ = restore.HasWarnings.Should().BeTrue();
        _ = restore.Warnings.Should().ContainSingle()
            .Which.Message.Should().Be("Unsupported step restored as raw script text.");
        _ = restore.Actions.Should().ContainSingle();
        _ = restore.Actions[0].Type.Should().Be(EditorActionType.RawScriptStep);
        _ = restore.Actions[0].Text.Should().Be("frobulate x 2");
    }

    [Fact]
    public async Task ForwardFixture_WhenFileIsTruncatedMidScript_StillPreservesCommittedSteps()
    {
        var filePath = GetTempFilePath();
        var content = "# Name: Truncated Forward Macro\n"
            + "# Format: CrossMacroFormatV3\n"
            + "[Script]\n"
            + "set a 10\n"
            + "mul a 2\n"
            + "div a 4\n"
            + "repeat $a / 2 {\n"
            + "click left\n";
        await File.WriteAllTextAsync(filePath, content, NonCancelableToken);

        var loaded = await _manager.LoadAsync(filePath);

        _ = loaded.Should().NotBeNull();
        _ = loaded!.ScriptSteps.Should().Equal(
            "set a 10",
            "mul a 2",
            "div a 4",
            "repeat $a / 2 {",
            "click left");
        _ = loaded.Events.Should().BeEmpty();
    }

    [Fact]
    public async Task LegacyFixture_WhenEventSectionHasGarbageLines_SkipsGarbageAndPreservesScript()
    {
        var filePath = GetTempFilePath();
        var content = "# Name: Garbage Tolerant Legacy Macro\n"
            + "# Created: 2026-01-15T10:30:00.0000000Z\n"
            + "# DurationMs: 150\n"
            + "# IsAbsolute: True\n"
            + "# SkipInitialZero: False\n"
            + "# Format: CrossMacroFormatV3\n"
            + "[Script]\n"
            + string.Join('\n', LegacyFixtureScriptSteps) + "\n"
            + "[Events]\n"
            + "GARBAGE LINE\n"
            + "Z,99,99\n"
            + "M,abs,100,200\n"
            + "C,abs,100,200,Left\n";
        await File.WriteAllTextAsync(filePath, content, NonCancelableToken);

        var loaded = await _manager.LoadAsync(filePath);

        _ = loaded.Should().NotBeNull();
        _ = loaded!.ScriptSteps.Should().Equal(LegacyFixtureScriptSteps);
        _ = loaded.Events.Should().HaveCount(2);
        _ = loaded.Events[0].Type.Should().Be(EventType.MouseMove);
        _ = loaded.Events[1].Type.Should().Be(EventType.Click);

        var restore = _converter.FromMacroSequenceWithDiagnostics(loaded);
        _ = restore.HasWarnings.Should().BeFalse();
        _ = restore.Actions.Should().NotContain(action => action.Type == EditorActionType.RawScriptStep);
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
