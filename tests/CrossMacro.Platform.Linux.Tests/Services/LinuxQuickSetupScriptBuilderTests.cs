
namespace CrossMacro.Platform.Linux.Tests.Services;

public sealed class LinuxQuickSetupScriptBuilderTests
{
    [Fact]
    public void Build_WhenLenient_ShouldNotRequireDevices()
    {
        var script = LinuxQuickSetupScriptBuilder.Build(LinuxQuickSetupScriptOptions.Lenient);

        Assert.DoesNotContain("uinput_ok=0", script, StringComparison.Ordinal);
        Assert.DoesNotContain("event_ok=0", script, StringComparison.Ordinal);
        Assert.Contains("uinput_count=0", script, StringComparison.Ordinal);
        Assert.Contains("event_count=0", script, StringComparison.Ordinal);
        Assert.Contains("setfacl -m \"u:${TARGET_IDENTITY}:rw\"", script, StringComparison.Ordinal);
        Assert.Contains("setfacl -m \"u:${TARGET_IDENTITY}:r\"", script, StringComparison.Ordinal);
        Assert.Contains("Applied session ACLs for ${TARGET_IDENTITY}: uinput=${uinput_count}, input-events=${event_count}.", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_WhenStrict_ShouldRequireDevices()
    {
        var script = LinuxQuickSetupScriptBuilder.Build(LinuxQuickSetupScriptOptions.Strict);

        Assert.Contains("uinput_ok=0", script, StringComparison.Ordinal);
        Assert.Contains("event_ok=0", script, StringComparison.Ordinal);
        Assert.Contains("exit 24", script, StringComparison.Ordinal);
        Assert.Contains("exit 25", script, StringComparison.Ordinal);
        Assert.Contains("exit 26", script, StringComparison.Ordinal);
    }
}
