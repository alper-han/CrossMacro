namespace CrossMacro.Core.Tests.Models;

public sealed class TextExpansionTests
{
    [Fact]
    public void NewTextExpansion_DefaultConstructor_HasEnabledPasteDefaults()
    {
        var expansion = new TextExpansionEntry();

        _ = expansion.Trigger.Should().BeEmpty();
        _ = expansion.Replacement.Should().BeEmpty();
        _ = expansion.IsEnabled.Should().BeTrue();
        _ = expansion.Method.Should().Be(PasteMethod.CtrlV);
        _ = expansion.InsertionMode.Should().Be(TextInsertionMode.Paste);
        _ = expansion.DirectTypingMethod.Should().Be(DirectTypingMethod.FastBatch);
    }

    [Fact]
    public void ParameterizedConstructor_AssignsEveryValue()
    {
        var expansion = new TextExpansionEntry(
            ":mail",
            "test@example.com",
            isEnabled: false,
            PasteMethod.CtrlShiftV,
            TextInsertionMode.DirectTyping,
            DirectTypingMethod.CompatibleKeyByKey);

        _ = expansion.Trigger.Should().Be(":mail");
        _ = expansion.Replacement.Should().Be("test@example.com");
        _ = expansion.IsEnabled.Should().BeFalse();
        _ = expansion.Method.Should().Be(PasteMethod.CtrlShiftV);
        _ = expansion.InsertionMode.Should().Be(TextInsertionMode.DirectTyping);
        _ = expansion.DirectTypingMethod.Should().Be(DirectTypingMethod.CompatibleKeyByKey);
    }
}
