
namespace CrossMacro.TestInfrastructure;

internal sealed class MacOSFactAttribute : FactAttribute
{
    public MacOSFactAttribute()
    {
        if (!OperatingSystem.IsMacOS())
        {
            Skip = ConditionalSkipMessage.For("macOS");
        }
    }
}
