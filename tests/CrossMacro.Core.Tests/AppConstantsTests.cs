namespace CrossMacro.Core.Tests;

using CrossMacro.Core;
using FluentAssertions;

public class AppConstantsTests
{
    [Fact]
    public void AppName_IsCorrect()
    {
        AppConstants.AppName.Should().Be("CrossMacro");
    }

    [Fact]
    public void AppIdentifier_IsLowercase()
    {
        AppConstants.AppIdentifier.Should().Be("crossmacro");
        AppConstants.AppIdentifier.Should().Be(AppConstants.AppIdentifier.ToLowerInvariant());
    }

    [Fact]
    public void DBusNamespace_HasCorrectFormat()
    {
        AppConstants.DBusNamespace.Should().Be("org.crossmacro");
        AppConstants.DBusNamespace.Should().StartWith("org.");
    }

    [Fact]
    public void DefaultHotkeys_AreNotEmpty()
    {
        AppConstants.DefaultRecordingHotkey.Should().NotBeNullOrEmpty();
        AppConstants.DefaultPlaybackHotkey.Should().NotBeNullOrEmpty();
        AppConstants.DefaultPauseHotkey.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void DefaultHotkeys_AreFunctionKeys()
    {
        AppConstants.DefaultRecordingHotkey.Should().Be("F8");
        AppConstants.DefaultPlaybackHotkey.Should().Be("F9");
        AppConstants.DefaultPauseHotkey.Should().Be("F10");
    }

    [Fact]
    public void DefaultHotkeys_AreAllDifferent()
    {
        var hotkeys = new[]
        {
            AppConstants.DefaultRecordingHotkey,
            AppConstants.DefaultPlaybackHotkey,
            AppConstants.DefaultPauseHotkey
        };

        hotkeys.Should().OnlyHaveUniqueItems();
    }
}
