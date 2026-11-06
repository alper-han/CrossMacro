namespace CrossMacro.UI.Tests.ViewModels;

public sealed class TrayPreferencePolicyTests
{
    [Fact]
    public void EveryPreferenceCombination_PreservesDesktopDependencyRules()
    {
        for (var combination = 0; combination < 16; combination++)
        {
            var before = new TrayPreferences(
                EnableTrayIcon: (combination & 1) is not 0,
                StartMinimized: (combination & 2) is not 0,
                HideToTrayOnPlayback: (combination & 4) is not 0,
                HideToTrayOnRecording: (combination & 8) is not 0);
            foreach (var preference in Enum.GetValues<TrayPreference>())
            {
                foreach (var value in new[] { false, true })
                {
                    var after = TrayPreferencePolicy.Apply(before, preference, value);
                    Assert.Equal(value, Read(after, preference));
                    if (Read(before, preference) == value)
                    {
                        Assert.Equal(before, after);
                        continue;
                    }
                    foreach (var other in Enum.GetValues<TrayPreference>().Where(other => other != preference))
                    {
                        var expected = Read(before, other);
                        if (preference is TrayPreference.EnableTrayIcon && !value) { expected = false; }
                        if (other is TrayPreference.EnableTrayIcon && value) { expected = true; }
                        Assert.Equal(expected, Read(after, other));
                    }
                }
            }
        }
    }

    [Fact]
    public void UnknownPreference_IsRejected()
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => TrayPreferencePolicy.Apply(
            default, (TrayPreference)int.MaxValue, value: true));
    }

    private static bool Read(TrayPreferences preferences, TrayPreference preference) => preference switch
    {
        TrayPreference.EnableTrayIcon => preferences.EnableTrayIcon,
        TrayPreference.StartMinimized => preferences.StartMinimized,
        TrayPreference.HideToTrayOnPlayback => preferences.HideToTrayOnPlayback,
        TrayPreference.HideToTrayOnRecording => preferences.HideToTrayOnRecording,
        _ => throw new ArgumentOutOfRangeException(nameof(preference), preference, message: null),
    };
}
