
namespace CrossMacro.Core.Tests.Diagnostics;

public sealed class InputBackendErrorClassifierTests
{
    [Theory]
    [InlineData("No usable Linux input capture backend is available.")]
    [InlineData("No usable Linux input backend is available.")]
    [InlineData("prefix No usable Linux input backend is available. suffix")]
    [InlineData("Failed to init UInput: Cannot open /dev/uinput (Errno: 13). Permission denied.")]
    [InlineData("failed to init uinput: unavailable")]
    public void IsKnownUnavailableMessage_WhenMessageMatches_ReturnsTrue(string message)
    {
        Assert.True(InputBackendErrorClassifier.IsKnownUnavailableMessage(message));
    }

    [Fact]
    public void IsKnownUnavailableMessage_WhenMessageDoesNotMatch_ReturnsFalse()
    {
        Assert.False(InputBackendErrorClassifier.IsKnownUnavailableMessage("Some unrelated error"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void IsKnownUnavailableMessage_WhenMessageIsBlank_ReturnsFalse(string message)
    {
        Assert.False(InputBackendErrorClassifier.IsKnownUnavailableMessage(message));
    }

    [Fact]
    public void IsKnownUnavailable_WhenExceptionMatches_ReturnsTrue()
    {
        var ex = new InvalidOperationException("No usable Linux input backend is available.");
        Assert.True(InputBackendErrorClassifier.IsKnownUnavailable(ex));
    }

    [Fact]
    public void IsKnownUnavailable_WhenInnerExceptionMatches_ReturnsTrue()
    {
        var ex = new InvalidOperationException("wrapper", new InvalidOperationException("No usable Linux input capture backend is available."));
        Assert.True(InputBackendErrorClassifier.IsKnownUnavailable(ex));
    }
}
