
namespace CrossMacro.TestInfrastructure;

internal sealed class NonMacOSFactAttribute : FactAttribute
{
    public NonMacOSFactAttribute()
    {
        if (OperatingSystem.IsMacOS())
        {
            Skip = ConditionalSkipMessage.For("non-macOS environment");
        }
    }
}
