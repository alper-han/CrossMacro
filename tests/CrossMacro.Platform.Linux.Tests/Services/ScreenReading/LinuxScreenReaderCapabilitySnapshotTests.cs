namespace CrossMacro.Platform.Linux.Tests.Services.ScreenReading;

public sealed class LinuxScreenReaderCapabilitySnapshotTests
{
    [Fact]
    public void Available_PreservesDetailsAndHasNoErrorState()
    {
        var capability = LinuxScreenReaderBackendCapability.Available(
            LinuxScreenReaderBackend.ExtImageCopy,
            "supported by compositor");

        Assert.Equal(LinuxScreenReaderBackend.ExtImageCopy, capability.Backend);
        Assert.True(capability.IsAvailable);
        Assert.Null(capability.ErrorKind);
        Assert.Null(capability.ErrorMessage);
        Assert.Equal("supported by compositor", capability.Details);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Unavailable_RejectsBlankErrorMessage(string errorMessage)
    {
        _ = Assert.Throws<ArgumentException>(() => LinuxScreenReaderBackendCapability.Unavailable(
            LinuxScreenReaderBackend.Portal,
            ScreenReadErrorKind.BackendUnavailable,
            errorMessage));
    }

    [Fact]
    public void FourArgumentConstructor_DefaultsGnomeExtensionToUnavailable()
    {
        var snapshot = new LinuxScreenReaderCapabilitySnapshot(
            LinuxScreenReaderBackendCapability.Available(LinuxScreenReaderBackend.KWinScreenShot2),
            LinuxScreenReaderBackendCapability.Available(LinuxScreenReaderBackend.ExtImageCopy),
            LinuxScreenReaderBackendCapability.Available(LinuxScreenReaderBackend.WlrScreencopy),
            LinuxScreenReaderBackendCapability.Available(LinuxScreenReaderBackend.Portal));

        Assert.Equal(LinuxScreenReaderBackend.GnomeExtension, snapshot.GnomeExtension.Backend);
        Assert.False(snapshot.GnomeExtension.IsAvailable);
        Assert.Equal(ScreenReadErrorKind.BackendUnavailable, snapshot.GnomeExtension.ErrorKind);
        Assert.Contains("unavailable", snapshot.GnomeExtension.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetCapability_MapsEveryBackendToItsNamedValue()
    {
        var kwin = LinuxScreenReaderBackendCapability.Available(LinuxScreenReaderBackend.KWinScreenShot2);
        var ext = LinuxScreenReaderBackendCapability.Available(LinuxScreenReaderBackend.ExtImageCopy);
        var wlr = LinuxScreenReaderBackendCapability.Available(LinuxScreenReaderBackend.WlrScreencopy);
        var portal = LinuxScreenReaderBackendCapability.Available(LinuxScreenReaderBackend.Portal);
        var gnome = LinuxScreenReaderBackendCapability.Available(LinuxScreenReaderBackend.GnomeExtension);
        var snapshot = new LinuxScreenReaderCapabilitySnapshot(kwin, ext, wlr, portal, gnome);

        Assert.Equal(kwin, snapshot.GetCapability(LinuxScreenReaderBackend.KWinScreenShot2));
        Assert.Equal(ext, snapshot.GetCapability(LinuxScreenReaderBackend.ExtImageCopy));
        Assert.Equal(wlr, snapshot.GetCapability(LinuxScreenReaderBackend.WlrScreencopy));
        Assert.Equal(portal, snapshot.GetCapability(LinuxScreenReaderBackend.Portal));
        Assert.Equal(gnome, snapshot.GetCapability(LinuxScreenReaderBackend.GnomeExtension));
    }

    [Fact]
    public void GetCapability_UnknownBackendThrows()
    {
        var snapshot = CreateSnapshot();

        _ = Assert.Throws<ArgumentOutOfRangeException>(() => snapshot.GetCapability((LinuxScreenReaderBackend)999));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void NotApplicable_RejectsBlankReason(string reason)
    {
        _ = Assert.Throws<ArgumentException>(() => LinuxScreenReaderCapabilitySnapshot.NotApplicable(reason));
    }

    [Fact]
    public void NotApplicable_MarksEveryBackendUnsupportedWithTheReason()
    {
        const string reason = "not a supported display session";
        var snapshot = LinuxScreenReaderCapabilitySnapshot.NotApplicable(reason);

        foreach (var backend in Enum.GetValues<LinuxScreenReaderBackend>())
        {
            var capability = snapshot.GetCapability(backend);

            Assert.Equal(backend, capability.Backend);
            Assert.False(capability.IsAvailable);
            Assert.Equal(ScreenReadErrorKind.Unsupported, capability.ErrorKind);
            Assert.Equal(reason, capability.ErrorMessage);
        }
    }

    private static LinuxScreenReaderCapabilitySnapshot CreateSnapshot() =>
        new(
            LinuxScreenReaderBackendCapability.Available(LinuxScreenReaderBackend.KWinScreenShot2),
            LinuxScreenReaderBackendCapability.Available(LinuxScreenReaderBackend.ExtImageCopy),
            LinuxScreenReaderBackendCapability.Available(LinuxScreenReaderBackend.WlrScreencopy),
            LinuxScreenReaderBackendCapability.Available(LinuxScreenReaderBackend.Portal));
}
