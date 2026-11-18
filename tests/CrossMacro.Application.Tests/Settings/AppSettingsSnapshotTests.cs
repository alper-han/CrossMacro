using System.Reflection;
using CrossMacro.Application.Settings;
using CrossMacro.Core.Models.Settings;

namespace CrossMacro.Application.Tests.Settings;

/// <summary>
/// The snapshot field list is maintained by hand; these tests fail when a setting is added to
/// <see cref="AppSettings"/> or <see cref="McpSecuritySettings"/> without being copied.
/// </summary>
public sealed class AppSettingsSnapshotTests
{
    private static readonly PropertyInfo[] ScalarSettings = typeof(AppSettings)
        .GetProperties(BindingFlags.Public | BindingFlags.Instance)
        .Where(property => property.CanWrite && property.PropertyType != typeof(McpSecuritySettings))
        .ToArray();

    public static TheoryData<string> ScalarSettingNames => new(ScalarSettings.Select(static property => property.Name));

    public static TheoryData<string> McpAllowNames => new(typeof(McpSecuritySettings)
        .GetProperties(BindingFlags.Public | BindingFlags.Instance)
        .Where(static property => property.PropertyType == typeof(bool) && property.CanWrite)
        .Select(static property => property.Name));

    [Theory]
    [MemberData(nameof(ScalarSettingNames))]
    public void Copy_PreservesEveryScalarSetting(string name)
    {
        var property = ScalarSettings.Single(candidate => string.Equals(candidate.Name, name, StringComparison.Ordinal));
        var source = new AppSettings();
        var value = NonDefaultValue(property, property.GetValue(source));
        property.SetValue(source, value);

        var copy = AppSettingsSnapshot.Copy(source);

        Assert.Equal(value, property.GetValue(copy));
        Assert.NotEqual(property.GetValue(new AppSettings()), property.GetValue(copy));
    }

    [Theory]
    [MemberData(nameof(McpAllowNames))]
    public void Copy_PreservesEveryMcpPermission(string name)
    {
        var property = typeof(McpSecuritySettings).GetProperty(name)!;
        var source = new AppSettings();
        var flipped = !(bool)property.GetValue(source.McpSecurity)!;
        property.SetValue(source.McpSecurity, flipped);

        var copy = AppSettingsSnapshot.Copy(source);

        Assert.Equal(flipped, property.GetValue(copy.McpSecurity));
    }

    [Fact]
    public void Copy_PreservesEveryPathRootList()
    {
        var source = new AppSettings();
        foreach (var setting in Enum.GetValues<McpPathSetting>())
        {
            source.McpSecurity.Paths = source.McpSecurity.Paths.WithRoots(setting, [$"/roots/{setting}/a", $"/roots/{setting}/b"]);
        }

        var copy = AppSettingsSnapshot.Copy(source);

        foreach (var setting in Enum.GetValues<McpPathSetting>())
        {
            Assert.Equal(source.McpSecurity.Paths.GetRoots(setting), copy.McpSecurity.Paths.GetRoots(setting));
        }
    }

    private static object NonDefaultValue(PropertyInfo property, object? current)
    {
        var type = property.PropertyType;
        if (type == typeof(bool))
        {
            return !(bool)current!;
        }

        if (type == typeof(int))
        {
            return (int)current! + 7;
        }

        if (type == typeof(double))
        {
            return (double)current! + 1.5;
        }

        if (type == typeof(string))
        {
            return $"changed-{property.Name}";
        }

        if (type.IsEnum)
        {
            return Enum.GetValues(type).Cast<object>().First(candidate => !Equals(candidate, current));
        }

        throw new NotSupportedException($"The completeness test cannot vary the type of AppSettings.{property.Name}: {type}.");
    }
}
