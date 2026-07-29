
namespace CrossMacro.TestInfrastructure;

internal sealed class LinuxTheoryAttribute : TheoryAttribute
{
    public LinuxTheoryAttribute()
    {
        if (!OperatingSystem.IsLinux())
        {
            Skip = ConditionalSkipMessage.For("Linux");
        }
    }
}
