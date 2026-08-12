namespace CrossMacro.Platform.Linux.Tests.Services.ScreenReading;

public sealed class PortalPipeWireBufferTypePolicyTests
{
    [Fact]
    public void MemFd_IsSupported()
    {
        Assert.True(PipeWireBufferTypePolicy.IsSupported(PipeWireBufferTypePolicy.SpaDataMemFd));
    }

    [Fact]
    public void DmaBuf_IsRejectedWithActionableMessage()
    {
        Assert.False(PipeWireBufferTypePolicy.IsSupported(PipeWireBufferTypePolicy.SpaDataDmaBuf));
        Assert.Contains("DmaBuf", PipeWireBufferTypePolicy.DescribeUnsupported(PipeWireBufferTypePolicy.SpaDataDmaBuf), StringComparison.Ordinal);
        Assert.Contains("modifier", PipeWireBufferTypePolicy.DescribeUnsupported(PipeWireBufferTypePolicy.SpaDataDmaBuf), StringComparison.Ordinal);
    }
}
