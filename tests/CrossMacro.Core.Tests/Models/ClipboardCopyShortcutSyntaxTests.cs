namespace CrossMacro.Core.Tests.Models;

public sealed class ClipboardCopyShortcutSyntaxTests
{
    [Theory]
    [InlineData(ClipboardCopyShortcut.CtrlC, ClipboardCopyShortcutSyntax.CtrlCScriptToken)]
    [InlineData(ClipboardCopyShortcut.CtrlShiftC, ClipboardCopyShortcutSyntax.CtrlShiftCScriptToken)]
    public void ToScriptToken_ReturnsCanonicalToken(ClipboardCopyShortcut shortcut, string expectedToken)
    {
        ClipboardCopyShortcutSyntax.ToScriptToken(shortcut).Should().Be(expectedToken);
    }

    [Theory]
    [InlineData("ctrl+c", ClipboardCopyShortcut.CtrlC)]
    [InlineData(" CTRL+SHIFT+C ", ClipboardCopyShortcut.CtrlShiftC)]
    public void TryParse_WhenTokenIsSupported_ReturnsShortcut(string token, ClipboardCopyShortcut expectedShortcut)
    {
        var parsed = ClipboardCopyShortcutSyntax.TryParse(token, out var shortcut);

        _ = parsed.Should().BeTrue();
        _ = shortcut.Should().Be(expectedShortcut);
    }

    [Theory]
    [InlineData("")]
    [InlineData("ctrl+v")]
    [InlineData("ctrl+c extra")]
    public void TryParse_WhenTokenIsUnsupported_ReturnsFalse(string token)
    {
        var parsed = ClipboardCopyShortcutSyntax.TryParse(token, out var shortcut);

        _ = parsed.Should().BeFalse();
        _ = shortcut.Should().Be(default);
    }
}
