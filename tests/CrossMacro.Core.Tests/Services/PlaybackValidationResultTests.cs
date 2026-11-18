namespace CrossMacro.Core.Tests.Services;

public sealed class PlaybackValidationResultTests
{
    [Fact]
    public void IsValid_IgnoresWarningsAndBecomesFalseOnFirstError()
    {
        var result = new PlaybackValidationResult();
        Assert.True(result.IsValid);

        result.AddWarning("Some warning");
        Assert.True(result.IsValid);
        Assert.Equal(["Some warning"], result.Warnings);

        result.AddError("Some error");
        Assert.False(result.IsValid);
        Assert.Equal(["Some error"], result.Errors);
    }
}
