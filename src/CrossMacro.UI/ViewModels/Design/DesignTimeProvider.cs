
namespace CrossMacro.UI.ViewModels.Design;

internal sealed class DesignTimeProvider : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => new DateTimeOffset(DesignPreviewSamples.SampleNow.ToUniversalTime());
}
