using CrossMacro.Platform.Linux.Services.QuickSetup;
using Xunit;

namespace CrossMacro.Platform.Linux.Tests.Services;

public sealed class LinuxQuickSetupScriptBuilderTests
{
    [Fact]
    public void Build_WhenLenient_ShouldNotRequireDevices()
    {
        var builder = new LinuxQuickSetupScriptBuilder();

        var script = builder.Build(LinuxQuickSetupScriptOptions.Lenient);

        Assert.DoesNotContain("uinput_ok=0", script);
        Assert.DoesNotContain("event_ok=0", script);
        Assert.Contains("setfacl -m \"u:${TARGET_IDENTITY}:rw\"", script);
        Assert.Contains("setfacl -m \"u:${TARGET_IDENTITY}:r\"", script);
    }

    [Fact]
    public void Build_WhenStrict_ShouldRequireDevices()
    {
        var builder = new LinuxQuickSetupScriptBuilder();

        var script = builder.Build(LinuxQuickSetupScriptOptions.Strict);

        Assert.Contains("uinput_ok=0", script);
        Assert.Contains("event_ok=0", script);
        Assert.Contains("exit 24", script);
        Assert.Contains("exit 25", script);
    }
}
