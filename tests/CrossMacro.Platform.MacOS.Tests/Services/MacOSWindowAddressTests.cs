namespace CrossMacro.Platform.MacOS.Tests.Services;

public sealed class MacOSWindowAddressTests
{
    [Fact]
    public void FormatAndParse_RoundTripUnicodeAndNegativeCoordinatesAsShellSafeToken()
    {
        var expected = new MacOSWindowAddress(123, 456, "Başlık\nSecond;Part", -1920, -200, 1280, 720);

        var formatted = expected.Format();

        Assert.True(MacOSWindowAddress.TryParse(formatted + ".42", out var parsed));
        Assert.Equal(expected, parsed);
        Assert.DoesNotContain(';', formatted);
        Assert.DoesNotContain(' ', formatted);
    }

    [Theory]
    [InlineData("")]
    [InlineData("ax:1")]
    [InlineData("ax2-not-base64")]
    public void TryParse_RejectsMalformedAddresses(string address)
    {
        Assert.False(MacOSWindowAddress.TryParse(address, out _));
    }
}
