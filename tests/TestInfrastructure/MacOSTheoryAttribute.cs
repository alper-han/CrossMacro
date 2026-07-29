
namespace CrossMacro.TestInfrastructure;

internal sealed class MacOSTheoryAttribute : TheoryAttribute
{
    public MacOSTheoryAttribute()
    {
        if (!OperatingSystem.IsMacOS())
        {
            Skip = ConditionalSkipMessage.For("macOS");
        }
    }
}
