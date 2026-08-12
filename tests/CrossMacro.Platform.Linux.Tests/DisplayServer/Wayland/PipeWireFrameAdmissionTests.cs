namespace CrossMacro.Platform.Linux.Tests.DisplayServer.Wayland;

public sealed class PipeWireFrameAdmissionTests
{
    [Fact]
    public void AcceptsNextUsableFrame_WhenSettlingIsRequired_PrimesThenAccepts()
    {
        var admission = new PipeWireFrameAdmission(requiresSettlingFrame: true);

        Assert.False(admission.AcceptsNextUsableFrame());
        Assert.True(admission.AcceptsNextUsableFrame());
    }

    [Fact]
    public void AcceptsNextUsableFrame_WhenSettlingIsNotRequired_AcceptsImmediately()
    {
        var admission = new PipeWireFrameAdmission(requiresSettlingFrame: false);

        Assert.True(admission.AcceptsNextUsableFrame());
    }

}
