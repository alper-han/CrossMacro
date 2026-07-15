
namespace CrossMacro.UI.ViewModels;

internal sealed class DesignTimeProvider : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => DesignPreviewSamples.SampleNow.ToUniversalTime();
}
