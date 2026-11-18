namespace CrossMacro.Core.Tests.Models;

public sealed class McpSecuritySettingCatalogTests
{
    private static readonly McpSecuritySetting[] AllSettings = Enum.GetValues<McpSecuritySetting>();

    [Fact]
    public void Values_ContainsEveryCapabilityExactlyOnce()
    {
        _ = McpSecuritySettingCatalog.Values.Should().BeEquivalentTo(AllSettings);
        _ = McpSecuritySettingCatalog.Values.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Keys_AreUniquePolicyKeysThatRoundTrip()
    {
        var keys = AllSettings.Select(McpSecuritySettingCatalog.GetKey).ToList();

        _ = keys.Should().OnlyHaveUniqueItems();
        _ = keys.Should().OnlyContain(key => McpSettingsKeys.IsPolicyKey(key));
        foreach (var setting in AllSettings)
        {
            _ = McpSecuritySettingCatalog.TryParse(McpSecuritySettingCatalog.GetKey(setting), out var parsed).Should().BeTrue();
            _ = parsed.Should().Be(setting);
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("mcp.unknown")]
    [InlineData("MCP.MACROREAD")]
    [InlineData("macroRead")]
    public void TryParse_UnknownKey_ReturnsFalse(string key)
    {
        _ = McpSecuritySettingCatalog.TryParse(key, out _).Should().BeFalse();
    }

    [Fact]
    public void TryParse_NullKey_ReturnsFalse()
    {
        _ = McpSecuritySettingCatalog.TryParse(null!, out _).Should().BeFalse();
    }

    [Fact]
    public void Defaults_DenyOnlyFileWriteCommandShellAndSettingsWrite()
    {
        var settings = new McpSecuritySettings();
        McpSecuritySetting[] denied =
        [
            McpSecuritySetting.FileWrite,
            McpSecuritySetting.CommandExecute,
            McpSecuritySetting.ShellExecute,
            McpSecuritySetting.SettingsWrite,
        ];

        foreach (var setting in AllSettings)
        {
            _ = settings.IsAllowed(setting).Should().Be(!denied.Contains(setting), setting.ToString());
        }
    }

    [Fact]
    public void Set_ChangesOnlyTheRequestedCapability()
    {
        foreach (var target in AllSettings)
        {
            var settings = new McpSecuritySettings();
            var before = AllSettings.ToDictionary(static setting => setting, settings.IsAllowed);

            settings.Set(target, !before[target]);

            foreach (var setting in AllSettings)
            {
                var expected = setting == target ? !before[setting] : before[setting];
                _ = settings.IsAllowed(setting).Should().Be(expected, $"{target} toggled, checking {setting}");
            }
        }
    }

    [Fact]
    public void UndefinedCapability_IsDeniedAndCannotBeSetOrNamed()
    {
        var undefined = (McpSecuritySetting)999;
        var settings = new McpSecuritySettings();

        _ = settings.IsAllowed(undefined).Should().BeFalse();
        _ = ((Action)(() => settings.Set(undefined, value: true))).Should().Throw<ArgumentOutOfRangeException>();
        _ = ((Action)(() => McpSecuritySettingCatalog.GetKey(undefined))).Should().Throw<ArgumentOutOfRangeException>();
    }
}
