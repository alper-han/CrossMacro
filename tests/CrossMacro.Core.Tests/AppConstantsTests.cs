namespace CrossMacro.Core.Tests;


public sealed class AppConstantsTests
{
    [Fact]
    public void AppName_IsCorrect()
    {
        _ = AppConstants.AppName.Should().Be("CrossMacro");
    }

    [Fact]
    public void AppIdentifier_IsLowercase()
    {
        _ = AppConstants.AppIdentifier.Should().Be("crossmacro");
    }

    [Fact]
    public void DBusNamespace_HasCorrectFormat()
    {
        _ = AppConstants.DBusNamespace.Should().Be("io.github.alper_han.crossmacro");
        _ = AppConstants.DBusNamespace.Should().StartWith("io.");
    }

    [Fact]
    public void DefaultHotkeys_AreNotEmpty()
    {
        _ = AppConstants.DefaultRecordingHotkey.Should().NotBeNullOrEmpty();
        _ = AppConstants.DefaultPlaybackHotkey.Should().NotBeNullOrEmpty();
        _ = AppConstants.DefaultPauseHotkey.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void DefaultHotkeys_AreFunctionKeys()
    {
        _ = AppConstants.DefaultRecordingHotkey.Should().Be("F8");
        _ = AppConstants.DefaultPlaybackHotkey.Should().Be("F9");
        _ = AppConstants.DefaultPauseHotkey.Should().Be("F10");
    }

    [Fact]
    public void DefaultHotkeys_AreAllDifferent()
    {
        var hotkeys = new[]
        {
            AppConstants.DefaultRecordingHotkey,
            AppConstants.DefaultPlaybackHotkey,
            AppConstants.DefaultPauseHotkey,
        };

        _ = hotkeys.Should().OnlyHaveUniqueItems();
    }
}
