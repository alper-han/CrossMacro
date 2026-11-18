namespace CrossMacro.Mcp.Tests;

public sealed class McpImageFailureMapperTests
{
    private static readonly McpImageFailureMessages All = new("invalid", "file", "environment", "runtime");

    [Theory]
    [InlineData(typeof(InvalidDataException), "invalid")]
    [InlineData(typeof(NotSupportedException), "invalid")]
    [InlineData(typeof(ArgumentException), "invalid")]
    [InlineData(typeof(IOException), "file")]
    [InlineData(typeof(UnauthorizedAccessException), "file")]
    [InlineData(typeof(ImageClipboardUnavailableException), "environment")]
    [InlineData(typeof(InvalidOperationException), "runtime")]
    public void TryMap_ClassifiesTheExpectedFailures(Type exceptionType, string expectedMessage)
    {
        var exception = (Exception)Activator.CreateInstance(exceptionType)!;

        Assert.True(McpImageFailureMapper.TryMap(exception, All, out var outcome));
        Assert.Equal(expectedMessage, outcome.Message);
        Assert.False(outcome.Success);
    }

    [Fact]
    public void TryMap_LeavesUnhandledClassesAndUnexpectedExceptionsToPropagate()
    {
        var validationOnly = new McpImageFailureMessages("invalid");

        Assert.False(McpImageFailureMapper.TryMap(new IOException(), validationOnly, out _));
        Assert.False(McpImageFailureMapper.TryMap(new ImageClipboardUnavailableException(), validationOnly, out _));
        Assert.False(McpImageFailureMapper.TryMap(new InvalidOperationException(), validationOnly, out _));
        Assert.False(McpImageFailureMapper.TryMap(new TimeoutException(), All, out _));
        Assert.True(McpImageFailureMapper.TryMap(new InvalidDataException(), validationOnly, out _));
    }
}
