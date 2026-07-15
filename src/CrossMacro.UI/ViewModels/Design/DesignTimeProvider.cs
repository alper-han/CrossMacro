
namespace CrossMacro.UI.ViewModels;

internal sealed class DesignTimeProvider : ITimeProvider
{
    public DateTime Now => DesignPreviewSamples.SampleNow;

    public DateTime UtcNow => DesignPreviewSamples.SampleNow.ToUniversalTime();
}
