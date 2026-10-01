namespace CrossMacro.Infrastructure.Tests.Services;

public sealed class ShortcutHotkeyNormalizerTests
{
    private readonly ShortcutHotkeyNormalizer _normalizer = CreateNormalizer();

    [Fact]
    public void TryNormalize_UsesCanonicalModifierOrderAndMappedKeyName()
    {
        var accepted = _normalizer.TryNormalize(" Shift + Ctrl + space ", out var normalized, out var error);

        _ = accepted.Should().BeTrue(error);
        _ = normalized.Should().Be("Ctrl+Shift+Space");
        _ = error.Should().BeNull();
    }
    [Theory]
    [InlineData("Ctrl+Mouse Left", "Ctrl+Mouse Left")]
    [InlineData("ctrl+mouse left", "Ctrl+Mouse Left")]
    public void TryNormalize_PreservesCanonicalMouseButtonName(string input, string expected)
    {
        var accepted = _normalizer.TryNormalize(input, out var normalized, out var error);

        _ = accepted.Should().BeTrue(error);
        _ = normalized.Should().Be(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Ctrl+")]
    [InlineData("Unknown+Space")]
    [InlineData("Ctrl+Shift")]
    [InlineData("Space+F1")]
    [InlineData("Ctrl+Ctrl+Space")]
    [InlineData("Alt+Alt+Space")]
    [InlineData("Mouse Left")]
    [InlineData("Mouse Right")]
    public void TryNormalize_RejectsInvalidChord(string input)
    {
        var accepted = _normalizer.TryNormalize(input, out var normalized, out var error);

        _ = accepted.Should().BeFalse();
        _ = normalized.Should().BeNull();
        _ = error.Should().NotBeNullOrWhiteSpace();
    }

    private static ShortcutHotkeyNormalizer CreateNormalizer()
    {
        var mapper = new TestKeyCodeMapper();
        return new ShortcutHotkeyNormalizer(mapper, new HotkeyStringBuilder(mapper), new MouseButtonMapper());
    }

    private sealed class TestKeyCodeMapper : IKeyCodeMapper
    {
        private static readonly IReadOnlyDictionary<string, int> KeyCodes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["Ctrl"] = InputEventCode.KEY_LEFTCTRL,
            ["Shift"] = InputEventCode.KEY_LEFTSHIFT,
            ["Alt"] = InputEventCode.KEY_LEFTALT,
            ["AltGr"] = InputEventCode.KEY_RIGHTALT,
            ["Super"] = InputEventCode.KEY_LEFTMETA,
            ["Space"] = InputEventCode.KEY_SPACE,
            ["F1"] = 59,
            ["Mouse Left"] = 272,
        };

        public int GetKeyCode(string keyName) => KeyCodes.TryGetValue(keyName, out var code) ? code : -1;
        public string GetKeyName(int keyCode) => keyCode == InputEventCode.KEY_SPACE
            ? "Space"
            : "Key" + keyCode.ToString(System.Globalization.CultureInfo.InvariantCulture);
        public bool IsModifierKeyCode(int code) => InputEventCode.IsModifierKey(code);
        public int GetKeyCodeForCharacter(char character) => -1;
        public bool RequiresShift(char character) => false;
        public char? GetCharacterForKeyCode(int keyCode, bool withShift = false) => null;
        public bool RequiresAltGr(char character) => false;
    }
}
